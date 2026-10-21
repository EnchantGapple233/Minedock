using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow
{
    private void ManageLaunchers(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        var manager = new LauncherManagerWindow(Vm.Settings.MinecraftRoot) { Owner = this };
        if (manager.ShowDialog() != true || manager.Installed is not { } result) return;
        _ = Guard(() =>
        {
            ApplyLauncher(result.Executable, LauncherIntegration.Identify(result.Executable));
            Vm.Status = result.Reused ? $"已关联现有 {Vm.Settings.LauncherName}" : $"{Vm.Settings.LauncherName} 已安装并关联";
            return Task.CompletedTask;
        });
    }

    private async void LaunchAssociatedLauncher(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanLaunchLauncher) return;
        Vm.LauncherStarting = true;
        try
        {
            await Guard(async () =>
            {
                string? java = null;
                if (Path.GetExtension(Vm.Settings.LauncherExecutable).Equals(".jar", StringComparison.OrdinalIgnoreCase))
                {
                    java = LauncherProcess.FindJava(Vm.Settings);
                    if (java is null)
                    {
                        var picker = new OpenFileDialog { Title = "为 HMCL JAR 选择 Java 17 或更高版本",
                            Filter = "Java 运行程序|javaw.exe;java.exe" };
                        if (picker.ShowDialog(this) != true) return;
                        if (!LauncherProcess.IsSupportedJava(picker.FileName))
                            throw new InvalidOperationException("请选择 Java 17 或更高版本的 javaw.exe 或 java.exe。");
                        java = Vm.Settings.LauncherJavaExecutable = picker.FileName;
                        Vm.Settings.Save();
                    }
                }
                using var process = Process.Start(LauncherProcess.StartInfo(Vm.Settings, java))
                    ?? throw new IOException("启动器未能启动，请检查文件与系统权限。");
                Vm.Status = $"已打开 {Vm.Settings.LauncherName}";
                await Task.Delay(700);
            });
        }
        finally { Vm.LauncherStarting = false; }
    }

    private void OpenNotices(object sender, RoutedEventArgs e)
        => new NoticesWindow { Owner = this }.ShowDialog();
}
