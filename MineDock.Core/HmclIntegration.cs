using System.Text.Json.Nodes;

namespace MineDock.Core;

public static class HmclIntegration
{
    private sealed record GameDirectory(string Id, string Path, JsonObject Data);
    private sealed record Configuration(string Base, bool Modern, JsonObject Settings,
        List<GameDirectory> Directories, string Home);

    public static string? FindMinecraftRoot(string executable)
    {
        var config = ReadConfiguration(executable);
        var selected = config.Settings.Str(config.Modern ? "selectedGameDirectory" : "last");
        var entry = config.Directories.FirstOrDefault(d => d.Id == selected);
        entry ??= config.Directories.Count == 1 ? config.Directories[0] : null;
        if (entry is null && config.Directories.Count > 1)
            throw new InvalidOperationException("HMCL 中有多个游戏目录，请先在 HMCL 选择目标目录并保存设置，再重新关联。");
        if (entry is null) return null;
        if (!Directory.Exists(entry.Path))
            throw new InvalidOperationException("HMCL 选中的游戏目录不存在，请先在 HMCL 检查目录设置。");
        return entry.Path;
    }

    public static (string Directory, bool Isolated, string? Error) Resolve(string root, string id, string executable)
    {
        try
        {
            if (!File.Exists(executable))
                throw new InvalidOperationException("请关联实际的 HMCL 启动器后安装资源；也可以使用「仅下载」。");
            var version = JsonTools.Child(root, "versions/" + JsonTools.SafeName(id));
            if (File.Exists(Path.Combine(version, "modpack.json"))) return (version, true, null);
            var config = ReadConfiguration(executable);
            var matching = config.Directories.Where(d => SamePath(d.Path, root)).ToList();
            var selected = config.Settings.Str(config.Modern ? "selectedGameDirectory" : "last");
            var entry = matching.FirstOrDefault(d => d.Id == selected)
                ?? (matching.Count == 1 ? matching[0] : null);
            if (config.Directories.Count > 0 && entry is null)
                throw new InvalidOperationException("当前目录无法对应到唯一的 HMCL 配置，请在 HMCL 选择该目录并保存设置后刷新。");

            var currentFile = Path.Combine(version, ".hmcl", "config", "instance-game-settings.json");
            var legacyFile = Path.Combine(version, "hmclversion.cfg");
            var local = ReadObject(currentFile, "instance-game-settings");
            if (config.Modern || local is not null)
            {
                var presetData = ReadObject(Path.Combine(config.Home, "config", "game-settings.json"), "game-settings");
                var presets = presetData?["presets"] as JsonArray;
                if (presetData?["presets"] is not null && (presets is null || presets.Any(p => p is not JsonObject)))
                    throw new InvalidDataException("HMCL 的游戏设置预设列表无法解析。");
                var parent = local.Str("parent");
                var parentSetting = presets?.OfType<JsonObject>().FirstOrDefault(p => p.Str("id") == parent);
                parentSetting ??= presets?.OfType<JsonObject>().FirstOrDefault(p =>
                    p.Str("id") == config.Settings.Str("defaultGameSettingsPreset"));
                parentSetting ??= presets?.OfType<JsonObject>().FirstOrDefault();
                if (parentSetting is null && parent.Length > 0)
                    throw new InvalidOperationException("HMCL 的游戏设置预设无法读取，请在 HMCL 保存设置后刷新。");
                if (local is not null)
                {
                    var running = local.Str("runningDirectory");
                    var overrides = local["overrideProperties"];
                    if (overrides is not null && overrides is not JsonArray)
                        throw new InvalidDataException("HMCL 的独立设置格式无法解析。");
                    var ownDirectory = running.Length > 0 ||
                        (overrides as JsonArray)?.Any(p => p?.ToString() == "runningDirectory") == true;
                    if (ownDirectory) return Result(running.Length == 0 ? version : ResolvePath(running, config.Base), root);
                }
                else if (ReadObject(legacyFile) is { } legacy)
                {
                    if (!ReadBoolean(legacy, "usesGlobal", false))
                        return LegacyDirectory(legacy, root, version, config.Base);
                    var legacyParent = presets?.OfType<JsonObject>().FirstOrDefault(p =>
                        p.Str("id") == entry?.Data.Str("legacyGameSettings")) ?? parentSetting;
                    if (legacyParent.Str("runningDirectory").Length > 0)
                        return Result(ResolvePath(legacyParent.Str("runningDirectory"), config.Base), root);
                    return Result(legacyParent.Str("defaultIsolationType") == "ALWAYS" ? version : root, root);
                }
                var inherited = parentSetting.Str("runningDirectory");
                return Result(inherited.Length == 0 ? root : ResolvePath(inherited, config.Base), root);
            }
            var oldLocal = ReadObject(legacyFile);
            var effective = oldLocal is not null && !ReadBoolean(oldLocal, "usesGlobal", false)
                ? oldLocal : entry?.Data["global"] as JsonObject;
            return LegacyDirectory(effective, root, version, config.Base);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ("", false, "无法确定 HMCL 的资源安装目录：" + ex.Message);
        }
    }

    private static (string Directory, bool Isolated, string? Error) LegacyDirectory(
        JsonObject? settings, string root, string version, string baseDirectory)
    {
        var mode = settings.Str("gameDirType", "0");
        return mode switch
        {
            "0" or "ROOT_FOLDER" => Result(root, root),
            "1" or "VERSION_FOLDER" => Result(version, root),
            "2" or "CUSTOM" when !string.IsNullOrWhiteSpace(settings.Str("gameDir")) =>
                Result(ResolvePath(settings.Str("gameDir"), baseDirectory), root),
            _ => throw new InvalidDataException("HMCL 的运行目录设置无效，请先在 HMCL 重新保存。")
        };
    }

    private static Configuration ReadConfiguration(string executable)
    {
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var home = Environment.GetEnvironmentVariable("HMCL_LOCAL_HOME");
        home = string.IsNullOrWhiteSpace(home) ? Path.Combine(baseDirectory, ".hmcl") : ResolvePath(home, baseDirectory);
        var modernSettings = ReadObject(Path.Combine(home, "config", "launcher-settings.json"), "launcher-settings");
        var directories = ReadObject(Path.Combine(home, "config", "game-directories.json"), "game-directories");
        if (modernSettings is not null || directories is not null)
        {
            var entries = ReadDirectories(directories, baseDirectory);
            var userHome = Environment.GetEnvironmentVariable("HMCL_USER_HOME");
            userHome = string.IsNullOrWhiteSpace(userHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".hmcl")
                : ResolvePath(userHome, baseDirectory);
            var userDirectories = ReadObject(Path.Combine(userHome, "config", "user-game-directories.json"), "game-directories");
            entries.AddRange(ReadDirectories(userDirectories, baseDirectory));
            return new(baseDirectory, true, modernSettings ?? new(), entries, home);
        }
        var legacy = ReadObject(Path.Combine(home, "settings.json"))
            ?? ReadObject(Path.Combine(baseDirectory, "hmcl.json"))
            ?? ReadObject(Path.Combine(baseDirectory, ".hmcl.json")) ?? new();
        var profiles = legacy["configurations"] as JsonObject;
        if (legacy["configurations"] is not null && (profiles is null || profiles.Any(p => p.Value is not JsonObject)))
            throw new InvalidDataException("HMCL 的旧版游戏目录列表无法解析。");
        var legacyEntries = profiles?.Where(p => p.Value is JsonObject).Select(p =>
            new GameDirectory(p.Key, ResolvePath(p.Value.Str("gameDir", ".minecraft"), baseDirectory), (JsonObject)p.Value!)).ToList() ?? [];
        return new(baseDirectory, false, legacy, legacyEntries, home);
    }

    private static List<GameDirectory> ReadDirectories(JsonObject? data, string baseDirectory)
    {
        if (data?["directories"] is null) return [];
        if (data["directories"] is not JsonArray entries)
            throw new InvalidDataException("HMCL 的游戏目录列表无法解析。");
        return entries.Select(n => n is JsonObject obj && obj.Str("id").Length > 0 && obj.Str("path").Length > 0
            ? new GameDirectory(obj.Str("id"), ResolvePath(obj.Str("path"), baseDirectory), obj)
            : throw new InvalidDataException("HMCL 的游戏目录记录无效。")).ToList();
    }

    private static JsonObject? ReadObject(string file, string? schemaId = null)
    {
        if (!File.Exists(file)) return null;
        var data = JsonNode.Parse(File.ReadAllText(file)) as JsonObject
            ?? throw new InvalidDataException("配置不是 JSON 对象：" + Path.GetFileName(file));
        if (schemaId is not null && data["$schema"] is { } schema)
        {
            var prefix = "https://schemas.glavo.site/hmcl/" + schemaId + "/";
            var text = schema.ToString();
            if (!text.StartsWith(prefix, StringComparison.Ordinal) ||
                !Version.TryParse(text[prefix.Length..], out var version) || version.Major != 1)
                throw new InvalidDataException("HMCL 配置版本暂不支持：" + Path.GetFileName(file));
        }
        return data;
    }

    private static bool ReadBoolean(JsonObject data, string key, bool fallback)
        => data[key] is null ? fallback : bool.TryParse(data.Str(key), out var value) ? value
            : throw new InvalidDataException("HMCL 的 " + key + " 设置无效。");

    private static string ResolvePath(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("HMCL 的目录为空。");
        var path = Path.GetFullPath(value.Replace('/', Path.DirectorySeparatorChar), baseDirectory);
        if (File.Exists(path)) throw new InvalidDataException("HMCL 配置中的目录指向了文件。");
        return path;
    }

    private static bool SamePath(string left, string right)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    private static (string Directory, bool Isolated, string? Error) Result(string path, string root)
        => (Path.GetFullPath(path), !SamePath(path, root), null);
}
