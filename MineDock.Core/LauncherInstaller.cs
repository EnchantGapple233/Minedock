using System.Security.Cryptography;

namespace MineDock.Core;

public sealed record LauncherPackage(LauncherKind Kind, string Version, string FileName, long Size,
    string Sha256, string SourceUrl);
public sealed record LauncherInstallResult(string Executable, bool Reused);

public static class LauncherInstaller
{
    public static async Task<LauncherInstallResult> Install(LauncherPackage package, Stream payload,
        string directory, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("请选择启动器安装目录。");
        var name = JsonTools.SafeName(package.FileName);
        if (!Enum.IsDefined(package.Kind) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || package.Size <= 0 ||
            package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("内置启动器清单无效。");
        var folder = Path.GetFullPath(directory);
        var target = Path.Combine(folder, name);
        if (Directory.Exists(target)) throw new IOException("安装位置存在同名文件夹，请选择其他目录。");
        Directory.CreateDirectory(folder);
        if (File.Exists(target))
        {
            if (!await Matches(target, package, ct))
                throw new IOException($"已有不同的 {name}，未覆盖。请关联现有启动器或选择其他目录。");
            if (LauncherIntegration.Identify(target) != package.Kind)
                throw new InvalidDataException("现有文件的启动器身份不匹配，安装已停止。");
            Directory.CreateDirectory(Path.Combine(folder, ".minecraft"));
            return new(target, true);
        }
        var temporary = Path.Combine(folder, ".minedock-" + Guid.NewGuid().ToString("N") + ".tmp.exe");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await payload.CopyToAsync(output, ct);
            if (!await Matches(temporary, package, ct))
                throw new InvalidDataException("内置启动器大小或 SHA-256 不匹配，安装已停止。");
            if (LauncherIntegration.Identify(temporary) != package.Kind)
                throw new InvalidDataException("内置文件的启动器身份不匹配，安装已停止。");
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(folder, ".minecraft"));
            File.Move(temporary, target);
            return new(target, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task<bool> Matches(string file, LauncherPackage package, CancellationToken ct)
    {
        if (new FileInfo(file).Length != package.Size) return false;
        await using var input = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
        return hash.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
