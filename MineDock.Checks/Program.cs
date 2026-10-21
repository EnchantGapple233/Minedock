using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MineDock.Core;

var testRoot = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? Path.Combine(Path.GetTempPath(), "MineDock-checks-" + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(testRoot);
Environment.SetEnvironmentVariable("MINEDOCK_DATA", Path.Combine(testRoot, "state"));
HttpClient.DefaultProxy = new LoopbackBypassProxy(HttpClient.DefaultProxy);
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks.Add(name); Console.WriteLine("PASS: " + name);
}
void Reject(Action action, string name)
{
    try { action(); } catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException) { Check(true, name); return; }
    throw new Exception("FAIL: " + name);
}
var root = Path.Combine(testRoot, ".minecraft");
Directory.CreateDirectory(root);
Reject(() => JsonTools.Child(root, "../outside.txt"), "archive traversal rejected");
Reject(() => JsonTools.Child(root, "C:\\outside.txt"), "absolute path rejected");
Reject(() => JsonTools.SafeName("a/b"), "invalid instance name rejected");
Reject(() => JsonTools.SafeName("con.txt"), "Windows reserved filename rejected");
Check(JsonTools.Child(root, "mods/example.jar").StartsWith(root, StringComparison.OrdinalIgnoreCase), "safe child accepted");
var pcl = Path.Combine(testRoot, "pcl", "PCL.exe");
Directory.CreateDirectory(Path.GetDirectoryName(pcl)!);
File.WriteAllText(pcl, "");
var globalIni = Path.Combine(testRoot, "pcl", "PCL", "Setup.ini");
Directory.CreateDirectory(Path.GetDirectoryName(globalIni)!);
for (var mode = 0; mode <= 4; mode++)
{
    File.WriteAllText(globalIni, "LaunchArgumentIndieV2:" + mode);
    Check(PclIntegration.Resolve(root, "new-fabric", pcl, "fabric").Isolated == (mode is 1 or 3 or 4), "PCL global mode " + mode + " fabric");
    Check(PclIntegration.Resolve(root, "new-vanilla", pcl, "vanilla").Isolated == (mode == 4), "PCL global mode " + mode + " vanilla");
}
var localIni = Path.Combine(root, "versions", "custom", "PCL", "Setup.ini");
Directory.CreateDirectory(Path.GetDirectoryName(localIni)!);
File.WriteAllText(localIni, "VersionArgumentIndieV2:False");
var beforeIni = File.ReadAllBytes(localIni);
Check(!PclIntegration.Resolve(root, "custom", pcl, "fabric").Isolated, "saved PCL version setting overrides global");
Check(beforeIni.SequenceEqual(File.ReadAllBytes(localIni)), "PCL settings remain untouched");
File.WriteAllText(localIni, "VersionArgumentIndie:1");
Check(PclIntegration.Resolve(root, "custom", pcl, "vanilla").Isolated, "legacy PCL setting respected without migration");
Check(PclIntegration.Resolve(root, "unknown", "", "fabric").Error is not null, "unknown directory is blocked");
File.WriteAllText(globalIni, "LaunchArgumentIndie:0");
Check(PclIntegration.Resolve(root, "old-global", pcl, "fabric").Error is not null, "ambiguous legacy global isolation cannot silently choose a directory");
File.WriteAllText(globalIni, "LaunchArgumentIndieV2:999");
Check(PclIntegration.Resolve(root, "unknown-policy", pcl, "fabric").Error is not null, "unknown launcher isolation policy is blocked");
File.WriteAllText(globalIni, "LaunchArgumentIndieV2:4");
var renamedFolder = Path.Combine(root, "versions", "renamed");
Directory.CreateDirectory(renamedFolder);
File.WriteAllText(Path.Combine(renamedFolder, "renamed.json"), """{"id":"renamed","clientVersion":"1.21.1","type":"release"}""");
Check(PclIntegration.Discover(root, pcl).Single(i => i.Id == "renamed").GameVersion == "1.21.1", "renamed PCL instance preserves actual game version");
var hmclFlag = Array.IndexOf(args, "--hmcl-exe");
LauncherChecks.Run(Path.Combine(testRoot, "launchers"), Check, Reject,
    hmclFlag >= 0 && hmclFlag + 1 < args.Length ? Path.GetFullPath(args[hmclFlag + 1]) : null);
using var network = new Network { Source = SourceMode.Auto };
Check(network.Candidates("https://resources.download.minecraft.net/aa/hash").Count() == 2, "automatic source has official fallback");
network.Source = SourceMode.Bmclapi;
Check(network.Candidates("https://libraries.minecraft.net/a.jar").Single().Contains("bmclapi2"), "locked mirror never silently changes source");
Check(Network.Mirror("https://libraries.minecraft.net/a.jar") == "https://bmclapi2.bangbang93.com/maven/a.jar", "BMCLAPI libraries use Maven prefix");
Check(Network.Mirror("https://resources.download.minecraft.net/aa/hash") == "https://bmclapi2.bangbang93.com/assets/aa/hash", "BMCLAPI assets use asset prefix");
Check(network.Candidates("https://cdn.modrinth.com/data/a.jar").Single().Contains("cdn.modrinth.com"), "Modrinth files keep original source");
Check(GameService.MavenPath("net.fabricmc:fabric-loader:0.16.0") == "net/fabricmc/fabric-loader/0.16.0/fabric-loader-0.16.0.jar", "Maven coordinate conversion");
Check(GameService.MavenPath("a.b:c:1:natives-windows@zip") == "a/b/c/1/c-1-natives-windows.zip", "Maven classifier and extension");
Check(GameService.Allowed(JsonNode.Parse("""{"rules":[{"action":"allow","os":{"name":"windows"}}]}""")), "Windows library rules");
Check(!GameService.Allowed(JsonNode.Parse("""{"rules":[{"action":"allow","os":{"name":"linux"}}]}""")), "Linux libraries excluded");
Check(!GameService.Allowed(JsonNode.Parse("""{"rules":[{"action":"allow","os":{"name":"windows","arch":"x86"}}]}""")), "x86-specific libraries excluded");
var engine = new InstallEngine(network) { CacheDirectory = Path.Combine(testRoot, "cache") };
var existing = Path.Combine(testRoot, "existing.txt");
File.WriteAllText(existing, "original");
var blocker = Path.Combine(testRoot, "blocker"); Directory.CreateDirectory(blocker);
var rollback = new InstallPlan { Title = "rollback-test", Destination = testRoot };
rollback.Add(new() { Action = FileAction.Text, Destination = existing, Text = "changed", Label = "changed" });
rollback.Add(new() { Action = FileAction.Text, Destination = blocker, Text = "will fail", Label = "blocked" });
try { await engine.Execute(rollback, null, default); throw new Exception("Expected transaction failure"); }
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
Check(File.ReadAllText(existing) == "original", "existing data restored after commit failure");
Check(Directory.Exists(blocker), "unrelated directory preserved during rollback");
var success = new InstallPlan { Title = "success-test", Destination = testRoot };
success.Add(new() { Action = FileAction.Text, Destination = existing, Text = "installed", Label = "file" });
await engine.Execute(success, null, default);
Check(File.ReadAllText(existing) == "installed", "successful file commit");
Check(InstallEngine.History().Any(h => h.Id == success.Id && h.Status == "已完成"), "successful installation receipt");
using (var cancel = new CancellationTokenSource())
{
    var canceled = new InstallPlan { Title = "cancel-test", Destination = testRoot };
    canceled.Add(new() { Action = FileAction.Text, Destination = existing, Text = "canceled content", Label = "first" });
    canceled.Add(new() { Action = FileAction.Text, Destination = Path.Combine(testRoot, "should-not-exist"), Text = "second", Label = "second" });
    var progress = new InlineProgress(p => { if (p.Phase == "安装文件" && p.Done == 1) cancel.Cancel(); });
    try { await engine.Execute(canceled, progress, cancel.Token); } catch (OperationCanceledException) { }
    Check(File.ReadAllText(existing) == "installed", "cancellation rolls back already written files");
    Check(!File.Exists(Path.Combine(testRoot, "should-not-exist")), "cancellation leaves no partial second file");
}
var crashed = Path.Combine(Settings.DataDirectory, "transactions", "crashed");
Directory.CreateDirectory(crashed);
File.WriteAllText(Path.Combine(crashed, "original"), "pre-crash");
File.WriteAllText(existing, "partial installation");
File.WriteAllText(Path.Combine(crashed, "journal.json"), JsonSerializer.Serialize(new InstallEngine.JournalItem(existing,
    Path.Combine(crashed, "original"), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("partial installation"))))) + "\n");
Check(InstallEngine.Recover().Any(m => m.EndsWith("crashed", StringComparison.Ordinal)) &&
    File.ReadAllText(existing) == "pre-crash", "interrupted transaction recovery");
var externalChange = new InstallPlan { Title = "external-change-test", Destination = testRoot };
externalChange.Add(new() { Action = FileAction.Text, Destination = existing, Text = "temporary installed text", Label = "first" });
externalChange.Add(new() { Action = FileAction.Text, Destination = blocker, Text = "blocked", Label = "second" });
try
{
    await engine.Execute(externalChange, new InlineProgress(p =>
    {
        if (p.Phase == "安装文件" && p.Done == 1) File.WriteAllText(existing, "external edit");
    }), default);
    throw new Exception("Expected protected rollback");
}
catch (IOException) { }
Check(File.ReadAllText(existing) == "external edit" &&
    Directory.Exists(Path.Combine(Settings.DataDirectory, "transactions", externalChange.Id)), "rollback protects third-party edits and retains backup");
File.WriteAllText(existing, "temporary installed text");
InstallEngine.Recover();
Check(File.ReadAllText(existing) == "pre-crash", "retained backup can recover after external conflict is resolved");
using (var server = new LocalServer())
{
    var payload = Encoding.UTF8.GetBytes("verified payload");
    server.Body = payload;
    network.Source = SourceMode.Official;
    var download = new InstallPlan { Title = "download-test", Destination = testRoot };
    var file = new PlannedFile { Url = server.Url + "file", Destination = Path.Combine(testRoot, "download.bin"), Hash = Convert.ToHexString(SHA256.HashData(payload)), HashAlgorithm = "SHA256", Size = payload.Length, Label = "test file" };
    try
    {
        await network.Download(new() { Url = server.Url + "unverified", Label = "unverified" }, engine.CacheDirectory, null, default);
        throw new Exception("Expected missing checksum rejection");
    }
    catch (InvalidDataException) { Check(true, "unverified remote files are rejected before download"); }
    download.Add(file);
    await engine.Execute(download, null, default);
    Check(File.ReadAllBytes(file.Destination).SequenceEqual(payload), "download is hash-verified and committed");
    var requests = server.Requests;
    File.Delete(file.Destination);
    await engine.Execute(download, null, default);
    Check(server.Requests == requests, "verified cache is reused");
    file.Hash = new string('0', 64);
    File.WriteAllText(file.Destination, "keep this");
    try { await engine.Execute(download, null, default); throw new Exception("Expected hash rejection"); }
    catch (IOException) { }
    Check(File.ReadAllText(file.Destination) == "keep this", "hash mismatch preserves existing destination");
    using var zipData = new MemoryStream();
    using (var zip = new ZipArchive(zipData, ZipArchiveMode.Create, true))
    {
        var entry = zip.CreateEntry("jdk/../../escape");
        using var writer = new StreamWriter(entry.Open()); writer.Write("escape");
    }
    server.Body = zipData.ToArray();
    var javaTarget = Path.Combine(testRoot, "bad-java");
    var badJava = new InstallPlan { Title = "unsafe-java-test", Destination = javaTarget };
    badJava.Add(new() { Action = FileAction.ExtractJava, Url = server.Url + "bad-java.zip", Destination = javaTarget,
        Hash = Convert.ToHexString(SHA256.HashData(server.Body)), HashAlgorithm = "SHA256", Size = server.Body.Length, Label = "unsafe archive" });
    try { await engine.Execute(badJava, null, default); throw new Exception("Expected unsafe archive rejection"); }
    catch (InvalidOperationException) { }
    Check(!Directory.Exists(javaTarget) && !File.Exists(Path.Combine(testRoot, "escape")), "unsafe Java archive cannot escape or partially install");
    server.Body = new byte[17 * 1024 * 1024];
    new Random(42).NextBytes(server.Body);
    server.RangeSupport = true;
    var segmented = new PlannedFile { Url = server.Url + "large.bin", Label = "large ranged file", Size = server.Body.Length,
        Hash = Convert.ToHexString(SHA256.HashData(server.Body)), HashAlgorithm = "SHA256" };
    await network.Download(segmented, engine.CacheDirectory, null, default);
    Check(server.RangeRequests > 1 && await Network.Valid(segmented.CachePath, segmented), "parallel segments merge into a fully verified file");
    server.IgnoreRanges = true;
    segmented.Url = server.Url + "range-ignored.bin";
    await network.Download(segmented, engine.CacheDirectory, null, default);
    Check(await Network.Valid(segmented.CachePath, segmented), "servers ignoring ranges fall back to ordinary verified download");
    server.IgnoreRanges = false;
    server.CutOneRange = 1;
    segmented.Url = server.Url + "range-resume.bin";
    await network.Download(segmented, engine.CacheDirectory, null, default);
    Check(server.RangeResumes > 0 && await Network.Valid(segmented.CachePath, segmented), "interrupted segments resume and final hash is verified");
}
if (args.Contains("--live"))
{
    network.Source = SourceMode.Official;
    var game = new GameService(network);
    var releases = await game.Releases();
    Check(releases.Count > 50 && releases.Any(r => r.Id == "1.21.1"), "live Mojang release manifest");
    Check((await game.Metadata("1.21.1"))["javaVersion"]!.Num("majorVersion") == 21, "live Minecraft Java requirement");
    var loaders = await game.Loaders("1.21.1");
    var loader = loaders.First(l => l.Stable);
    var targetId = "MineDock-live-test";
    for (var suffix = 2; Directory.Exists(Path.Combine(root, "versions", targetId)); suffix++) targetId = "MineDock-live-test-" + suffix;
    var plan = await game.Plan(root, "1.21.1", loader.Version, targetId);
    Check(plan.Files.Count > 1000 && plan.Files.Any(f => f.Label == "Minecraft 客户端"), "live full game and Fabric installation plan");
    Check(plan.Files.Any(f => f.Destination.EndsWith(targetId + ".json") && f.Text.Contains("inheritsFrom")), "Fabric profile inherits installed base");
    Check(plan.Files.Any(f => f.Label == "日志配置"), "complete game plan includes logging configuration");
    Check(plan.Files.Where(f => f.Action == FileAction.Download).All(f => f.Hash.Length > 0), "every game and Fabric download has upstream checksum");
    var renamedPlan = await game.Plan(root, "1.21.1", null, "renamed-vanilla-test");
    var renamedMetadata = JsonNode.Parse(renamedPlan.Files.First(f => f.Label == "游戏版本配置").Text);
    Check(renamedMetadata.Str("id") == "renamed-vanilla-test" && renamedMetadata.Str("clientVersion") == "1.21.1", "custom vanilla profile retains original game identity");
    if (args.Contains("--mirror"))
    {
        network.Source = SourceMode.Bmclapi;
        var mirrorManifest = await network.Json(GameService.ManifestUrl);
        Check(mirrorManifest["versions"]!.AsArray().Count > 50, "live locked BMCLAPI manifest");
        var sample = plan.Files.First(f => f.Action == FileAction.Download && f.Url.StartsWith("https://resources.download.minecraft.net/") && f.Size is > 100 and < 1000);
        var mirrorFile = new PlannedFile { Url = sample.Url, Hash = sample.Hash, HashAlgorithm = sample.HashAlgorithm, Size = sample.Size, Label = "镜像资源校验" };
        await network.Download(mirrorFile, Path.Combine(testRoot, "mirror-cache"), null, default);
        Check(await Network.Valid(mirrorFile.CachePath, mirrorFile), "live locked BMCLAPI asset download matches Mojang checksum");
        network.Source = SourceMode.Official;
    }
    var content = new ContentService(network);
    var tags = await content.Categories();
    Check(tags.Any(t => t.ProjectType == "shader" && t.Header == "performance impact"), "live shader-specific filters");
    var result = await content.Search("mod", "sodium", "1.21.1", "fabric", "", "relevance", 0);
    Check(result.Projects.Any(p => p.Slug == "sodium"), "live filtered Modrinth search");
    var versions = await content.Versions("fabric-api", "1.21.1", "fabric");
    Check(versions.Any(v => v.VersionType == "release" && v.Files.Any(f => f.Hash.Length == 128)), "live Fabric API file hashes");
    var apiPlan = await content.Plan(versions.First(v => v.VersionType == "release"),
        new Instance("test", "1.21.1", "fabric", Path.Combine(testRoot, "live-instance"), true, null), "mod");
    Check(apiPlan.Files.Count > 0 && apiPlan.Details.Any(d => d.Contains("Fabric API")), "live visible dependency installation plan");
    var irisVersion = (await content.Versions("iris", "1.21.1", "fabric")).First(v => v.VersionType == "release");
    var irisPlan = await content.Plan(irisVersion,
        new Instance("dependency-test", "1.21.1", "fabric", Path.Combine(testRoot, "dependency-instance"), true, null), "mod");
    Check(irisPlan.Files.Any(f => f.Label.StartsWith("Iris")) && irisPlan.Files.Any(f => f.Label.StartsWith("Sodium")),
        "live Iris installation resolves and displays required Sodium dependency");
    File.WriteAllText(Path.Combine(testRoot, "dependency-plan.json"), JsonSerializer.Serialize(irisPlan, JsonTools.Options));
    var java = await new JavaService(network).Release(21, "jre");
    Check(java.File.Algorithm == "SHA256" && java.File.Hash.Length == 64 && java.File.Filename.EndsWith(".zip"), "live verified Temurin Windows ZIP");
    if (args.Contains("--install"))
    {
        var lastReport = 0L;
        var progress = new InlineProgress(p =>
        {
            if (Environment.TickCount64 - lastReport < 5000 && p.Done < p.Total) return;
            lastReport = Environment.TickCount64;
            Console.WriteLine($"{p.Phase}: {p.Done}/{p.Total} · {JsonTools.Size(p.Bytes)}");
        });
        Console.WriteLine($"Installing real Minecraft and Fabric: {plan.Files.Count} files, {JsonTools.Size(plan.Bytes)}");
        await engine.Execute(plan, progress, default);
        Check(plan.Files.All(f => File.Exists(f.Destination)), "real Minecraft and Fabric complete file installation");
        var actualInstance = PclIntegration.Discover(root, pcl).Single(i => i.Id == plan.TargetId);
        Check(actualInstance.GameVersion == "1.21.1" && actualInstance.Loader == "fabric", "installed Fabric profile is rediscovered with correct identity");
        var actualApiPlan = await content.Plan(versions.First(v => v.VersionType == "release"), actualInstance, "mod");
        await engine.Execute(actualApiPlan, progress, default);
        using (var apiArchive = ZipFile.OpenRead(actualApiPlan.Files.First().Destination))
        using (var metadataStream = apiArchive.GetEntry("fabric.mod.json")!.Open())
            Check(JsonNode.Parse(metadataStream).Str("id") == "fabric-api", "real verified Fabric API JAR installs and contains expected metadata");
        var javaPlan = new JavaService(network).Plan(java, Path.Combine(testRoot, "runtime"));
        await engine.Execute(javaPlan, progress, default);
        using var javaProcess = Process.Start(new ProcessStartInfo(Path.Combine(javaPlan.Destination, "bin", "java.exe"), "-version")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var javaOutput = await javaProcess.StandardError.ReadToEndAsync();
        await javaProcess.WaitForExitAsync();
        Check(javaProcess.ExitCode == 0 && javaOutput.Contains("21."), "real Temurin JRE installs and java.exe reports Java 21");
        await File.WriteAllTextAsync(Path.Combine(testRoot, "installation-evidence.json"), JsonSerializer.Serialize(new
        {
            MinecraftFiles = plan.Files.Count, MinecraftBytes = plan.Bytes, GameRoot = root,
            ModFiles = actualApiPlan.Files.Select(f => f.Destination).ToArray(), JavaOutput = javaOutput,
            JavaDirectory = javaPlan.Destination, PclExecutableTested = false
        }, JsonTools.Options));
    }
}
Console.WriteLine($"All {checks.Count} checks passed.");
File.WriteAllText(Path.Combine(testRoot, "results.json"), JsonSerializer.Serialize(new { Passed = checks.Count, Checks = checks }, JsonTools.Options));

sealed class InlineProgress(Action<TransferProgress> report) : IProgress<TransferProgress>
{ public void Report(TransferProgress value) => report(value); }
sealed class LoopbackBypassProxy(IWebProxy upstream) : IWebProxy
{
    public ICredentials? Credentials { get => upstream.Credentials; set => upstream.Credentials = value; }
    public Uri? GetProxy(Uri destination) => destination.IsLoopback ? destination : upstream.GetProxy(destination);
    public bool IsBypassed(Uri host) => host.IsLoopback || upstream.IsBypassed(host);
}
sealed class LocalServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancel = new();
    public byte[] Body { get; set; } = [];
    public int Requests;
    public int RangeRequests;
    public bool RangeSupport { get; set; }
    public bool IgnoreRanges { get; set; }
    public int CutOneRange;
    public int RangeResumes;
    public string Url { get; }
    public LocalServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        Url = $"http://127.0.0.1:{port}/"; _listener.Prefixes.Add(Url); _listener.Start();
        _ = Task.Run(async () =>
        {
            while (!_cancel.IsCancellationRequested)
            {
                try
                {
                    var request = await _listener.GetContextAsync();
                    Interlocked.Increment(ref Requests);
                    var body = Body;
                    var start = 0;
                    var length = body.Length;
                    var isRange = false;
                    if (RangeSupport) request.Response.AddHeader("Accept-Ranges", "bytes");
                    if (RangeSupport && RangeHeaderValue.TryParse(request.Request.Headers["Range"], out var range))
                    {
                        Interlocked.Increment(ref RangeRequests);
                        if (!IgnoreRanges)
                        {
                            isRange = true;
                            var item = range.Ranges.Single();
                            start = (int)item.From!.Value;
                            var end = (int)item.To!.Value;
                            length = end - start + 1;
                            if (start % ((body.Length + 15) / 16) != 0) Interlocked.Increment(ref RangeResumes);
                            request.Response.StatusCode = 206;
                            request.Response.AddHeader("Content-Range", $"bytes {start}-{end}/{body.Length}");
                        }
                    }
                    request.Response.ContentLength64 = length;
                    if (isRange && Interlocked.Exchange(ref CutOneRange, 0) == 1)
                    {
                        await request.Response.OutputStream.WriteAsync(body.AsMemory(start, length / 2));
                        request.Response.Abort();
                        continue;
                    }
                    await request.Response.OutputStream.WriteAsync(body.AsMemory(start, length));
                    request.Response.Close();
                }
                catch when (_cancel.IsCancellationRequested) { break; }
                catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException) { }
            }
        });
    }
    public void Dispose() { _cancel.Cancel(); _listener.Close(); _cancel.Dispose(); }
}
