using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using MineDock.Core;

internal static class LauncherChecks
{
    public static void Run(string folder, Action<bool, string> check, Action<Action, string> reject, string? officialHmcl)
    {
        var userHome = Environment.GetEnvironmentVariable("HMCL_USER_HOME");
        var localHome = Environment.GetEnvironmentVariable("HMCL_LOCAL_HOME");
        Environment.SetEnvironmentVariable("HMCL_USER_HOME", Path.Combine(folder, "hmcl-user"));
        Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", null);
        try { Verify(folder, check, reject, officialHmcl); }
        finally
        {
            Environment.SetEnvironmentVariable("HMCL_USER_HOME", userHome);
            Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", localHome);
        }
    }

    private static void Verify(string folder, Action<bool, string> check, Action<Action, string> reject, string? officialHmcl)
    {
        static void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
        static string Json(object value) => JsonSerializer.Serialize(value);
        var launcher = Path.Combine(folder, "HMCL.jar");
        Directory.CreateDirectory(folder);
        using (var zip = ZipFile.Open(launcher, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("META-INF/MANIFEST.MF").Open()))
            writer.Write("Manifest-Version: 1.0\r\nMain-Class: org.jackhuang.hmcl.Main\r\n");
        check(LauncherIntegration.Identify(launcher) == LauncherKind.Hmcl, "HMCL JAR identity is read without executing it");
        var impostor = Path.Combine(folder, "PCL.exe");
        File.WriteAllText(impostor, "not a launcher");
        reject(() => LauncherIntegration.Identify(impostor), "A launcher filename alone does not establish identity");
        if (officialHmcl is not null)
        {
            check(LauncherIntegration.Identify(officialHmcl) == LauncherKind.Hmcl, "Official HMCL EXE is recognized by product information");
            var jar = Path.ChangeExtension(officialHmcl, ".jar");
            if (File.Exists(jar))
                check(LauncherIntegration.Identify(jar) == LauncherKind.Hmcl, "Official HMCL JAR manifest is recognized");
        }
        var root = Path.Combine(folder, ".minecraft");
        Directory.CreateDirectory(root);
        check(LauncherIntegration.FindMinecraftRoot(impostor, LauncherKind.Pcl) == root, "PCL automatically selects the sibling .minecraft directory");
        check(LauncherIntegration.FindMinecraftRoot(launcher, LauncherKind.Hmcl) == root, "HMCL automatically selects the sibling .minecraft directory");
        var pclIni = Path.Combine(folder, "PCL", "Setup.ini");
        Write(pclIni, "LaunchFolderSelect:$.minecraft\\\nLaunchArgumentIndieV2:4");
        check(Path.TrimEndingDirectorySeparator(LauncherIntegration.FindMinecraftRoot(impostor, LauncherKind.Pcl)!) == root,
            "PCL portable dollar paths resolve relative to the launcher");
        var customRoot = Path.Combine(folder, "custom-game");
        Directory.CreateDirectory(customRoot);
        Write(pclIni, "LaunchFolderSelect=" + customRoot);
        check(LauncherIntegration.FindMinecraftRoot(impostor, LauncherKind.Pcl) == customRoot,
            "PCL configured game directory takes precedence over a sibling directory");
        Write(pclIni, "LaunchFolderSelect:" + Path.Combine(folder, "missing"));
        reject(() => LauncherIntegration.FindMinecraftRoot(impostor, LauncherKind.Pcl), "A missing PCL configured directory is reported");
        var oldSettings = JsonSerializer.Deserialize<Settings>(Json(new { PclExecutable = impostor, MinecraftRoot = root }))!;
        check(oldSettings.Launcher == LauncherKind.Pcl && oldSettings.LauncherExecutable == impostor,
            "Existing PCL associations remain compatible with old settings files");

        var id = "renamed-fabric";
        var version = Path.Combine(root, "versions", id);
        Write(Path.Combine(version, id + ".json"),
            """{"id":"renamed-fabric","clientVersion":"1.21.1","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.0"}]}""");
        Write(Path.Combine(version, "PCL", "Setup.ini"), "VersionArgumentIndieV2:False");
        var settings = new Settings { MinecraftRoot = root, Launcher = LauncherKind.Hmcl, HmclExecutable = launcher };
        check(LauncherIntegration.Discover(settings).Single().GameVersion == "1.21.1",
            "HMCL discovers renamed Fabric instances through standard version metadata");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == root, "An unconfigured HMCL workspace uses the root run directory");
        var config = Path.Combine(folder, "hmcl.json");
        Write(config, Json(new { last = "Custom", configurations = new
        {
            Default = new { gameDir = ".minecraft", global = new { gameDirType = 1 } },
            Custom = new { gameDir = customRoot, global = new { gameDirType = 0 } }
        } }));
        check(LauncherIntegration.FindMinecraftRoot(launcher, LauncherKind.Hmcl) == customRoot,
            "HMCL selected legacy profile takes precedence over a sibling directory");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == version,
            "HMCL resolves the matching profile when a different profile is selected");
        var local = Path.Combine(version, "hmclversion.cfg");
        Write(local, """{"usesGlobal":false,"gameDirType":0}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == root, "HMCL local root setting overrides global isolation");
        Write(local, """{"usesGlobal":true,"gameDirType":0}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == version, "HMCL usesGlobal inherits the matching profile");
        Write(local, """{"usesGlobal":false,"gameDirType":2,"gameDir":"runs/custom"}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == Path.Combine(folder, "runs", "custom"),
            "HMCL relative custom run directories use the launcher workspace");
        Write(local, """{"gameDirType":1}""");
        check(HmclIntegration.Resolve(root, id, launcher).Isolated, "Legacy HMCL files without usesGlobal keep independent settings");
        Write(local, """{"usesGlobal":"invalid","gameDirType":1}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Malformed HMCL inheritance blocks resource installation");
        Write(local, """{"usesGlobal":false,"gameDirType":99}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Unknown HMCL directory modes are blocked");
        Write(local, """{"usesGlobal":false,"gameDirType":2,"gameDir":""}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Empty HMCL custom paths cannot silently select another directory");
        Write(local, "corrupt JSON");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Corrupted HMCL local configuration is reported");
        Write(local, """{"usesGlobal":false,"gameDirType":1}""");
        var unknownRoot = Path.Combine(folder, "unregistered");
        check(HmclIntegration.Resolve(unknownRoot, id, launcher).Error is not null, "Unregistered HMCL roots cannot borrow another profile's policy");
        var before = File.ReadAllBytes(local);
        LauncherIntegration.Discover(settings);
        check(before.SequenceEqual(File.ReadAllBytes(local)), "HMCL configuration remains byte-for-byte unchanged");

        var home = Path.Combine(folder, ".hmcl");
        var currentConfig = Path.Combine(home, "config", "launcher-settings.json");
        var currentDirectories = Path.Combine(home, "config", "game-directories.json");
        var presetsFile = Path.Combine(home, "config", "game-settings.json");
        Write(currentConfig, """{"$schema":"https://schemas.glavo.site/hmcl/launcher-settings/1.0.0","selectedGameDirectory":"main","defaultGameSettingsPreset":"default"}""");
        Write(currentDirectories, """{"$schema":"https://schemas.glavo.site/hmcl/game-directories/1.0.0","directories":[{"id":"main","path":".minecraft","legacyGameSettings":"default"}]}""");
        Write(presetsFile, """{"$schema":"https://schemas.glavo.site/hmcl/game-settings/1.0.0","presets":[{"id":"default","runningDirectory":"","defaultIsolationType":"ALWAYS"},{"id":"custom","runningDirectory":"shared-run"}]}""");
        check(HmclIntegration.FindMinecraftRoot(launcher) == root, "Modern HMCL game directory selection supersedes stale legacy configuration");
        var currentLocal = Path.Combine(version, ".hmcl", "config", "instance-game-settings.json");
        Write(currentLocal, """{"$schema":"https://schemas.glavo.site/hmcl/instance-game-settings/1.0.0","parent":"custom","overrideProperties":[]}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == Path.Combine(folder, "shared-run"),
            "Modern HMCL inherits the instance's explicit parent preset");
        Write(currentLocal, """{"parent":"custom","overrideProperties":["runningDirectory"],"runningDirectory":""}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == version,
            "Modern HMCL blank overridden run directory means version isolation");
        Write(currentLocal, """{"parent":"custom","overrideProperties":[],"runningDirectory":"own-run"}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == Path.Combine(folder, "own-run"),
            "Modern HMCL normalizes nonblank custom paths into local overrides");
        Write(currentLocal, Json(new { parent = "custom", overrideProperties = new[] { "runningDirectory" }, runningDirectory = root }));
        check(HmclIntegration.Resolve(root, id, launcher).Directory == root,
            "Modern HMCL explicit root overrides a custom parent");
        File.Delete(currentLocal);
        File.Delete(local);
        check(HmclIntegration.Resolve(root, id, launcher).Directory == root,
            "Externally installed HMCL versions use actual run policy rather than the HMCL installer isolation hint");
        Write(local, """{"usesGlobal":true,"gameDirType":0}""");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == version,
            "Modern HMCL legacy-instance migration respects an always-isolated legacy preset");
        Write(currentLocal, """{"$schema":"https://schemas.glavo.site/hmcl/instance-game-settings/2.0.0","runningDirectory":"unknown"}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null,
            "Future unsupported HMCL configuration schemas are blocked");
        Write(currentLocal, """{"overrideProperties":"invalid"}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Malformed modern HMCL override lists are blocked");
        Write(currentLocal, """{"overrideProperties":["runningDirectory"]}""");
        before = File.ReadAllBytes(currentLocal);
        check(LauncherIntegration.Discover(settings).Single().GameDirectory == version,
            "Launcher-neutral instance discovery uses HMCL policy instead of a neighboring PCL setting");
        check(before.SequenceEqual(File.ReadAllBytes(currentLocal)), "Modern HMCL configuration is read without migration or rewriting");
        Write(Path.Combine(version, "modpack.json"), "{}");
        Write(currentLocal, "invalid");
        check(HmclIntegration.Resolve(root, id, launcher).Directory == version, "HMCL managed modpacks retain version-directory isolation");
        File.Delete(Path.Combine(version, "modpack.json"));
        Write(currentConfig, """{"selectedGameDirectory":"unknown"}""");
        Write(currentDirectories, """{"directories":[{"id":"main","path":".minecraft"},{"id":"other","path":"custom-game"}]}""");
        reject(() => HmclIntegration.FindMinecraftRoot(launcher), "Ambiguous HMCL directory selection is reported before association");
        Write(currentConfig, """{"selectedGameDirectory":"other"}""");
        check(HmclIntegration.FindMinecraftRoot(launcher) == customRoot, "Modern HMCL selected custom root is resolved automatically");
        settings.Save();
        check(Settings.Load().Launcher == LauncherKind.Hmcl && Settings.Load().HmclExecutable == launcher,
            "HMCL association persists across application restarts");
        Write(currentLocal, """{"parent":"custom","overrideProperties":[]}""");
        Write(presetsFile, """{"presets":"invalid"}""");
        check(HmclIntegration.Resolve(root, id, launcher).Error is not null, "Malformed HMCL preset stores cannot fall back to a guessed root directory");
        Write(presetsFile, """{"presets":[{"id":"custom","runningDirectory":"shared-run"}]}""");
        var alternateHome = Path.Combine(folder, "alternate-home");
        Write(Path.Combine(alternateHome, "config", "launcher-settings.json"), """{"selectedGameDirectory":"alternate"}""");
        Write(Path.Combine(alternateHome, "config", "game-directories.json"), """{"directories":[{"id":"alternate","path":"custom-game"}]}""");
        Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", alternateHome);
        check(HmclIntegration.FindMinecraftRoot(launcher) == customRoot, "HMCL_LOCAL_HOME redirects configuration without changing the relative game path base");
        Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", null);
        Write(currentConfig, """{"selectedGameDirectory":"user"}""");
        Write(currentDirectories, "{}");
        Write(Path.Combine(Environment.GetEnvironmentVariable("HMCL_USER_HOME")!, "config", "user-game-directories.json"),
            Json(new { directories = new[] { new { id = "user", path = customRoot } } }));
        check(HmclIntegration.FindMinecraftRoot(launcher) == customRoot, "Modern HMCL shared user game directories participate in selection");
        Write(currentConfig, """{"$schema":"https://schemas.glavo.site/hmcl/launcher-settings/2.0.0"}""");
        reject(() => HmclIntegration.FindMinecraftRoot(launcher), "Unsupported global HMCL schemas block automatic reassociation");
    }
}
