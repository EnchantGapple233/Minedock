using System.Text.Json.Nodes;

namespace MineDock.Core;

public sealed class JavaService(Network network)
{
    public async Task<List<int>> Majors(CancellationToken ct = default)
    {
        var data = await network.Json("https://api.adoptium.net/v3/info/available_releases", ct);
        var latest = data.Num("most_recent_feature_release");
        return data["available_releases"]!.AsArray().Select(v => (int)v!).Where(v => v <= latest).OrderDescending().ToList();
    }
    public async Task<JavaRelease> Release(int major, string imageType, CancellationToken ct = default)
    {
        var data = await network.Json($"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?architecture=x64&heap_size=normal&image_type={imageType}&os=windows&vendor=eclipse", ct);
        var node = data.AsArray().FirstOrDefault() ?? throw new InvalidOperationException("该主版本暂无所选类型的 Windows x64 包，请切换 JRE / JDK。");
        var package = node["binary"]!["package"]!;
        return new(major, node.Str("release_name"), imageType, new(package.Str("link"), package.Str("name"),
            package.Num("size"), package.Str("checksum"), "SHA256"));
    }
    public InstallPlan Plan(JavaRelease release, string directory)
    {
        var target = Path.Combine(Path.GetFullPath(directory), JsonTools.SafeName(release.Name + "-" + release.ImageType));
        if (Directory.Exists(target)) throw new InvalidOperationException("该 Java 已安装在目标位置。可以在 PCL / HMCL 搜索或导入 bin\\java.exe。");
        var plan = new InstallPlan { Title = $"Eclipse Temurin {release.Major} · {release.ImageType.ToUpperInvariant()}",
            Type = "Java", Destination = target, TargetId = "Java " + release.Major };
        plan.Add(new() { Action = FileAction.ExtractJava, Url = release.File.Url, Hash = release.File.Hash,
            HashAlgorithm = release.File.Algorithm, Size = release.File.Size, Destination = target, Label = release.Name });
        plan.Details.Add("发行方：Eclipse Adoptium · Windows x64 · SHA-256 校验");
        plan.Details.Add("多版本独立保存；不修改 PATH、JAVA_HOME 或启动器设置。");
        plan.Details.Add("安装后在 PCL / HMCL 刷新 Java 列表，或导入目标目录中的 bin\\java.exe。");
        return plan;
    }
}
