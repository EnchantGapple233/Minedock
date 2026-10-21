using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace MineDock.App;

internal static class ProjectIcons
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim Gate = new(4);
    private static readonly ConcurrentDictionary<string, BitmapSource> Cache = new();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(8)
        }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MineDock/0.1.3 (Windows desktop)");
        return client;
    }
    public static async Task<BitmapSource?> Load(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cdn.modrinth.com") return null;
        if (Cache.TryGetValue(url, out var cached)) return cached;
        var acquired = false;
        try
        {
            await Gate.WaitAsync(ct); acquired = true;
            using var reply = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            reply.EnsureSuccessStatusCode();
            await using var input = await reply.Content.ReadAsStreamAsync(ct);
            using var data = new MemoryStream();
            var buffer = new byte[16384];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (data.Length + read > 4 * 1024 * 1024) return null;
                data.Write(buffer, 0, read);
            }
            var bytes = data.ToArray();
            var image = await Task.Run(() =>
            {
                using var encoded = SKData.CreateCopy(bytes);
                using var codec = SKCodec.Create(encoded);
                if (codec is null || codec.Info.Width is < 1 or > 2048 || codec.Info.Height is < 1 or > 2048) return null;
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap is null) return null;
                using var decoded = SKImage.FromBitmap(bitmap);
                using var png = decoded.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = png.AsStream();
                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.DecodePixelWidth = 96;
                result.StreamSource = stream;
                result.EndInit(); result.Freeze();
                return result;
            }, ct);
            if (image is not null)
            {
                if (Cache.Count > 200) Cache.Clear();
                Cache[url] = image;
            }
            return image;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
        finally { if (acquired) Gate.Release(); }
    }
}
