using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MineDock.App;
using MineDock.Core;

internal static class LauncherFeatureChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static async Task Run(MainWindow window, string folder, Action<bool, string> check)
    {
        var localHome = Environment.GetEnvironmentVariable("HMCL_LOCAL_HOME");
        var userHome = Environment.GetEnvironmentVariable("HMCL_USER_HOME");
        Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", null);
        Environment.SetEnvironmentVariable("HMCL_USER_HOME", Path.Combine(folder, "launcher-user"));
        try { await Verify(window, folder, check); }
        finally
        {
            Environment.SetEnvironmentVariable("HMCL_LOCAL_HOME", localHome);
            Environment.SetEnvironmentVariable("HMCL_USER_HOME", userHome);
        }
    }

    private static async Task Verify(MainWindow window, string folder, Action<bool, string> check)
    {
        async Task Reject(Func<Task> action, string name)
        {
            try { await action(); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OperationCanceledException)
            { check(true, name); return; }
            throw new Exception("FAIL: " + name);
        }
        async Task Install(LauncherPackage package, string target, Stream? content = null, CancellationToken ct = default)
        {
            using var input = content ?? BundledLaunchers.Open(package.FileName);
            await LauncherInstaller.Install(package, input, target, ct);
        }
        var packages = BundledLaunchers.Packages;
        check(packages.Count == 2 && packages.Select(p => p.Kind).Distinct().Count() == 2,
            "Both official PCL and HMCL packages are embedded in the application");
        var work = Path.Combine(folder, "launcher install 空格 & (test)");
        var installed = new Dictionary<LauncherKind, LauncherInstallResult>();
        foreach (var package in packages)
        {
            var target = Path.Combine(work, package.Kind.ToString());
            using var content = BundledLaunchers.Open(package.FileName);
            var result = await LauncherInstaller.Install(package, content, target);
            installed.Add(package.Kind, result);
            await using var file = File.OpenRead(result.Executable);
            check(!result.Reused && LauncherIntegration.Identify(result.Executable) == package.Kind &&
                Convert.ToHexString(await SHA256.HashDataAsync(file)).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.Combine(target, ".minecraft")),
                "Offline installer preserves the official hash and identity: " + package.Kind);
            var settings = new Settings { Launcher = package.Kind };
            if (package.Kind == LauncherKind.Pcl) settings.PclExecutable = result.Executable;
            else settings.HmclExecutable = result.Executable;
            var info = LauncherProcess.StartInfo(settings);
            check(info.FileName == result.Executable && info.WorkingDirectory == target &&
                info.UseShellExecute && info.ArgumentList.Count == 0,
                "EXE launch uses the selected launcher's own working directory without shell arguments: " + package.Kind);
            check(LauncherIntegration.FindMinecraftRoot(result.Executable, package.Kind) == Path.Combine(target, ".minecraft"),
                "Fresh launcher installation is immediately discoverable: " + package.Kind);
            var sentinel = Path.Combine(target, ".minecraft", "keep.txt");
            File.WriteAllText(sentinel, "existing game data");
            var before = File.GetLastWriteTimeUtc(result.Executable);
            using var repeated = BundledLaunchers.Open(package.FileName);
            var reused = await LauncherInstaller.Install(package, repeated, target);
            check(reused.Reused && File.GetLastWriteTimeUtc(result.Executable) == before &&
                File.ReadAllText(sentinel) == "existing game data",
                "Identical installed launchers are reused without changing their files or game data: " + package.Kind);
        }
        var pcl = packages.Single(p => p.Kind == LauncherKind.Pcl);
        var conflicts = Path.Combine(work, "conflict");
        Directory.CreateDirectory(conflicts);
        var existing = Path.Combine(conflicts, pcl.FileName);
        File.WriteAllText(existing, "existing different launcher");
        await Reject(() => Install(pcl, conflicts), "A different existing launcher cannot be silently overwritten");
        check(File.ReadAllText(existing) == "existing different launcher", "Existing conflicting launcher contents remain untouched");
        var badHash = Path.Combine(work, "bad hash");
        await Reject(() => Install(pcl with { Sha256 = new string('0', 64) }, badHash),
            "An embedded payload with an invalid hash cannot be installed");
        check(!File.Exists(Path.Combine(badHash, pcl.FileName)) &&
            !Directory.EnumerateFiles(badHash, ".minedock-*.tmp.exe").Any(),
            "Hash failures leave no executable or temporary payload behind");
        await Reject(() => Install(pcl with { Size = pcl.Size + 1 }, Path.Combine(work, "bad size")),
            "An incorrect payload size is rejected");
        await Reject(() => Install(pcl with { Kind = LauncherKind.Hmcl }, Path.Combine(work, "wrong identity")),
            "A valid payload hash cannot bypass the launcher identity check");
        await Reject(() => Install(pcl with { Kind = LauncherKind.Hmcl }, Path.GetDirectoryName(installed[LauncherKind.Pcl].Executable)!),
            "Reusing an installed file also requires the expected launcher identity");
        await Reject(() => Install(pcl with { FileName = "../escaped.exe" }, work),
            "Launcher package filenames cannot escape the selected directory");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            var canceled = Path.Combine(work, "canceled");
            await Reject(() => Install(pcl, canceled, ct: cancel.Token), "Pre-canceled installation does not write files");
            check(!Directory.Exists(canceled), "Pre-canceled installation does not create the destination");
        }
        using (var cancel = new CancellationTokenSource())
        {
            var canceled = Path.Combine(work, "cancel during copy");
            await Reject(() => Install(pcl, canceled, new CancelStream(BundledLaunchers.Open(pcl.FileName), cancel), cancel.Token),
                "Cancellation during payload copying aborts the installation");
            check(!File.Exists(Path.Combine(canceled, pcl.FileName)) &&
                !Directory.EnumerateFiles(canceled, ".minedock-*.tmp.exe").Any(),
                "Cancellation cleans up its temporary payload");
        }
        await Reject(() => Task.FromResult(LauncherProcess.StartInfo(new Settings())),
            "Unassociated launchers cannot be started");
        await Reject(() => Task.FromResult(LauncherProcess.StartInfo(new Settings
            { Launcher = LauncherKind.Hmcl, HmclExecutable = installed[LauncherKind.Pcl].Executable })),
            "A launcher whose identity differs from the saved association cannot be started");
        var jar = Path.Combine(work, "HMCL 空格 & (test).jar");
        using (var archive = ZipFile.Open(jar, ZipArchiveMode.Create))
        using (var text = new StreamWriter(archive.CreateEntry("META-INF/MANIFEST.MF").Open()))
            text.Write("Main-Class: org.jackhuang.hmcl.Main\r\n");
        var jarSettings = new Settings { Launcher = LauncherKind.Hmcl, HmclExecutable = jar };
        var java = Environment.GetEnvironmentVariable("MINEDOCK_TEST_JAVA");
        if (java is not null)
        {
            check(LauncherProcess.IsSupportedJava(java), "Java runtime selected for the JAR launch check is supported");
            var info = LauncherProcess.StartInfo(jarSettings, java);
            check(info.FileName == java && !info.UseShellExecute && info.CreateNoWindow &&
                info.WorkingDirectory == work && info.ArgumentList.SequenceEqual(new[] { "-jar", jar }),
                "HMCL JAR launch quotes paths through ArgumentList and uses its own working directory");
            jarSettings.LauncherJavaExecutable = java;
            check(LauncherProcess.FindJava(jarSettings) == java, "The selected JAR Java runtime is rediscovered without changing PATH");
        }
        await Reject(() => Task.FromResult(LauncherProcess.StartInfo(jarSettings, existing)),
            "An unrelated executable cannot be selected as the HMCL JAR Java runtime");

        var apply = typeof(MainWindow).GetMethod("ApplyLauncher", Private)!;
        apply.Invoke(window, [installed[LauncherKind.Pcl].Executable, LauncherKind.Pcl]);
        var launch = (Button)window.FindName("LaunchLauncherButton");
        var install = (Button)window.FindName("InstallLaunchersButton");
        check(launch.IsEnabled && install.IsEnabled && window.Vm.LaunchLauncherLabel == "启动 PCL",
            "Associated launchers enable the correctly named launch button and installer entry");
        window.Vm.Running = true;
        check(!launch.IsEnabled && !install.IsEnabled, "Active content installations disable launcher launch and installation");
        window.Vm.Running = false;
        window.Vm.LauncherStarting = true;
        check(!launch.IsEnabled && !install.IsEnabled, "Launcher startup prevents duplicate clicks and concurrent association changes");
        window.Vm.LauncherStarting = false;
        await CheckManager(window, folder, packages, check);
        var selected = packages.Single(p => p.Kind == LauncherKind.Hmcl);
        var modalTarget = Path.Combine(work, "modal workflow");
        _ = window.Dispatcher.BeginInvoke(new Action(() =>
        {
            var manager = Application.Current.Windows.OfType<LauncherManagerWindow>().Single();
            manager.Vm.Directory = modalTarget;
            var button = Descendants<Button>(manager).Single(b =>
                AutomationProperties.GetName(b) == "安装并关联 HMCL");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }), DispatcherPriority.Background);
        install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(window.Vm.Settings.Launcher == LauncherKind.Hmcl &&
            window.Vm.Settings.HmclExecutable == Path.Combine(modalTarget, selected.FileName) &&
            window.Vm.Settings.MinecraftRoot == Path.Combine(modalTarget, ".minecraft") &&
            window.Vm.LaunchLauncherLabel == "启动 HMCL" && Settings.Load().HmclExecutable == window.Vm.Settings.HmclExecutable,
            "The actual modal install button installs HMCL, closes the dialog, associates it, and saves the detected game root");
        foreach (var theme in new[] { "Dark", "Light" })
        {
            App.ApplyTheme(theme);
            foreach (var size in new[] { (1340d, 860d), (1080d, 740d) })
            {
                window.Width = size.Item1; window.Height = size.Item2;
                await Task.Delay(100);
                window.UpdateLayout();
                var navigation = (RadioButton)window.FindName("NavTasks");
                var navBounds = navigation.TransformToAncestor(window).TransformBounds(new Rect(navigation.RenderSize));
                var association = (Button)window.FindName("LauncherButton");
                var associationBounds = association.TransformToAncestor(window).TransformBounds(new Rect(association.RenderSize));
                var bounds = launch.TransformToAncestor(window).TransformBounds(new Rect(launch.RenderSize));
                check(navBounds.Bottom <= associationBounds.Top && bounds.Bottom <= window.ActualHeight &&
                    launch.ActualWidth >= 110 && install.ActualWidth == 36,
                    $"Launcher controls fit without navigation overlap: {theme}, {size.Item1}x{size.Item2}");
                Capture(window, Path.Combine(folder, $"launchers-main-{theme}-{size.Item1}.png"));
            }
        }
        var notices = new NoticesWindow { Left = -30000, Top = -30000, ShowInTaskbar = false };
        try
        {
            notices.Show();
            var choice = (ComboBox)notices.FindName("NoticeChoice");
            var text = (TextBox)notices.FindName("NoticeText");
            foreach (var item in choice.Items)
            {
                choice.SelectedItem = item;
                check(text.Text.Length > 40 && text.Text != "许可文件未找到。",
                    "Embedded notice is readable: " + ((NoticesWindow.NoticeFile)item).Label);
            }
            choice.SelectedIndex = 0;
            Capture(notices, Path.Combine(folder, "launchers-notices.png"));
        }
        finally { notices.Close(); }
        var exported = Path.Combine(folder, "HMCL-corresponding-source.zip");
        await BundledLaunchers.Export("HMCL-3.16.4-corresponding-source.zip", exported);
        using var sources = ZipFile.OpenRead(exported);
        check(sources.Entries.Any(e => e.Name == "HMCL-source-3.16.4.zip") &&
            sources.Entries.Any(e => e.Name.StartsWith("HMCLauncher-source-")) &&
            sources.GetEntry("README.md") is not null && sources.GetEntry("HMCL-LICENSE.txt") is not null,
            "The offline source export includes HMCL, its Windows wrapper, license, and build/source provenance");
        App.ApplyTheme("Dark");
    }

    private static async Task CheckManager(MainWindow owner, string folder, IReadOnlyList<LauncherPackage> packages,
        Action<bool, string> check)
    {
        var manager = new LauncherManagerWindow("") { Owner = owner, Left = -30000, Top = -30000, ShowInTaskbar = false };
        try
        {
            manager.Show();
            foreach (var theme in new[] { "Dark", "Light" })
            {
                App.ApplyTheme(theme);
                manager.Width = 620; manager.Height = 550;
                await Task.Delay(100);
                manager.UpdateLayout();
                var buttons = Descendants<Button>(manager).Where(b => b.Tag is LauncherPackage).ToArray();
                check(buttons.Length == 2 && buttons.All(b => b.ActualWidth >= 100 &&
                    b.TransformToAncestor(manager).TransformBounds(new Rect(b.RenderSize)).Right <= manager.ActualWidth),
                    "Both offline launcher actions fit the minimum installer window: " + theme);
                Capture(manager, Path.Combine(folder, "launcher-installer-" + theme + ".png"));
            }
            manager.Vm.Directory = Path.Combine(folder, "manager offline");
            await (Task)typeof(LauncherManagerWindow).GetMethod("InstallPackage", Private)!.Invoke(manager, [packages[0]])!;
            check(manager.Installed is { } result && File.Exists(result.Executable) && !manager.Vm.Busy,
                "The WPF installer completes an embedded package without a download service");
        }
        finally { manager.Close(); App.ApplyTheme("Dark"); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path);
        encoder.Save(output);
    }
    private sealed class CancelStream(Stream inner, CancellationTokenSource cancel) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            cancel.Cancel();
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
