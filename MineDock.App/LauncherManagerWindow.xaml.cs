using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MineDock.Core;

namespace MineDock.App;

public partial class LauncherManagerWindow : Window
{
    public LauncherManagerViewModel Vm { get; }
    public LauncherInstallResult? Installed { get; private set; }
    private CancellationTokenSource? _operation;
    private bool _closeRequested;

    public LauncherManagerWindow(string minecraftRoot)
    {
        var folder = minecraftRoot.Length > 0 &&
            Path.GetFileName(Path.TrimEndingDirectorySeparator(minecraftRoot)).Equals(".minecraft", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(minecraftRoot))!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Minecraft");
        Vm = new(folder);
        DataContext = Vm;
        InitializeComponent();
    }

    private void ChooseDirectory(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "选择启动器安装目录",
            InitialDirectory = Directory.Exists(Vm.Directory) ? Vm.Directory : "" };
        if (picker.ShowDialog(this) == true) Vm.Directory = picker.FolderName;
    }

    private async void InstallLauncher(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanInstall || sender is not Button { Tag: LauncherPackage package }) return;
        await InstallPackage(package);
        if (Installed is not null) DialogResult = true;
        else if (_closeRequested) Close();
    }

    internal async Task InstallPackage(LauncherPackage package)
    {
        if (!Vm.CanInstall) return;
        _operation = new();
        Vm.Busy = true;
        Vm.Status = "正在校验并安装 " + (package.Kind == LauncherKind.Pcl ? "PCL" : "HMCL") + "…";
        try
        {
            using var payload = BundledLaunchers.Open(package.FileName);
            Installed = await LauncherInstaller.Install(package, payload, Vm.Directory, _operation.Token);
            Vm.Status = Installed.Reused ? "已核对并复用现有启动器。" : "启动器安装完成。";
        }
        catch (OperationCanceledException) { Vm.Status = "安装已取消。"; }
        catch (Exception ex) { Vm.Status = ex.Message; }
        finally { Vm.Busy = false; _operation.Dispose(); _operation = null; }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!Vm.Busy) return;
        _closeRequested = true;
        _operation?.Cancel();
        e.Cancel = true;
    }
    private void CloseManager(object sender, RoutedEventArgs e) => Close();
    private void ShowLicenses(object sender, RoutedEventArgs e) => new NoticesWindow { Owner = this }.ShowDialog();
}

public sealed class LauncherManagerViewModel(string directory) : Observable
{
    private string _directory = directory, _status = "";
    private bool _busy;
    public string Directory
    {
        get => _directory;
        set { if (Set(ref _directory, value)) Changed(nameof(GameDirectory)); }
    }
    public string GameDirectory => "游戏目录 · " + Path.Combine(Directory.Length > 0 ? Directory : "…", ".minecraft");
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool Busy { get => _busy; set { if (Set(ref _busy, value)) Changed(nameof(CanInstall)); } }
    public bool CanInstall => !Busy;
    public LauncherPackageRow[] Packages { get; } = BundledLaunchers.Packages.Select(p => new LauncherPackageRow(p)).ToArray();
}

public record LauncherPackageRow(LauncherPackage Package)
{
    public string Name => Package.Kind == LauncherKind.Pcl ? "Plain Craft Launcher" : "Hello Minecraft! Launcher";
    public string VersionLabel => Package.Version + "  ·  " + JsonTools.Size(Package.Size) + "  ·  官方原版";
    public string License => Package.Kind == LauncherKind.Pcl ? "PCL 分发有限许可" : "GPLv3 与附加条款";
    public string InstallLabel => "安装并关联 " + (Package.Kind == LauncherKind.Pcl ? "PCL" : "HMCL");
}
