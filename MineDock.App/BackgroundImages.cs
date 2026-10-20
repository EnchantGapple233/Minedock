using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using MineDock.Core;
using SkiaSharp;

namespace MineDock.App;

internal static class BackgroundImages
{
    internal sealed record Imported(string File, string Name, BitmapSource Image);
    private static string CacheDirectory => Path.Combine(Settings.DataDirectory, "appearance");
    private const long MaximumBytes = 64L * 1024 * 1024;

    public static Imported Import(string source)
    {
        using var input = File.OpenRead(source);
        if (input.Length is < 1 or > MaximumBytes)
            throw new InvalidDataException("请选择不超过 64 MB 的图片。");
        using var data = SKData.Create(input);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg
            or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
            throw new InvalidDataException("图片无法识别，请选择 PNG、JPEG、WebP 或 BMP 图片。");
        if (codec.Info.Width < 1 || codec.Info.Height < 1 || codec.Info.Width > 16384 || codec.Info.Height > 16384 ||
            (long)codec.Info.Width * codec.Info.Height > 40_000_000)
            throw new InvalidDataException("图片尺寸过大，请使用不超过 4000 万像素的图片。");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("图片已损坏，无法读取。");
        var scale = Math.Min(1, 2560d / Math.Max(bitmap.Width, bitmap.Height));
        using var resized = scale < 1
            ? bitmap.Resize(new SKImageInfo(Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale))),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            : null;
        using var image = SKImage.FromBitmap(resized ?? bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("图片处理失败，请更换图片。");
        var bytes = encoded.ToArray();
        var result = Decode(bytes);
        Directory.CreateDirectory(CacheDirectory);
        var name = "background-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".png";
        var path = CachePath(name);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new(name, Path.GetFileName(source), result);
    }

    public static BitmapSource? Load(string file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        var path = CachePath(file);
        using var input = File.OpenRead(path);
        if (input.Length is < 1 or > MaximumBytes) throw new InvalidDataException("背景缓存无法读取，请重新选择图片。");
        using var data = new MemoryStream();
        input.CopyTo(data);
        return Decode(data.ToArray());
    }

    private static string CachePath(string file)
    {
        if (file.Length != 79 || !file.StartsWith("background-", StringComparison.Ordinal) ||
            !file.EndsWith(".png", StringComparison.Ordinal) || file[11..75].Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("背景缓存名称无效，请重新选择图片。");
        return Path.Combine(CacheDirectory, file);
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width is < 1 or > 2560 || codec.Info.Height is < 1 or > 2560)
            throw new InvalidDataException("背景缓存已损坏，请重新选择图片。");
        using var stream = new MemoryStream(bytes, false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
