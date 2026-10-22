using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MineDock.Core;

public sealed record AppUpdate(Version Version, string Tag, string Notes, PlannedFile File);
public sealed record UpdateRequest(string Target, string Payload, string Hash, long Size, string OriginalHash,
    int ParentId, long ParentStartTicks, string ReadyFile, string Backup, string ResultFile);

public sealed class UpdateService(Network network)
{
    public const string CurrentVersion = "0.1.4";
    public const string Repository = "https://github.com/EnchantGapple233/Minedock";
    public const string LatestApi = "https://api.github.com/repos/EnchantGapple233/Minedock/releases/latest";
    public async Task<AppUpdate?> Check(Version current, CancellationToken ct = default)
    {
        var data = await network.Json(LatestApi, ct);
        var update = Parse(data, current);
        if (update is null) return null;
        if (update.File.Hash.Length == 0)
        {
            var checksum = data["assets"]?.AsArray().FirstOrDefault(a => a.Str("name") == "SHA256SUMS.txt")
                ?? throw new InvalidDataException("更新缺少 SHA-256 校验文件。");
            var url = ValidateAssetUrl(checksum.Str("browser_download_url"), update.Tag);
            var text = await network.Text(url, ct);
            update.File.Hash = ParseChecksum(text);
        }
        return update;
    }
    public static AppUpdate? Parse(JsonNode data, Version current)
    {
        if (data.Bool("draft") || data.Bool("prerelease")) return null;
        var tag = data.Str("tag_name");
        if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+(?:\.\d+)?$") ||
            !Version.TryParse(tag.TrimStart('v'), out var version) || Normalize(version) <= Normalize(current)) return null;
        var asset = data["assets"]?.AsArray().FirstOrDefault(a => a.Str("name") == "MineDock.exe")
            ?? throw new InvalidDataException("新版本未提供 Windows x64 单 EXE。");
        if (asset.Str("state") != "uploaded" || asset.Num("size") <= 0)
            throw new InvalidDataException("新版本的程序尚未上传完整。");
        var hash = asset.Str("digest");
        if (hash.Length > 0 && !Regex.IsMatch(hash, "^sha256:[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("更新 SHA-256 格式不正确。");
        return new(version, tag, data.Str("body"), new PlannedFile
        {
            Url = ValidateAssetUrl(asset.Str("browser_download_url"), tag),
            Size = asset.Num("size"), Hash = hash.Length == 0 ? "" : hash[7..],
            HashAlgorithm = "SHA256", Label = "MineDock " + version
        });
    }
    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    public static string ValidateAssetUrl(string url, string tag)
    {
        var prefix = Repository + "/releases/download/" + Uri.EscapeDataString(tag) + "/";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal) || uri.Query.Length > 0 ||
            uri.Fragment.Length > 0 || uri.AbsolutePath[prefix.Replace("https://github.com", "").Length..].Contains('/'))
            throw new InvalidDataException("更新文件不属于指定仓库的发行版本。");
        return uri.AbsoluteUri;
    }
    public static string ParseChecksum(string text)
    {
        var matches = text.Split('\n').Select(line => Regex.Match(line.Trim(),
            @"^([a-fA-F0-9]{64})\s+\*?MineDock\.exe$")).Where(m => m.Success).ToList();
        if (matches.Count != 1) throw new InvalidDataException("校验文件中没有唯一的 MineDock.exe SHA-256。");
        return matches[0].Groups[1].Value;
    }
    public async Task<string> Download(AppUpdate update, CancellationToken ct = default)
    {
        await network.Download(update.File, Path.Combine(Settings.DataDirectory, "updates", "cache"), null, ct);
        return update.File.CachePath;
    }
    public static async Task<string> CreateRequest(string target, AppUpdate update, string payload, CancellationToken ct = default)
    {
        if (!await Network.Valid(payload, update.File, ct)) throw new InvalidDataException("更新文件校验失败。");
        target = Path.GetFullPath(target);
        var directory = Path.Combine(Settings.DataDirectory, "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        // Verify write access before asking the running application to exit.
        var probe = target + ".minedock-probe-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllTextAsync(probe, "", ct); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
        using var process = Process.GetCurrentProcess();
        await using var input = File.OpenRead(target);
        var originalHash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
        var ready = Path.Combine(directory, "ready");
        var request = new UpdateRequest(target, Path.GetFullPath(payload), update.File.Hash, update.File.Size,
            originalHash, process.Id, process.StartTime.ToUniversalTime().Ticks, ready,
            target + ".minedock-" + Guid.NewGuid().ToString("N") + ".bak", Path.Combine(directory, "result.json"));
        var path = Path.Combine(directory, "request.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request, JsonTools.Options), ct);
        return path;
    }
    public static async Task Replace(UpdateRequest request, CancellationToken ct = default)
    {
        if (!Path.IsPathFullyQualified(request.Target) || !Path.GetExtension(request.Target).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetDirectoryName(request.Backup) != Path.GetDirectoryName(request.Target))
            throw new InvalidDataException("无效的更新目标。");
        if (!File.Exists(request.Target) || !await Network.Valid(request.Target, new()
            { Hash=request.OriginalHash, HashAlgorithm="SHA256" }, ct))
            throw new IOException("当前程序已改变，已停止替换。");
        if (!await Network.Valid(request.Payload, new() { Hash=request.Hash, HashAlgorithm="SHA256", Size=request.Size }, ct))
            throw new InvalidDataException("更新文件校验失败，保留当前程序。");
        var incoming = request.Target + ".incoming-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(request.Payload, incoming);
            File.Replace(incoming, request.Target, request.Backup);
        }
        finally { if (File.Exists(incoming)) File.Delete(incoming); }
    }
    public static async Task Rollback(UpdateRequest request)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!File.Exists(request.Backup) || !await Network.Valid(request.Backup, new()
                { Hash=request.OriginalHash, HashAlgorithm="SHA256" }))
                throw new IOException("更新备份校验失败。");
            if (!await Network.Valid(request.Target, new() { Hash=request.Hash, HashAlgorithm="SHA256" }))
                throw new IOException("更新后程序已被其他进程修改，保留现状与备份。");
            try { File.Replace(request.Backup, request.Target, null); return; }
            catch (IOException) when (attempt < 15) { await Task.Delay(500); }
        }
    }
    public static async Task<int> Apply(string requestPath)
    {
        var request = JsonSerializer.Deserialize<UpdateRequest>(await File.ReadAllTextAsync(requestPath), JsonTools.Options)
            ?? throw new InvalidDataException("更新请求为空。");
        var replaced = false;
        Process? child = null;
        try
        {
            try
            {
                using var parent = Process.GetProcessById(request.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartTicks)
                    throw new IOException("原程序进程标识发生变化。");
                using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await parent.WaitForExitAsync(deadline.Token);
            }
            catch (ArgumentException) { }
            for (var attempt = 0; ; attempt++)
            {
                try { await Replace(request); break; }
                catch (IOException) when (attempt < 15) { await Task.Delay(1000); }
            }
            replaced = true;
            var info = new ProcessStartInfo(request.Target) { UseShellExecute=false, WorkingDirectory=Path.GetDirectoryName(request.Target)! };
            info.ArgumentList.Add("--update-ready"); info.ArgumentList.Add(request.ReadyFile);
            child = Process.Start(info) ?? throw new IOException("新版本未能启动。");
            var end = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < end && !child.HasExited && !File.Exists(request.ReadyFile)) await Task.Delay(250);
            if (!File.Exists(request.ReadyFile)) throw new IOException("新版本未能正常打开窗口，恢复原版本。");
            await File.WriteAllTextAsync(request.ResultFile, "{\"success\":true}");
            File.Delete(request.Backup);
            return 0;
        }
        catch (Exception ex)
        {
            if (replaced)
            {
                if (child is { HasExited: false }) { child.Kill(true); await child.WaitForExitAsync(); }
                try { await Rollback(request); }
                catch (Exception restore)
                {
                    await File.WriteAllTextAsync(request.ResultFile, JsonSerializer.Serialize(new
                        { success=false, error=ex.Message, recoveryError=restore.Message, backup=request.Backup }));
                    return 1;
                }
            }
            await File.WriteAllTextAsync(request.ResultFile, JsonSerializer.Serialize(new { success=false, error=ex.Message }));
            Process.Start(new ProcessStartInfo(request.Target) { UseShellExecute=true, WorkingDirectory=Path.GetDirectoryName(request.Target)! });
            return 1;
        }
        finally { child?.Dispose(); }
    }
}
