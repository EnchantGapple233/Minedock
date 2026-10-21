using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MineDock.Core;

public static class LauncherProcess
{
    public static ProcessStartInfo StartInfo(Settings settings, string? javaExecutable = null)
    {
        if (string.IsNullOrWhiteSpace(settings.LauncherExecutable))
            throw new InvalidOperationException("请先关联或安装 PCL / HMCL 启动器。");
        var executable = Path.GetFullPath(settings.LauncherExecutable);
        if (LauncherIntegration.Identify(executable) != settings.Launcher)
            throw new InvalidOperationException("关联文件的启动器类型已改变，请重新关联。");
        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = true
        };
        if (Path.GetExtension(executable).Equals(".jar", StringComparison.OrdinalIgnoreCase))
        {
            javaExecutable ??= FindJava(settings);
            if (javaExecutable is null || !IsSupportedJava(javaExecutable))
                throw new InvalidOperationException("HMCL JAR 需要 Java 17 或更高版本，请安装 Java 或选择 javaw.exe。");
            info.FileName = Path.GetFullPath(javaExecutable);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.ArgumentList.Add("-jar");
            info.ArgumentList.Add(executable);
        }
        return info;
    }

    public static bool IsSupportedJava(string path)
    {
        if (!File.Exists(path) || Path.GetFileName(path).ToLowerInvariant() is not ("java.exe" or "javaw.exe"))
            return false;
        try
        {
            var version = FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "";
            var match = Regex.Match(version, @"^(?:1\.)?(\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out var major) && major >= 17;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    public static string? FindJava(Settings settings)
    {
        IEnumerable<string> Candidates()
        {
            if (settings.LauncherJavaExecutable.Length > 0) yield return settings.LauncherJavaExecutable;
            foreach (var variable in new[] { "JAVA_HOME", "JRE_HOME" })
            {
                var home = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(home)) continue;
                yield return Path.Combine(home, "bin", "javaw.exe");
                yield return Path.Combine(home, "bin", "java.exe");
            }
            if (Directory.Exists(settings.JavaDirectory))
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 4 };
                foreach (var name in new[] { "javaw.exe", "java.exe" })
                    foreach (var path in Directory.EnumerateFiles(settings.JavaDirectory, name, options).OrderDescending())
                        yield return path;
            }
            foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var clean = folder.Trim().Trim('"');
                yield return Path.Combine(clean, "javaw.exe");
                yield return Path.Combine(clean, "java.exe");
            }
        }
        return Candidates().Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(IsSupportedJava);
    }
}
