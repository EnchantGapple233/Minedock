using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MineDock.Core;

public static class LoaderInstaller
{
    public static async Task Prepare(InstallPlan plan, Settings settings, Network network, CancellationToken ct = default)
    {
        if (plan.Loader is not { } job) return;
        var candidates = new List<string>();
        if (settings.LauncherJavaExecutable.Length > 0) candidates.Add(settings.LauncherJavaExecutable);
        foreach (var name in new[] { "JAVA_HOME", "JDK_HOME", "JRE_HOME" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } folder)
                candidates.Add(Path.Combine(folder, "bin", "java.exe"));
        foreach (var directory in new[] { settings.JavaDirectory, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium") })
        {
            if (!Directory.Exists(directory)) continue;
            candidates.AddRange(Directory.EnumerateFiles(directory, "java.exe", new EnumerationOptions
            { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 4 }));
        }
        job.JavaExecutable = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(path =>
        {
            if (!File.Exists(path) || !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "javac.exe"))) return false;
            var version = FileVersionInfo.GetVersionInfo(path).ProductMajorPart;
            return version >= job.MinimumJava;
        }) ?? "";
        if (job.JavaExecutable.Length == 0)
        {
            var service = new JavaService(network);
            var java = service.Plan(await service.Release(job.MinimumJava, "jdk", ct), settings.JavaDirectory);
            foreach (var file in java.Files) plan.Add(file);
            job.JavaExecutable = Path.Combine(java.Destination, "bin", "java.exe");
            plan.Details.Add($"安装引擎需要 JDK {job.MinimumJava}；本计划将安装对应 Temurin JDK，保留游戏自身的 Java 要求。");
        }
        job.EngineDirectory = Path.Combine(Settings.DataDirectory, "loader-engine");
        foreach (var part in new[] { "base", "graphics" })
        {
            var url = $"https://repo.maven.apache.org/maven2/org/openjfx/javafx-{part}/21.0.8/javafx-{part}-21.0.8-win.jar";
            var hash = part == "base" ? "c000208028c983b9ccf85b57c74e0e74f85849f0" : "f3aba10d2c9f767e41831aeed6e1cd5418299c03";
            plan.Add(new() { Url = url, Hash = hash, HashAlgorithm = "SHA1",
                Destination = Path.Combine(job.EngineDirectory, $"javafx-{part}.jar"), Label = "OpenJFX " + part + " 21.0.8",
                Size = part == "base" ? 753858 : 5942320 });
        }
        if (job.Kind == "liteloader" && job.Version.EndsWith("-SNAPSHOT", StringComparison.Ordinal))
        {
            var metadata = $"https://repo.mumfrey.com/content/repositories/snapshots/com/mumfrey/liteloader/{Uri.EscapeDataString(job.Version)}/maven-metadata.xml";
            var url = LiteLoaderSnapshotUrl(job.Version, await network.Text(metadata, ct));
            var hash = (await network.Text(url + ".sha1", ct)).Trim();
            if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{40}$"))
                throw new InvalidDataException("LiteLoader 官方仓库未提供有效校验信息。");
            plan.Add(new() { Url = url, Hash = hash, HashAlgorithm = "SHA1", Label = "LiteLoader " + job.Version,
                Destination = JsonTools.Child(job.Root, $"libraries/com/mumfrey/liteloader/{job.Version}/liteloader-{job.Version}.jar") });
            plan.Details.Add("LiteLoader 快照从其官方 Maven 仓库获取，并使用仓库 SHA-1 校验。");
        }
    }

    public static string LiteLoaderSnapshotUrl(string version, string xml)
    {
        JsonTools.SafeName(version);
        var data = XDocument.Parse(xml).Root ?? throw new InvalidDataException("LiteLoader Maven 元数据为空。");
        var snapshot = data.Element("versioning")?.Element("snapshot");
        var timestamp = snapshot?.Element("timestamp")?.Value ?? "";
        var build = snapshot?.Element("buildNumber")?.Value ?? "";
        if (data.Element("groupId")?.Value != "com.mumfrey" || data.Element("artifactId")?.Value != "liteloader" ||
            data.Element("version")?.Value != version || !version.EndsWith("-SNAPSHOT", StringComparison.Ordinal) ||
            !Regex.IsMatch(timestamp, @"^\d{8}\.\d{6}$") || !Regex.IsMatch(build, @"^\d+$"))
            throw new InvalidDataException("LiteLoader Maven 元数据与所选版本不一致。");
        var resolved = version[..^"-SNAPSHOT".Length] + "-" + timestamp + "-" + build;
        return $"https://repo.mumfrey.com/content/repositories/snapshots/com/mumfrey/liteloader/{version}/liteloader-{resolved}-release.jar";
    }

    public static async Task<List<PlannedFile>> Stage(InstallPlan plan, string staging,
        IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        var job = plan.Loader ?? throw new InvalidOperationException("缺少加载器安装任务。");
        Directory.CreateDirectory(staging);
        // Copy verified inputs, never hard-link files an upstream processor might modify.
        foreach (var file in plan.Files)
        {
            var relative = Path.GetRelativePath(job.Root, file.Destination).Replace('\\', '/');
            if (!relative.StartsWith("libraries/", StringComparison.OrdinalIgnoreCase) &&
                !relative.StartsWith($"versions/{job.Game}/", StringComparison.OrdinalIgnoreCase)) continue;
            var destination = JsonTools.Child(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file.Destination, destination, true);
        }
        var jar = Path.Combine(job.EngineDirectory, "HMCL.jar");
        var source = Path.Combine(job.EngineDirectory, "MineDockBridge.java");
        if (!File.Exists(jar) || !File.Exists(source)) throw new IOException("缺少内置加载器引擎，请重新打开 MineDock 后重试。");
        var info = new ProcessStartInfo(job.JavaExecutable)
        {
            WorkingDirectory = staging, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("-Djava.awt.headless=true");
        info.ArgumentList.Add("-Djava.net.useSystemProxies=true");
        var proxy = HttpClient.DefaultProxy.GetProxy(new Uri("https://bmclapi2.bangbang93.com"));
        if (proxy is not null && proxy.Host != "bmclapi2.bangbang93.com")
            foreach (var protocol in new[] { "http", "https" })
            {
                info.ArgumentList.Add($"-D{protocol}.proxyHost={proxy.Host}");
                info.ArgumentList.Add($"-D{protocol}.proxyPort={proxy.Port}");
            }
        info.ArgumentList.Add("-cp");
        info.ArgumentList.Add(string.Join(Path.PathSeparator, jar, Path.Combine(job.EngineDirectory, "javafx-base.jar"),
            Path.Combine(job.EngineDirectory, "javafx-graphics.jar")));
        info.ArgumentList.Add(source);
        foreach (var arg in new[] { staging, job.Game, job.Kind, job.Version, job.TargetId, plan.Source.ToString() })
            info.ArgumentList.Add(arg);
        progress?.Report(new("安装加载器", LoaderService.Name(job.Kind) + " " + job.Version, 0, 1, 0, 0));
        using var process = Process.Start(info) ?? throw new IOException("加载器引擎未能启动。");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        var log = await output + "\n" + await error;
        var logs = Path.Combine(Settings.DataDirectory, "loader-logs");
        Directory.CreateDirectory(logs);
        await File.WriteAllTextAsync(Path.Combine(logs, plan.Id + ".log"), log, CancellationToken.None);
        if (process.ExitCode != 0) throw new IOException(
            $"加载器安装失败（{process.ExitCode}），本次文件写入将回滚。详情：{Path.Combine(logs, plan.Id + ".log")}");
        var profile = JsonTools.Child(staging, $"versions/{JsonTools.SafeName(job.TargetId)}/{job.TargetId}.json");
        if (!File.Exists(profile)) throw new InvalidDataException("加载器未生成启动配置。");
        var profileData = JsonNode.Parse(await File.ReadAllTextAsync(profile, ct))
            ?? throw new InvalidDataException("加载器启动配置为空。");
        profileData["clientVersion"] = job.Game;
        await File.WriteAllTextAsync(profile, profileData.ToJsonString(JsonTools.Options), ct);
        var files = new List<PlannedFile>();
        foreach (var folder in new[] { JsonTools.Child(staging, "libraries"), Path.GetDirectoryName(profile)! })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("加载器输出包含符号链接。");
                var relative = Path.GetRelativePath(staging, path);
                var destination = JsonTools.Child(job.Root, relative);
                await using var stream = File.OpenRead(path);
                var file = new PlannedFile { Action = FileAction.Local, CachePath = path, Destination = destination,
                    Hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)), HashAlgorithm = "SHA256",
                    Size = stream.Length, Label = LoaderService.Name(job.Kind) + " · " + Path.GetFileName(path) };
                if (File.Exists(destination) && await Network.Valid(destination, file, ct)) continue;
                files.Add(file);
            }
        }
        return files;
    }
}
