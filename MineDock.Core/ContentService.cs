using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MineDock.Core;

public sealed class ContentService(Network network)
{
    public const int SearchPageSize = 30;
    private const string Api = "https://api.modrinth.com/v2";
    private readonly Dictionary<string, Dictionary<string, ProjectVersion>> _installedIndexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _forbiddenProjects = [];
    private readonly HashSet<string> _forbiddenVersions = [];
    public void ResetInstalledIndex() { _installedIndexes.Clear(); _forbiddenProjects.Clear(); _forbiddenVersions.Clear(); }
    public async Task<List<LoaderTag>> LoaderTags(CancellationToken ct = default)
    {
        var json = await network.Json(Api + "/tag/loader", ct);
        return json.AsArray().Select(n => new LoaderTag(n.Str("name"),
            n!["supported_project_types"]!.AsArray().Select(t => t!.ToString()).ToArray())).ToList();
    }
    public async Task<List<CategoryTag>> Categories(CancellationToken ct = default)
    {
        var json = await network.Json(Api + "/tag/category", ct);
        return json.AsArray().Select(n => new CategoryTag(n.Str("name"), n.Str("header"), n.Str("project_type"))).ToList();
    }
    public async Task<(List<SearchProject> Projects, int Total)> Search(string kind, string query, string game,
        string loader, string category, string sort, int offset, CancellationToken ct = default, string feature = "", string extra = "")
    {
        var facets = new List<string[]> { new[] { "project_type:" + kind } };
        if (game.Length > 0) facets.Add(["versions:" + game]);
        if (loader.Length > 0 && loader != "vanilla") facets.Add(["categories:" + loader]);
        if (category.Length > 0) facets.Add(["categories:" + category]);
        if (feature.Length > 0) facets.Add(["categories:" + feature]);
        if (extra.Length > 0) facets.Add(["categories:" + extra]);
        var url = $"{Api}/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(JsonSerializer.Serialize(facets))}&index={sort}&limit={SearchPageSize}&offset={offset}";
        var data = await network.Json(url, ct);
        var projects = data["hits"]!.AsArray().Select(n => new SearchProject(n.Str("project_id"), n.Str("slug"), n.Str("title"),
            n.Str("description"), n.Str("icon_url"), n.Str("author"), n.Num("downloads"),
            n?["categories"]?.AsArray().Select(c => c!.ToString()).ToArray() ?? [], n.Str("project_type"))).ToList();
        return (projects, (int)data.Num("total_hits"));
    }
    public async Task<List<ProjectVersion>> Versions(string id, string game = "", string loader = "", CancellationToken ct = default)
    {
        var filters = new List<string>();
        if (game.Length > 0) filters.Add("game_versions=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { game })));
        if (loader.Length > 0 && loader != "vanilla") filters.Add("loaders=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { loader })));
        var data = await network.Json($"{Api}/project/{Uri.EscapeDataString(id)}/version?{string.Join("&", filters)}", ct);
        return data.AsArray().Select(ParseVersion).OrderByDescending(v => v.Published).ToList();
    }
    public async Task<ProjectVersion> Version(string id, CancellationToken ct = default)
        => ParseVersion(await network.Json(Api + "/version/" + Uri.EscapeDataString(id), ct));
    public static ProjectVersion ParseVersion(JsonNode? n)
    {
        var files = n!["files"]!.AsArray().Select(f => new RemoteFile(f.Str("url"), JsonTools.SafeName(f.Str("filename")),
            f.Num("size"), f?["hashes"]?.Str("sha512", f?["hashes"]?.Str("sha1") ?? "") ?? "",
            f?["hashes"]?["sha512"] is not null ? "SHA512" : "SHA1", f.Bool("primary"))).ToList();
        return new(n.Str("id"), n.Str("project_id"), n.Str("name"), n.Str("version_number"), n.Str("version_type"),
            DateTime.Parse(n.Str("date_published")), n["game_versions"]!.AsArray().Select(x => x!.ToString()).ToArray(),
            n["loaders"]!.AsArray().Select(x => x!.ToString()).ToArray(), files,
            n["dependencies"]!.AsArray().Select(d => new ModDependency(d.Str("project_id"), d.Str("version_id"), d.Str("dependency_type"))).ToList());
    }
    public static PlannedFile File(RemoteFile file, string directory, string label)
        => new() { Url = file.Url, Destination = JsonTools.Child(directory, file.Filename), Size = file.Size,
            Hash = file.Hash, HashAlgorithm = file.Algorithm, Label = label };

    public async Task<InstallPlan> Plan(ProjectVersion version, Instance target, string kind, CancellationToken ct = default)
    {
        ResetInstalledIndex();
        if (target.DirectoryError is not null) throw new InvalidOperationException(target.DirectoryError);
        if (!version.GameVersions.Contains(target.GameVersion))
            throw new InvalidOperationException("所选文件不支持当前游戏版本，可使用「仅下载」。");
        if (kind == "mod" && !version.Loaders.Contains(target.Loader))
            throw new InvalidOperationException("所选模组不支持当前加载器，可使用「仅下载」。");
        var directory = Path.Combine(target.GameDirectory, kind switch { "mod" => "mods", "shader" => "shaderpacks", _ => "resourcepacks" });
        var plan = new InstallPlan { Title = version.Name, Destination = directory, TargetId = target.Id,
            Type = kind switch { "mod" => "模组", "shader" => "光影", _ => "材质包" } };
        if (kind == "mod")
            await AddDependencies(plan, version, target, new(), ct);
        else
        {
            plan.Add(File(version.Primary, directory, version.Name));
            if (kind == "shader")
            {
                if (target.Loader == "fabric" && version.Loaders.Contains("iris"))
                {
                    RejectIncompatibleRenderer(target.GameDirectory);
                    var iris = (await Versions("iris", target.GameVersion, "fabric", ct)).FirstOrDefault(v => v.VersionType == "release")
                        ?? throw new InvalidOperationException("没有找到当前版本的 Iris 正式版，请使用仅下载或先在启动器安装光影支持。");
                    await AddDependencies(plan, iris, target, new(), ct);
                    plan.Details.Add("光影运行组件：Iris 及其必需依赖已列入计划。");
                }
                else
                    throw new InvalidOperationException("当前版本的光影运行组件无法自动确认。请使用「仅下载」，并通过启动器配置对应渲染组件。");
            }
        }
        plan.Details.Add($"目标：{target.Display}");
        plan.Details.Add("兼容检查依据上游版本、加载器、声明依赖及冲突信息。");
        return plan;
    }
    private static void RejectIncompatibleRenderer(string directory)
    {
        var mods = Path.Combine(directory, "mods");
        if (!Directory.Exists(mods)) return;
        foreach (var path in Directory.EnumerateFiles(mods, "*.jar"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("OptiFine", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("已有 OptiFine，不能自动加入 Iris。请先在启动器处理光影运行组件，或使用「仅下载」。");
            try
            {
                using var archive = ZipFile.OpenRead(path);
                var metadata = archive.GetEntry("fabric.mod.json");
                if (metadata is null) continue;
                using var stream = metadata.Open();
                var id = JsonNode.Parse(stream).Str("id");
                if (id is "canvas" or "optifabric" or "vulkanmod")
                    throw new InvalidOperationException($"已有渲染组件 {id}，停止自动加入 Iris。请先在启动器处理，或使用「仅下载」。");
            }
            catch (InvalidDataException) { }
        }
    }
    public async Task AddDependencies(InstallPlan plan, ProjectVersion version, Instance target,
        Dictionary<string, string> resolved, CancellationToken ct)
    {
        if (_forbiddenProjects.Contains(version.ProjectId) || _forbiddenVersions.Contains(version.Id))
            throw new InvalidOperationException($"{version.Name} 与本次计划中的另一个项目声明冲突。");
        if (resolved.TryGetValue(version.ProjectId, out var existing))
        {
            if (existing != version.Id) throw new InvalidOperationException("依赖要求了同一项目的不同版本，请先调整所选模组。");
            return;
        }
        resolved[version.ProjectId] = version.Id;
        if (!version.GameVersions.Contains(target.GameVersion) || !version.Loaders.Contains(target.Loader))
            throw new InvalidOperationException($"依赖 {version.Name} 与当前游戏或加载器不兼容。");
        var modDirectory = Path.Combine(target.GameDirectory, "mods");
        var project = await network.Json(Api + "/project/" + version.ProjectId, ct);
        if (project.Str("client_side") == "unsupported")
            throw new InvalidOperationException($"{project.Str("title")} 是服务端专用项目，不安装到客户端。");
        var slug = project.Str("slug");
        if (!_installedIndexes.TryGetValue(modDirectory, out var installedProjects))
        {
            installedProjects = new();
            if (Directory.Exists(modDirectory))
            {
                var hashes = new List<string>();
                foreach (var path in Directory.EnumerateFiles(modDirectory, "*.jar"))
                {
                    await using var stream = System.IO.File.OpenRead(path);
                    hashes.Add(Convert.ToHexString(await SHA512.HashDataAsync(stream, ct)).ToLowerInvariant());
                }
                foreach (var chunk in hashes.Chunk(500))
                {
                    var found = await network.Post(Api + "/version_files", new { hashes = chunk, algorithm = "sha512" }, ct);
                    foreach (var pair in found.AsObject())
                    {
                        var installed = ParseVersion(pair.Value);
                        if (installedProjects.TryGetValue(installed.ProjectId, out var duplicate) && duplicate.Id != installed.Id)
                            throw new InvalidOperationException("目标目录中存在同一项目的多个版本，请先在启动器处理。");
                        installedProjects[installed.ProjectId] = installed;
                    }
                }
                var unrecognized = hashes.Count - installedProjects.Count;
                if (unrecognized > 0) plan.Details.Add($"{unrecognized} 个已有模组未被 Modrinth 识别，未自动验证其运行时兼容性。");
            }
            _installedIndexes[modDirectory] = installedProjects;
        }
        if (installedProjects.TryGetValue(version.ProjectId, out var current) && current.Id != version.Id)
            throw new InvalidOperationException($"已安装 {slug} 的其他版本（{current.Number}），请先在启动器处理版本替换。");
        plan.Details.Add($"依赖 / 项目：{project.Str("title")} · {version.Number}");
        foreach (var dependency in version.Dependencies.Where(d => d.Type == "required"))
        {
            ProjectVersion required;
            if (dependency.VersionId.Length > 0) required = await Version(dependency.VersionId, ct);
            else if (dependency.ProjectId.Length > 0)
                required = (await Versions(dependency.ProjectId, target.GameVersion, target.Loader, ct))
                    .FirstOrDefault(v => v.VersionType == "release")
                    ?? throw new InvalidOperationException("缺少可用的正式版必需依赖：" + dependency.ProjectId);
            else throw new InvalidOperationException("项目包含无法自动定位的必需依赖，请使用仅下载并在启动器中处理。");
            await AddDependencies(plan, required, target, resolved, ct);
        }
        foreach (var conflict in version.Dependencies.Where(d => d.Type == "incompatible"))
        {
            if (conflict.VersionId.Length > 0)
            {
                _forbiddenVersions.Add(conflict.VersionId);
                var forbidden = await Version(conflict.VersionId, ct);
                if (installedProjects.TryGetValue(forbidden.ProjectId, out var installed) && installed.Id == forbidden.Id)
                    throw new InvalidOperationException($"{version.Name} 声明与已有模组冲突，请先在启动器调整。");
                if (resolved.TryGetValue(forbidden.ProjectId, out var planned) && planned == forbidden.Id)
                    throw new InvalidOperationException($"{version.Name} 与本次计划中的另一个项目版本冲突。");
            }
            else if (conflict.ProjectId.Length > 0)
            {
                _forbiddenProjects.Add(conflict.ProjectId);
                if (installedProjects.ContainsKey(conflict.ProjectId) || resolved.ContainsKey(conflict.ProjectId))
                    throw new InvalidOperationException($"{version.Name} 声明与已有模组或本次计划冲突，请先在启动器调整。");
            }
        }
        if (current is null) plan.Add(File(version.Primary, modDirectory, project.Str("title") + " " + version.Number));
        else plan.Details.Add($"已满足：{project.Str("title")} · {current.Number}");
    }
}
