using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MineDock.Core;

public sealed class LoaderService(Network network)
{
    public static readonly string[] Kinds = ["fabric", "forge", "neoforge", "quilt", "optifine", "legacy-fabric", "liteloader"];
    public static string Name(string kind) => kind switch
    {
        "fabric" => "Fabric", "forge" => "Forge", "neoforge" => "NeoForge", "quilt" => "Quilt",
        "optifine" => "OptiFine", "legacy-fabric" => "Legacy Fabric", "liteloader" => "LiteLoader", _ => "原版"
    };
    public static bool IsMeta(string kind) => kind is "fabric" or "quilt" or "legacy-fabric";
    public static string Meta(string kind) => kind switch
    {
        "fabric" => "https://meta.fabricmc.net/v2",
        "quilt" => "https://meta.quiltmc.org/v3",
        "legacy-fabric" => "https://meta.legacyfabric.net/v2",
        _ => throw new InvalidOperationException("该加载器不使用 Meta 接口。")
    };
    private static IEnumerable<JsonNode> Nodes(JsonNode data) =>
        data is JsonArray array ? array.OfType<JsonNode>() : [data];
    public async Task<List<LoaderRelease>> Releases(string kind, string game, CancellationToken ct = default)
    {
        if (!Kinds.Contains(kind)) throw new InvalidOperationException("不支持的加载器。");
        var escaped = Uri.EscapeDataString(game);
        if (IsMeta(kind))
        {
            // Some Meta APIs return loaders even for a nonexistent intermediary.
            var supported = await network.Json(Meta(kind) + "/versions/game", ct);
            if (!Nodes(supported).Any(n => n.Str("version") == game)) return [];
            var data = await network.Json(Meta(kind) + "/versions/loader/" + escaped, ct);
            return Sort(Nodes(data).Select(n => n["loader"]!).Select(n =>
                new LoaderRelease(n.Str("version"), n["stable"] is null ? Stable(n.Str("version")) : n.Bool("stable"))));
        }
        var endpoint = kind switch
        {
            "forge" => "/forge/minecraft/" + escaped,
            "neoforge" => "/neoforge/list/" + escaped,
            "optifine" => "/optifine/" + escaped,
            _ => "/liteloader/list?mcversion=" + escaped
        };
        var result = await network.Json("https://bmclapi2.bangbang93.com" + endpoint, ct);
        return Sort(Nodes(result).Where(n => n.Str("mcversion") == game || kind == "optifine" &&
            n.Str("mcversion") == game + ".0").Select(n =>
        {
            var version = kind == "optifine" ? n.Str("type") + "_" + n.Str("patch") : n.Str("version");
            if (kind == "neoforge") version = Regex.Replace(version, @"^1\.20\.1-(?:forge-)?", "");
            return new LoaderRelease(version, Stable(version));
        }).Where(v => v.Version.Length > 0));
    }
    public static bool Stable(string value) => !Regex.IsMatch(value, "alpha|beta|pre|rc|snapshot", RegexOptions.IgnoreCase);
    public static List<LoaderRelease> Sort(IEnumerable<LoaderRelease> versions) => versions
        .DistinctBy(v => v.Version).OrderByDescending(v => v.Version, NaturalVersionComparer.Instance).ToList();
}

public sealed class NaturalVersionComparer : IComparer<string>
{
    public static readonly NaturalVersionComparer Instance = new();
    public int Compare(string? x, string? y)
    {
        var a = Regex.Matches(x ?? "", @"\d+|\D+");
        var b = Regex.Matches(y ?? "", @"\d+|\D+");
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var left = a[i].Value; var right = b[i].Value;
            int comparison;
            if (ulong.TryParse(left, out var l) && ulong.TryParse(right, out var r)) comparison = l.CompareTo(r);
            else comparison = StringComparer.OrdinalIgnoreCase.Compare(left, right);
            if (comparison != 0) return comparison;
        }
        return a.Count.CompareTo(b.Count);
    }
}
