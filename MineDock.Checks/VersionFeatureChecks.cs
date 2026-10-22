using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MineDock.Core;

internal static class VersionFeatureChecks
{
    public static async Task Run(string root, Action<bool, string> check)
    {
        using var network = new Network(new Handler());
        var service = new LoaderService(network);
        foreach (var kind in LoaderService.Kinds)
        {
            var loaders = await service.Releases(kind, "1.21.1");
            check(loaders.Count == 2, kind + " retains every compatible version");
            check(loaders.Any(l => !l.Stable), kind + " retains preview versions");
        }
        check((await service.Releases("fabric", "unsupported")).Count == 0, "unsupported intermediary does not expose unrelated Fabric loaders");
        check(LoaderService.Sort([new("0.9.0",true), new("0.20.1",true), new("0.10.0",true)])[0].Version == "0.20.1",
            "loader versions use numeric ordering rather than lexical ordering");
        var gameService = new GameService(network);
        var games = await gameService.Releases();
        check(games.Select(g => g.Type).Distinct().Count() == 4, "Minecraft releases include snapshots and historical alpha and beta");
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            var rejected = false;
            try { await gameService.Releases(canceled.Token); }
            catch (OperationCanceledException) { rejected = true; }
            check(rejected, "Cancelled game-list requests cannot return cached results to an obsolete source request");
        }
        var natives=new InstallPlan();
        GameService.Libraries(natives,JsonNode.Parse("""
            {"libraries":[{"name":"org.lwjgl.lwjgl:lwjgl-platform:2.9.4+legacyfabric.17",
            "url":"https://maven.legacyfabric.net/","natives":{"windows":"natives-windows","linux":"natives-linux"}}]}
            """),root);
        check(natives.Files.Count==1 && natives.Files[0].Url.EndsWith("-natives-windows.jar"),
            "Meta native libraries resolve the Windows classifier without requesting a nonexistent base JAR");
        var current = new Version("0.1.4");
        var liteMetadata = """
            <metadata><groupId>com.mumfrey</groupId><artifactId>liteloader</artifactId><version>1.12.2-SNAPSHOT</version>
            <versioning><snapshot><timestamp>20171128.144431</timestamp><buildNumber>4</buildNumber></snapshot></versioning></metadata>
            """;
        check(LoaderInstaller.LiteLoaderSnapshotUrl("1.12.2-SNAPSHOT", liteMetadata).EndsWith("liteloader-1.12.2-20171128.144431-4-release.jar"),
            "LiteLoader snapshots resolve the official timestamped Maven artifact");
        var release = Release("v0.1.5");
        check(UpdateService.Parse(release, current)?.Version == new Version("0.1.5"), "new stable GitHub release is selected");
        check(UpdateService.Parse(Release("v0.1.4"), current) is null, "same release is not downloaded again");
        check(UpdateService.Parse(Release("v0.1.4.0"), current) is null, "three-part and four-part equivalent versions do not cause an update loop");
        check(UpdateService.Parse(Release("v0.1.3"), current) is null, "updates cannot downgrade the application");
        foreach (var field in new[] { "draft", "prerelease" })
        {
            var data = Release("v0.1.5"); data[field] = true;
            check(UpdateService.Parse(data, current) is null, "automatic updater ignores " + field);
        }
        check(UpdateService.Parse(Release("v0.1.5-beta.1"), current) is null, "nonstable release tags are ignored");
        async Task Fails(Func<Task> action, string message)
        {
            try { await action(); }
            catch (Exception ex) when (ex is InvalidDataException or IOException) { check(true, message); return; }
            throw new Exception(message);
        }
        await Fails(() => Task.FromResult(UpdateService.ValidateAssetUrl("https://example.com/MineDock.exe", "v0.1.5")),
            "updater rejects release assets from another repository");
        await Fails(() => Task.FromResult(LoaderInstaller.LiteLoaderSnapshotUrl("1.12.1-SNAPSHOT", liteMetadata)),
            "LiteLoader snapshot metadata cannot substitute another game version");
        await Fails(() => Task.FromResult(UpdateService.ValidateAssetUrl(UpdateService.Repository+"/releases/download/v0.1.3/MineDock.exe", "v0.1.5")),
            "updater rejects assets from a different release tag");
        await Fails(() => Task.FromResult(UpdateService.ValidateAssetUrl("http://github.com/EnchantGapple233/Minedock/releases/download/v0.1.5/MineDock.exe", "v0.1.5")),
            "updater requires HTTPS");
        var invalidDigest=Release("v0.1.5");
        invalidDigest["assets"]![0]!["digest"]="sha1:"+new string('a',40);
        await Fails(() => Task.FromResult(UpdateService.Parse(invalidDigest,current)), "updater rejects a weak or malformed release digest");
        await Fails(() => Task.FromResult(UpdateService.ParseChecksum(new string('a',64) + "  other.exe")),
            "updater rejects checksums for another file");
        check(UpdateService.ParseChecksum(new string('a',64) + "  MineDock.exe") == new string('a',64),
            "legacy SHA256SUMS fallback resolves the exact EXE");
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "MineDock.exe");
        var payload = Path.Combine(root, "new.exe");
        await File.WriteAllTextAsync(target, "old");
        await File.WriteAllTextAsync(payload, "new");
        string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var request = new UpdateRequest(target, payload, Hash("new"), 3, Hash("old"), 0, 0,
            Path.Combine(root,"ready"), target+".bak", Path.Combine(root,"result.json"));
        await UpdateService.Replace(request);
        check(File.ReadAllText(target) == "new" && File.ReadAllText(request.Backup) == "old",
            "updater atomically replaces the EXE while retaining a verified backup");
        await UpdateService.Rollback(request);
        check(File.ReadAllText(target) == "old", "failed startup can restore the previous executable");
        await File.WriteAllTextAsync(payload, "tampered");
        await Fails(() => UpdateService.Replace(request), "tampered update payload is rejected");
        check(File.ReadAllText(target) == "old", "invalid update leaves the working program intact");
        await File.WriteAllTextAsync(payload, "new");
        await File.WriteAllTextAsync(target, "external");
        await Fails(() => UpdateService.Replace(request), "updater preserves a concurrently changed current EXE");
    }
    private static JsonNode Release(string tag) => new JsonObject
    {
        ["tag_name"]=tag, ["draft"]=false, ["prerelease"]=false,
        ["assets"]=new JsonArray(new JsonObject { ["name"]="MineDock.exe", ["state"]="uploaded", ["size"]=3,
            ["digest"]="sha256:"+new string('a',64),
            ["browser_download_url"]=UpdateService.Repository+"/releases/download/"+tag+"/MineDock.exe" })
    };
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object body = path.Contains("/versions/game")
                ? new[] { new { version="1.21.1" } }
                : path.Contains("version_manifest")
                    ? new { versions = new[] { "release","snapshot","old_alpha","old_beta" }.Select((type,i) =>
                        new { id="game"+i, type, releaseTime="2026-10-22T00:00:00Z", url="https://example.org/game.json" }) }
                : path.Contains("/versions/loader/")
                    ? new[] { new { loader=new { version="0.20.0", stable=true } }, new { loader=new { version="0.21.0-beta.1", stable=false } } }
                : path.Contains("/optifine/")
                    ? new[] { new { mcversion="1.21.1", type="HD_U", patch="J1" }, new { mcversion="1.21.1", type="HD_U_J2", patch="pre1" } }
                : new[] { new { mcversion="1.21.1", version="21.1.1" }, new { mcversion="1.21.1", version="21.1.2-beta" },
                    new { mcversion="1.20.1", version="wrong-game" } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(body)) });
        }
    }
}
