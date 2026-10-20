using System.Text.Json.Nodes;

namespace MineDock.Core;

public static class PclIntegration
{
    public static Dictionary<string, string> ReadIni(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadLines(path))
        {
            var colon = line.IndexOf(':');
            var equal = line.IndexOf('=');
            var split = colon < 1 ? equal : equal < 1 ? colon : Math.Min(colon, equal);
            if (split > 0) result[line[..split].Trim().Trim('\uFEFF')] = line[(split + 1)..].Trim();
        }
        return result;
    }
    public static (string Directory, bool Isolated, string? Error) Resolve(
        string root, string id, string pclExecutable, string loader, string type = "release")
    {
        var version = JsonTools.Child(root, "versions/" + JsonTools.SafeName(id));
        var local = ReadIni(Path.Combine(version, "PCL", "Setup.ini"));
        if (local.TryGetValue("VersionArgumentIndieV2", out var saved))
        {
            if (!bool.TryParse(saved, out var isolated))
                return ("", false, "PCL 的版本隔离设置无法解析，请先在 PCL 中保存该版本设置。");
            return (isolated ? version : root, isolated, null);
        }
        if (local.TryGetValue("VersionArgumentIndie", out var legacy))
        {
            if (!int.TryParse(legacy, out var old) || old is < 0 or > 2)
                return ("", false, "旧版 PCL 版本隔离设置无法解析，请先在 PCL 保存该版本设置。");
            if (old > 0) return (old == 1 ? version : root, old == 1, null);
        }
        var mods = Path.Combine(version, "mods");
        var saves = Path.Combine(version, "saves");
        if ((System.IO.Directory.Exists(mods) && System.IO.Directory.EnumerateFiles(mods).Any()) ||
            (System.IO.Directory.Exists(saves) && System.IO.Directory.EnumerateDirectories(saves).Any()))
            return (version, true, null);
        if (string.IsNullOrWhiteSpace(pclExecutable) || !File.Exists(pclExecutable))
            return ("", false, "关联 PCL 后才能确定资源安装目录；也可以使用「仅下载」。");
        var global = ReadIni(Path.Combine(Path.GetDirectoryName(pclExecutable)!, "PCL", "Setup.ini"));
        if (!global.ContainsKey("LaunchArgumentIndieV2") && global.ContainsKey("LaunchArgumentIndie"))
            return ("", false, "检测到旧版 PCL 全局隔离配置，请先在 PCL 保存目标版本的隔离设置，再安装资源；也可以使用「仅下载」。");
        var policy = global.TryGetValue("LaunchArgumentIndieV2", out var value) ? value : "4";
        if (!int.TryParse(policy, out var mode) || mode is < 0 or > 4)
            return ("", false, "PCL 的全局隔离设置无法解析，请先在 PCL 中保存设置。");
        var modable = loader != "vanilla";
        var formal = type is not ("snapshot" or "old_alpha" or "old_beta");
        var enabled = mode switch { 0 => false, 1 => modable, 2 => !formal, 3 => !formal || modable, _ => true };
        return (enabled ? version : root, enabled, null);
    }
    public static List<Instance> Discover(string root, string pclExecutable)
        => Discover(root, (id, loader, type) => Resolve(root, id, pclExecutable, loader, type));

    internal static List<Instance> Discover(string root,
        Func<string, string, string, (string Directory, bool Isolated, string? Error)> resolve)
    {
        var result = new List<Instance>();
        if (string.IsNullOrWhiteSpace(root)) return result;
        if (!System.IO.Directory.Exists(Path.Combine(root, "versions"))) return result;
        foreach (var folder in System.IO.Directory.EnumerateDirectories(Path.Combine(root, "versions")))
        {
            var id = Path.GetFileName(folder);
            var path = Path.Combine(folder, id + ".json");
            if (!File.Exists(path)) continue;
            try
            {
                var data = JsonNode.Parse(File.ReadAllText(path))!;
                var chain = new List<JsonNode> { data };
                var seen = new HashSet<string> { id };
                var parent = data.Str("inheritsFrom");
                while (parent.Length > 0 && seen.Add(parent) && chain.Count < 8)
                {
                    var parentFile = JsonTools.Child(root, $"versions/{JsonTools.SafeName(parent)}/{parent}.json");
                    if (!File.Exists(parentFile)) break;
                    var node = JsonNode.Parse(File.ReadAllText(parentFile))!;
                    chain.Add(node);
                    parent = node.Str("inheritsFrom");
                }
                var text = string.Join("\n", chain.Select(n => n.ToJsonString()));
                var loader = text.Contains("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase) ? "fabric"
                    : text.Contains("org.quiltmc:quilt-loader", StringComparison.OrdinalIgnoreCase) ? "quilt"
                    : text.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ? "neoforge"
                    : text.Contains("net.minecraftforge", StringComparison.OrdinalIgnoreCase) ? "forge" : "vanilla";
                var baseVersion = chain.Last();
                var game = baseVersion.Str("clientVersion", baseVersion.Str("id", id));
                if (baseVersion["patches"] is JsonArray patches &&
                    patches.FirstOrDefault(p => p.Str("id") == "game")?.Str("version") is { Length: > 0 } patchedGame)
                    game = patchedGame;
                var resolved = resolve(id, loader, chain.Last().Str("type", "release"));
                result.Add(new(id, game, loader, resolved.Directory, resolved.Isolated, resolved.Error));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { result.Add(new(id, "无法解析", "vanilla", "", false, "版本 JSON 无法解析：" + ex.Message)); }
        }
        return result.OrderByDescending(x => File.GetLastWriteTimeUtc(Path.Combine(root, "versions", x.Id, x.Id + ".json"))).ToList();
    }
}
