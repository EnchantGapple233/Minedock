using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MineDock.Core;

public sealed class GameService(Network network)
{
    public const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private List<GameRelease>? _releases;
    public void Invalidate() => _releases = null;
    public async Task<List<GameRelease>> Releases(CancellationToken ct = default)
    {
        if (_releases is not null) return _releases;
        var root = await network.Json(ManifestUrl, ct);
        _releases = root["versions"]!.AsArray().Where(x => x.Str("type") == "release")
            .Select(x => new GameRelease(x.Str("id"), x.Str("url"), DateTime.Parse(x.Str("releaseTime")))).ToList();
        return _releases;
    }
    public async Task<JsonNode> Metadata(string version, CancellationToken ct = default)
    {
        var release = (await Releases(ct)).FirstOrDefault(x => x.Id == version)
            ?? throw new InvalidOperationException("游戏正式版本不存在。");
        return await network.Json(release.Url, ct);
    }
    public async Task<List<LoaderRelease>> Loaders(string game, CancellationToken ct = default)
    {
        var json = await network.Json($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(game)}", ct);
        return json.AsArray().Select(x => x!["loader"]!).Select(x => new LoaderRelease(x.Str("version"), x.Bool("stable"))).ToList();
    }
    public static bool Allowed(JsonNode? library)
    {
        if (library?["rules"] is not JsonArray rules) return true;
        var allow = false;
        foreach (var rule in rules)
        {
            var os = rule?["os"];
            if (os is not null)
            {
                if (os.Str("name", "windows") != "windows") continue;
                var arch = os.Str("arch");
                if (arch.Length > 0 && arch is not ("amd64" or "x86_64" or "x64")) continue;
                if (os.Str("version") is { Length: > 0 } pattern &&
                    !Regex.IsMatch(Environment.OSVersion.Version.ToString(), pattern)) continue;
            }
            if (rule?["features"] is JsonObject features && features.Any(f => f.Value?.ToString() == "true")) continue;
            allow = rule.Str("action") == "allow";
        }
        return allow;
    }
    public static string MavenPath(string coordinate)
    {
        var ext = "jar";
        var at = coordinate.Split('@');
        if (at.Length > 1) ext = at[1];
        var pieces = at[0].Split(':');
        if (pieces.Length is < 3 or > 4) throw new InvalidDataException("无效的 Maven 坐标：" + coordinate);
        return $"{pieces[0].Replace('.', '/')}/{pieces[1]}/{pieces[2]}/{pieces[1]}-{pieces[2]}{(pieces.Length == 4 ? "-" + pieces[3] : "")}.{ext}";
    }
    public static void Libraries(InstallPlan plan, JsonNode? data, string root)
    {
        if (data?["libraries"] is not JsonArray libraries) return;
        foreach (var lib in libraries)
        {
            if (!Allowed(lib)) continue;
            var artifact = lib?["downloads"]?["artifact"];
            if (artifact is not null)
                AddDownload(plan, artifact, JsonTools.Child(root, "libraries/" + artifact.Str("path")), lib.Str("name"));
            else if (lib?["downloads"] is null)
            {
                var path = MavenPath(lib.Str("name"));
                plan.Add(new() { Url = lib.Str("url", "https://libraries.minecraft.net/").TrimEnd('/') + "/" + path,
                    Destination = JsonTools.Child(root, "libraries/" + path), Label = lib.Str("name"),
                    Hash = lib.Str("sha512", lib.Str("sha1")), HashAlgorithm = lib?["sha512"] is null ? "SHA1" : "SHA512",
                    Size = lib.Num("size") });
            }
            var native = lib?["natives"]?.Str("windows").Replace("${arch}", "64");
            if (!string.IsNullOrWhiteSpace(native) && lib?["downloads"]?["classifiers"]?[native] is JsonNode file)
                AddDownload(plan, file, JsonTools.Child(root, "libraries/" + file.Str("path")), lib.Str("name") + " native");
        }
    }
    public static void AddDownload(InstallPlan plan, JsonNode data, string destination, string label)
        => plan.Add(new() { Url = data.Str("url"), Destination = destination, Size = data.Num("size"),
            Hash = data.Str("sha1"), HashAlgorithm = "SHA1", Label = label });
    public async Task<InstallPlan> Plan(string root, string game, string? loader, string targetId, CancellationToken ct = default)
    {
        root = Path.GetFullPath(root);
        JsonTools.SafeName(targetId);
        var ownFolder = JsonTools.Child(root, "versions/" + targetId);
        if (Directory.Exists(ownFolder))
            throw new InvalidOperationException("目标版本名称已经存在。请选择新名称；已有安装可从任务记录重试。");
        var metadata = await Metadata(game, ct);
        var baseId = metadata.Str("id");
        var plan = new InstallPlan { Title = loader is null ? $"Minecraft {game}" : $"Minecraft {game} · Fabric {loader}",
            Destination = ownFolder, TargetId = targetId, Type = "游戏" };
        var baseDirectory = JsonTools.Child(root, "versions/" + baseId);
        if (loader is null && targetId != baseId)
        {
            metadata["id"] = targetId;
            metadata["clientVersion"] = game;
            baseDirectory = ownFolder;
        }
        var jarName = loader is null ? targetId : baseId;
        var baseJson = Path.Combine(baseDirectory, jarName + ".json");
        if (loader is not null && File.Exists(baseJson))
        {
            var installed = JsonNode.Parse(await File.ReadAllTextAsync(baseJson, ct));
            foreach (var key in new[] { "mainClass", "libraries", "arguments", "minecraftArguments", "inheritsFrom" })
                if (!JsonNode.DeepEquals(installed?[key], metadata[key]))
                    throw new InvalidOperationException($"基础版本 {baseId} 的启动配置已被修改，请先在启动器中处理该版本，避免覆盖已有修改。");
        }
        plan.Add(new() { Action = FileAction.Text, Destination = baseJson, Text = metadata.ToJsonString(JsonTools.Options), Label = "游戏版本配置" });
        AddDownload(plan, metadata["downloads"]!["client"]!, Path.Combine(baseDirectory, jarName + ".jar"), "Minecraft 客户端");
        Libraries(plan, metadata, root);
        if (metadata["logging"]?["client"]?["file"] is JsonNode logging)
            AddDownload(plan, logging, JsonTools.Child(root, "assets/log_configs/" + JsonTools.SafeName(logging.Str("id"))), "日志配置");
        if (metadata["assetIndex"] is JsonNode index)
        {
            var assets = await network.Json(index.Str("url"), ct);
            var indexId = index.Str("id");
            AddDownload(plan, index, JsonTools.Child(root, "assets/indexes/" + indexId + ".json"), "资源索引");
            foreach (var pair in assets["objects"]!.AsObject())
            {
                var hash = pair.Value.Str("hash");
                if (hash.Length != 40) throw new InvalidDataException("资源索引包含无效哈希。");
                var relative = hash[..2] + "/" + hash;
                var url = "https://resources.download.minecraft.net/" + relative;
                plan.Add(new() { Url = url, Destination = JsonTools.Child(root, "assets/objects/" + relative),
                    Hash = hash, Size = pair.Value.Num("size"), Label = pair.Key });
                if (assets.Bool("virtual"))
                    plan.Add(new() { Url = url, Destination = JsonTools.Child(root, $"assets/virtual/{indexId}/" + pair.Key),
                        Hash = hash, Size = pair.Value.Num("size"), Label = pair.Key });
                if (assets.Bool("map_to_resources"))
                    plan.Add(new() { Url = url, Destination = JsonTools.Child(root, "resources/" + pair.Key),
                        Hash = hash, Size = pair.Value.Num("size"), Label = pair.Key });
            }
        }
        if (loader is not null)
        {
            var profile = await network.Json($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(game)}/{Uri.EscapeDataString(loader)}/profile/json", ct);
            profile["id"] = targetId;
            plan.Add(new() { Action = FileAction.Text, Destination = Path.Combine(ownFolder, targetId + ".json"),
                Text = profile.ToJsonString(JsonTools.Options), Label = "Fabric 启动配置" });
            Libraries(plan, profile, root);
        }
        foreach (var file in plan.Files.Where(f => f.Action == FileAction.Download && f.Hash.Length == 0))
        {
            var checksum = (await network.Text(file.Url + ".sha1", ct)).Trim().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!Regex.IsMatch(checksum, "^[a-fA-F0-9]{40}$"))
                throw new InvalidDataException("上游没有提供有效校验信息：" + file.Label);
            file.Hash = checksum;
            file.HashAlgorithm = "SHA1";
        }
        plan.Details.Add($"游戏：{game} · {(loader is null ? "原版" : "Fabric " + loader)}");
        var requiredJava = metadata["javaVersion"]?.Num("majorVersion") ?? 8;
        plan.Details.Add($"Java 要求：{requiredJava}");
        plan.Details.Add("安装游戏本体、支持库及完整资源；启动器配置保持由启动器管理。");
        return plan;
    }
}
