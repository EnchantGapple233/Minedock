using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MineDock.Core;

public enum SourceMode { Auto, Official, Bmclapi }
public enum PageKind { Game, Modpack, Mod, Shader, Resourcepack, Java, Tasks }
public enum FileAction { Download, Text, ExtractJava }
public enum LauncherKind { Pcl, Hmcl }

public sealed class Settings
{
    public string MinecraftRoot { get; set; } = "";
    public string PclExecutable { get; set; } = "";
    public string HmclExecutable { get; set; } = "";
    public string LauncherJavaExecutable { get; set; } = "";
    public LauncherKind Launcher { get; set; }
    [JsonIgnore] public string LauncherExecutable => Launcher == LauncherKind.Hmcl ? HmclExecutable : PclExecutable;
    [JsonIgnore] public string LauncherName => Launcher == LauncherKind.Hmcl ? "HMCL" : "PCL";
    public string SelectedInstance { get; set; } = "";
    public SourceMode Source { get; set; }
    public string Theme { get; set; } = "System";
    public string BackgroundImageFile { get; set; } = "";
    public string BackgroundImageName { get; set; } = "";
    public double BackgroundIntensity { get; set; } = 0.55;
    public string BackgroundStretch { get; set; } = "UniformToFill";
    public string JavaDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime", "MineDock");
    public static string DataDirectory => Environment.GetEnvironmentVariable("MINEDOCK_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineDock");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, JsonTools.Options));
        File.Move(path + ".tmp", path, true);
    }
}

public static class JsonTools
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string Str(this JsonNode? n, string key, string fallback = "") => n?[key]?.ToString() ?? fallback;
    public static long Num(this JsonNode? n, string key) => long.TryParse(n?[key]?.ToString(), out var v) ? v : 0;
    public static bool Bool(this JsonNode? n, string key) => bool.TryParse(n?[key]?.ToString(), out var v) && v;
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024d:0.#} KB",
        < 1073741824 => $"{bytes / 1048576d:0.#} MB",
        _ => $"{bytes / 1073741824d:0.##} GB"
    };
    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
            throw new InvalidOperationException("名称包含非法路径字符。");
        var value = name.Trim();
        if (value.EndsWith('.') || value.Length > 100)
            throw new InvalidOperationException("名称过长或以句点结尾。");
        var stem = value.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && stem[..3] is "COM" or "LPT" && stem[3] is >= '1' and <= '9'))
            throw new InvalidOperationException("名称是 Windows 保留名称。");
        return value;
    }
    public static string Child(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new InvalidOperationException("安装包包含非法路径。");
        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装包试图写入目标目录之外。");
        return result;
    }
}

public record GameRelease(string Id, string Url, DateTime ReleaseDate);
public record LoaderRelease(string Version, bool Stable)
{
    public override string ToString() => Version;
}
public record Instance(string Id, string GameVersion, string Loader, string GameDirectory, bool Isolated, string? DirectoryError)
{
    public string Display => $"{Id}  ·  {GameVersion}{(Loader == "vanilla" ? "" : " / " + Loader)}";
    public override string ToString() => Display;
}
public record SearchProject(string Id, string Slug, string Title, string Description, string IconUrl,
    string Author, long Downloads, string[] Categories, string ProjectType);
public record CategoryTag(string Name, string Header, string ProjectType);
public record LoaderTag(string Name, string[] ProjectTypes);
public record ProjectVersion(string Id, string ProjectId, string Name, string Number, string VersionType,
    DateTime Published, string[] GameVersions, string[] Loaders, List<RemoteFile> Files, List<ModDependency> Dependencies)
{
    public RemoteFile Primary => Files.FirstOrDefault(f => f.Primary) ?? Files.First();
    public string Display => $"{Number}  ·  {VersionType}  ·  {Published:yyyy-MM-dd}";
    public override string ToString() => Display;
}
public record RemoteFile(string Url, string Filename, long Size, string Hash, string Algorithm, bool Primary = false);
public record ModDependency(string ProjectId, string VersionId, string Type);
public record JavaRelease(int Major, string Name, string ImageType, RemoteFile File);

public sealed class PlannedFile
{
    public FileAction Action { get; set; }
    public string Url { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Hash { get; set; } = "";
    public string HashAlgorithm { get; set; } = "SHA1";
    public long Size { get; set; }
    public string Text { get; set; } = "";
    public string Label { get; set; } = "";
    public string CachePath { get; set; } = "";
    public bool Existing => File.Exists(Destination);
}
public sealed class InstallPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Destination { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Type { get; set; } = "安装";
    public List<PlannedFile> Files { get; set; } = [];
    public List<string> Details { get; set; } = [];
    public long Bytes => Files.Sum(f => f.Size);
    public int Conflicts => Files.Count(f => f.Existing);
    public void Add(PlannedFile file)
    {
        var existing = Files.FirstOrDefault(f => f.Destination.Equals(file.Destination, StringComparison.OrdinalIgnoreCase));
        if (existing is null) Files.Add(file);
        else if (existing.Hash != file.Hash || existing.Text != file.Text)
            throw new InvalidOperationException($"安装计划包含重复且不同的文件：{file.Destination}");
    }
}
public record TransferProgress(string Phase, string Message, int Done, int Total, long Bytes, long TotalBytes);
public record HistoryEntry(string Id, string Title, string Target, string Type, string Status, DateTime Time, string Message, string TargetId = "");
