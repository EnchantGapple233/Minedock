using System.IO;
using System.Reflection;
using MineDock.Core;

namespace MineDock.App;

public static class LoaderRuntime
{
    public static async Task Ensure(CancellationToken ct = default)
    {
        var folder = Path.Combine(Settings.DataDirectory, "loader-engine");
        Directory.CreateDirectory(folder);
        var jar = Path.Combine(folder, "HMCL.jar");
        var expected = new PlannedFile { HashAlgorithm = "SHA256",
            Hash = "98f0123f38e86a606aa583f74d21ae74128679e921ea7ec0c43ab26303767e9e", Size = 10453287 };
        if (!File.Exists(jar) || !await Network.Valid(jar, expected, ct))
        {
            var temporary = jar + ".tmp";
            try
            {
                using var input = BundledLaunchers.Open("HMCL.exe");
                var remaining = 454144;
                var buffer = new byte[81920];
                while (remaining > 0)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
                    if (read == 0) throw new InvalidDataException("内置 HMCL 数据不完整。");
                    remaining -= read;
                }
                await using (var output = File.Create(temporary)) await input.CopyToAsync(output, ct);
                if (!await Network.Valid(temporary, expected, ct)) throw new InvalidDataException("内置 HMCL 引擎校验失败。");
                File.Move(temporary, jar, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "MineDock.App.Assets.Installer.MineDockBridge.java") ?? throw new FileNotFoundException("缺少安装桥接源码。");
        using var reader = new StreamReader(source);
        await File.WriteAllTextAsync(Path.Combine(folder, "MineDockBridge.java"), await reader.ReadToEndAsync(ct), ct);
    }
}
