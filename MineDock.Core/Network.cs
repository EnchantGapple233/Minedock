using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace MineDock.Core;

public sealed class Network : IDisposable
{
    private static readonly SemaphoreSlim SegmentGate = new(16);
    private readonly HttpClient _client;
    public SourceMode Source { get; set; }
    public Network()
    {
        _client = new(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
        _client.Timeout = Timeout.InfiniteTimeSpan;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("MineDock/0.1.0 (Windows desktop)");
    }
    public static string? Mirror(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return null;
        return u.Host switch
        {
            "libraries.minecraft.net" => "https://bmclapi2.bangbang93.com/maven" + u.PathAndQuery,
            "resources.download.minecraft.net" => "https://bmclapi2.bangbang93.com/assets" + u.PathAndQuery,
            "piston-meta.mojang.com" or "piston-data.mojang.com" or "launchermeta.mojang.com"
                or "launcher.mojang.com"
                => "https://bmclapi2.bangbang93.com" + u.PathAndQuery,
            _ => null
        };
    }
    public IEnumerable<string> Candidates(string url)
    {
        var mirror = Mirror(url);
        if (Source == SourceMode.Official || mirror is null) return [url];
        return Source == SourceMode.Bmclapi ? [mirror] : [mirror, url];
    }
    public async Task<JsonNode> Json(string url, CancellationToken ct = default)
    {
        Exception? last = null;
        var lastHost = new Uri(url).Host;
        foreach (var candidate in Candidates(url))
        {
            lastHost = new Uri(candidate).Host;
            for (var attempt = 0; attempt < 2; attempt++)
            {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            try
            {
                using var response = await _client.GetAsync(candidate, timeout.Token);
                response.EnsureSuccessStatusCode();
                return JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))
                       ?? throw new InvalidDataException("服务器返回了空数据。");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { last = ex; if (attempt == 0) await Task.Delay(300, ct); }
            }
        }
        throw new IOException($"获取数据失败：{lastHost}。{Friendly(last)}", last);
    }
    public async Task<JsonNode> Post(string url, object body, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await _client.PostAsJsonAsync(url, body, timeout.Token);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))!;
    }
    public async Task<string> Text(string url, CancellationToken ct = default)
    {
        Exception? last = null;
        foreach (var candidate in Candidates(url))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            try { return await _client.GetStringAsync(candidate, timeout.Token); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { last = ex; }
        }
        throw new IOException($"获取校验信息失败：{new Uri(url).Host}。{Friendly(last)}", last);
    }
    public async Task Download(PlannedFile file, string cache, Action<long>? progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(file.Hash)) throw new InvalidDataException("远程文件缺少上游校验值：" + file.Label);
        Directory.CreateDirectory(cache);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            file.Url + "|" + file.HashAlgorithm + "|" + file.Hash)));
        var target = Path.Combine(cache, key);
        if (File.Exists(target) && await Valid(target, file, ct)) { file.CachePath = target; return; }
        Exception? last = null;
        await foreach (var candidate in DownloadCandidates(file.Url, ct))
        {
            var rangesAllowed = true;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var partial = target + ".part";
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromMinutes(30));
                    using var request = new HttpRequestMessage(HttpMethod.Get, candidate);
                    if (new Uri(candidate).Host == "api.github.com" && new Uri(candidate).AbsolutePath.Contains("/releases/assets/"))
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                    using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    if (rangesAllowed && file.Size >= 16L * 1024 * 1024 &&
                        response.Content.Headers.ContentLength == file.Size && response.Headers.AcceptRanges.Contains("bytes"))
                    {
                        var resolved = response.RequestMessage?.RequestUri ?? new Uri(candidate);
                        response.Dispose();
                        await DownloadSegments(resolved, partial, file.Size, progress, timeout.Token);
                    }
                    else
                    {
                        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                        await using var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                        await CopyProgress(input, output, progress, timeout.Token);
                    }
                    if (!await Valid(partial, file, ct))
                    {
                        DeleteSegments(partial);
                        throw new InvalidDataException("文件大小或校验和不匹配。");
                    }
                    File.Move(partial, target, true);
                    DeleteSegments(partial);
                    file.CachePath = target;
                    return;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    last = ex;
                    if (ex is NotSupportedException) rangesAllowed = false;
                    if (File.Exists(partial)) File.Delete(partial);
                    if (attempt == 0) await Task.Delay(400, ct);
                }
            }
        }
        throw new IOException($"下载失败：{file.Label}。{Friendly(last)}", last);
    }
    private static async Task<long> CopyProgress(Stream input, Stream output, Action<long>? progress, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            length += read;
            progress?.Invoke(read);
        }
        return length;
    }
    private async Task DownloadSegments(Uri uri, string partial, long length, Action<long>? progress, CancellationToken ct)
    {
        const int count = 16;
        var size = (length + count - 1) / count;
        var chunks = Enumerable.Range(0, count).Select(i => partial + ".segment-" + i).ToArray();
        using var failure = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await Task.WhenAll(Enumerable.Range(0, count).Select(async i =>
        {
            await SegmentGate.WaitAsync(failure.Token);
            try
            {
                var start = i * size;
                var end = Math.Min(length - 1, start + size - 1);
                var existing = File.Exists(chunks[i]) ? new FileInfo(chunks[i]).Length : 0;
                if (existing > end - start + 1) { File.Delete(chunks[i]); existing = 0; }
                if (existing > 0) progress?.Invoke(existing);
                if (existing == end - start + 1) return;
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(start + existing, end);
                request.Headers.AcceptEncoding.ParseAdd("identity");
                if (uri.Host == "api.github.com")
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using var reply = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, failure.Token);
                if (reply.StatusCode == HttpStatusCode.OK)
                    throw new NotSupportedException("下载来源未响应分段请求，将改为普通下载。");
                reply.EnsureSuccessStatusCode();
                var range = reply.Content.Headers.ContentRange;
                if (reply.StatusCode != HttpStatusCode.PartialContent || range?.From != start + existing || range.To != end || range.Length != length)
                    throw new InvalidDataException("下载来源返回了不正确的文件分段。");
                await using var input = await reply.Content.ReadAsStreamAsync(failure.Token);
                await using var output = new FileStream(chunks[i], FileMode.Append, FileAccess.Write, FileShare.None, 81920, true);
                if (await CopyProgress(input, output, progress, failure.Token) + existing != end - start + 1)
                {
                    throw new InvalidDataException("下载的文件分段不完整。");
                }
            }
            catch { failure.Cancel(); throw; }
            finally { SegmentGate.Release(); }
        }));
        await using var merged = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        foreach (var chunk in chunks)
        {
            await using var input = File.OpenRead(chunk);
            await input.CopyToAsync(merged, ct);
        }
    }
    private static void DeleteSegments(string partial)
    {
        for (var i = 0; i < 16; i++)
            if (File.Exists(partial + ".segment-" + i)) File.Delete(partial + ".segment-" + i);
    }
    private async IAsyncEnumerable<string> DownloadCandidates(string url, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var candidate in Candidates(url)) yield return candidate;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "github.com") yield break;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6 || parts[2] != "releases" || parts[3] != "download") yield break;
        string? alternate = null;
        try
        {
            var release = await Json($"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/tags/{parts[4]}", ct);
            var filename = Uri.UnescapeDataString(parts[5]);
            alternate = release["assets"]?.AsArray().FirstOrDefault(a => a.Str("name") == filename).Str("url");
        }
        catch (Exception) when (!ct.IsCancellationRequested) { }
        if (Uri.TryCreate(alternate, UriKind.Absolute, out var asset) && asset.Scheme == "https" && asset.Host == "api.github.com")
            yield return asset.AbsoluteUri;
    }
    public static async Task<bool> Valid(string path, PlannedFile file, CancellationToken ct = default)
    {
        if (file.Size > 0 && new FileInfo(path).Length != file.Size) return false;
        if (string.IsNullOrWhiteSpace(file.Hash)) return false;
        await using var stream = File.OpenRead(path);
        using HashAlgorithm algorithm = file.HashAlgorithm.ToUpperInvariant() switch
        {
            "SHA512" => SHA512.Create(), "SHA256" => SHA256.Create(), _ => SHA1.Create()
        };
        var actual = Convert.ToHexString(await algorithm.ComputeHashAsync(stream, ct));
        return actual.Equals(file.Hash, StringComparison.OrdinalIgnoreCase);
    }
    public static string Friendly(Exception? ex) => ex switch
    {
        OperationCanceledException => "请求超时，请重试或切换来源。",
        HttpRequestException h when h.StatusCode == HttpStatusCode.TooManyRequests => "上游请求频率受限，请稍后重试。",
        HttpRequestException { InnerException: System.Security.Authentication.AuthenticationException } => "上游 HTTPS 证书校验失败，请切换来源或稍后重试。",
        _ => ex?.Message ?? "未知网络错误。"
    };
    public void Dispose() => _client.Dispose();
}
