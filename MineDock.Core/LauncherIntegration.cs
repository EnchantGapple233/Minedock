using System.Diagnostics;
using System.IO.Compression;

namespace MineDock.Core;

public static class LauncherIntegration
{
    public static LauncherKind Identify(string executable)
    {
        if (!File.Exists(executable)) throw new InvalidOperationException("启动器文件不存在。");
        if (Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var info = FileVersionInfo.GetVersionInfo(executable);
            var identity = (info.FileDescription ?? "") + " " + (info.ProductName ?? "");
            if (identity.Contains("Kingsoft", StringComparison.OrdinalIgnoreCase) ||
                identity.Contains("Internet Security", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("所选文件是金山安全安装程序，请选择实际的 PCL 或 HMCL 启动器。");
            if (identity.Contains("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) ||
                identity.Contains("PCL", StringComparison.OrdinalIgnoreCase)) return LauncherKind.Pcl;
            if (identity.Contains("HMCL", StringComparison.OrdinalIgnoreCase) ||
                identity.Contains("Hello Minecraft", StringComparison.OrdinalIgnoreCase)) return LauncherKind.Hmcl;
        }
        // HMCL's JAR and some EXE wrappers carry their identity in the JAR manifest.
        try
        {
            using var zip = ZipFile.OpenRead(executable);
            var manifest = zip.GetEntry("META-INF/MANIFEST.MF");
            if (manifest is { Length: < 65536 })
            {
                using var reader = new StreamReader(manifest.Open());
                var text = reader.ReadToEnd().Replace("\r\n ", "").Replace("\n ", "");
                if (text.Split('\n').Any(line => line.Trim().StartsWith(
                    "Main-Class: org.jackhuang.hmcl.", StringComparison.Ordinal)))
                    return LauncherKind.Hmcl;
            }
        }
        catch (InvalidDataException) { }
        throw new InvalidOperationException("无法识别所选文件，请选择实际的 PCL.exe、HMCL.exe 或 HMCL.jar。");
    }

    public static string? FindMinecraftRoot(string executable, LauncherKind launcher)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (launcher == LauncherKind.Hmcl)
        {
            var configured = HmclIntegration.FindMinecraftRoot(executable);
            if (configured is not null) return configured;
        }
        else
        {
            var setup = PclIntegration.ReadIni(Path.Combine(folder, "PCL", "Setup.ini"));
            if (setup.TryGetValue("LaunchFolderSelect", out var selected) && !string.IsNullOrWhiteSpace(selected))
            {
                var configured = Path.GetFullPath(selected.Replace("$",
                    Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar), folder);
                if (!Directory.Exists(configured))
                    throw new InvalidOperationException("PCL 选中的游戏目录不存在，请先在 PCL 检查目录设置。");
                return configured;
            }
        }
        var sibling = Path.Combine(folder, ".minecraft");
        return Directory.Exists(sibling) ? sibling : null;
    }

    public static (string Directory, bool Isolated, string? Error) Resolve(
        Settings settings, string id, string loader, string type = "release")
        => settings.Launcher == LauncherKind.Hmcl
            ? HmclIntegration.Resolve(settings.MinecraftRoot, id, settings.HmclExecutable)
            : PclIntegration.Resolve(settings.MinecraftRoot, id, settings.PclExecutable, loader, type);

    public static List<Instance> Discover(Settings settings)
        => PclIntegration.Discover(settings.MinecraftRoot,
            (id, loader, type) => Resolve(settings, id, loader, type));
}
