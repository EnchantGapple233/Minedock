using System.IO;
using System.Reflection;
using System.Text.Json;
using MineDock.Core;

namespace MineDock.App;

public static class BundledLaunchers
{
    private const string Prefix = "MineDock.App.Assets.Launchers.";
    public static IReadOnlyList<LauncherPackage> Packages { get; } = ReadPackages();

    private static List<LauncherPackage> ReadPackages()
    {
        using var stream = Open("launcher-packages.json");
        return JsonSerializer.Deserialize<List<LauncherPackage>>(stream, JsonTools.Options)
            ?? throw new InvalidDataException("内置启动器清单为空。");
    }

    public static Stream Open(string name)
        => Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + name)
            ?? throw new FileNotFoundException("缺少内置启动器资源：" + name);

    public static async Task Export(string name, string destination)
    {
        using var input = Open(name);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await input.CopyToAsync(output);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
