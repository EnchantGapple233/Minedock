using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow
{
    private readonly CancellationTokenSource _updateLifetime = new();
    private DispatcherTimer? _updateTimer;
    private CancellationTokenSource? _updateCheck;
    private bool _updateChecking, _applyingUpdate, _manualUpdate;
    private AppUpdate? _availableUpdate;
    private string? _updatePayload;
    private void StartUpdates()
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _updateTimer.Tick += async (_, _) => { if (Vm.AutomaticUpdates) await CheckUpdates(false); };
        _updateTimer.Start();
        if (Vm.AutomaticUpdates) _ = CheckUpdates(false);
    }
    private async void CheckForUpdates(object sender, RoutedEventArgs e) => await CheckUpdates(true);
    private async Task CheckUpdates(bool manual)
    {
        if (_updateChecking) return;
        _updateChecking = true;
        _manualUpdate = manual;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
        _updateCheck = cancellation;
        var ct = cancellation.Token;
        try
        {
            Vm.UpdateStatus = "正在检查 MineDock 更新…";
            using var network = new Network { Source = SourceMode.Official };
            var service = new UpdateService(network);
            var update = await service.Check(new Version(UpdateService.CurrentVersion), ct);
            if (update is null) { Vm.UpdateStatus = "MineDock " + UpdateService.CurrentVersion + " · 已是最新正式版"; return; }
            _availableUpdate = update;
            Vm.UpdateStatus = $"正在下载 MineDock {update.Version}…";
            _updatePayload = await service.Download(update, ct);
            Vm.UpdateStatus = $"MineDock {update.Version} 已就绪，空闲时自动重启";
            // Leave the status visible and allow automatic updates to be disabled before restart.
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            while (!CanApplyUpdate() && !ct.IsCancellationRequested)
            {
                if (!Vm.AutomaticUpdates && !manual) return;
                await Task.Delay(1000, ct);
            }
            if (Vm.AutomaticUpdates || manual) await ApplyUpdate(manual, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Vm.UpdateStatus = "更新未完成：" + ex.Message;
            if (manual) Vm.Error = Vm.UpdateStatus;
        }
        finally { _updateCheck = null; _updateChecking = false; _manualUpdate = false; }
    }
    private bool CanApplyUpdate() => Vm.CanPrepare && !Vm.ShowPlan && !Vm.BackgroundBusy && !Vm.Loading &&
        !Vm.GameListLoading && !Vm.Refreshing && !Vm.LoaderLoading && !BackgroundPopup.IsOpen &&
        UpdateButton.ContextMenu?.IsOpen != true && OwnedWindows.Count == 0 &&
        !System.Windows.Interop.ComponentDispatcher.IsThreadModal && !_applyingUpdate;
    private static bool IsSingleFile()
    {
#if MINEDOCK_SINGLE_FILE
        return true;
#else
        return false;
#endif
    }
    private async Task ApplyUpdate(bool manual, CancellationToken ct)
    {
        if (_availableUpdate is null || _updatePayload is null || !CanApplyUpdate()) return;
        if (!IsSingleFile())
        {
            Vm.UpdateStatus = "此构建为开发版本；请使用发行版单 EXE 完成自动替换。";
            return;
        }
        var executable = Environment.ProcessPath ?? throw new IOException("无法定位当前程序。");
        var request = await UpdateService.CreateRequest(executable, _availableUpdate, _updatePayload, ct);
        if (!CanApplyUpdate() || (!manual && !Vm.AutomaticUpdates)) return;
        var helper = Path.Combine(Path.GetDirectoryName(request)!, "MineDock.Update.exe");
        File.Copy(executable, helper);
        var info = new ProcessStartInfo(helper) { UseShellExecute=false, CreateNoWindow=true };
        info.ArgumentList.Add("--apply-update"); info.ArgumentList.Add(request);
        using var process = Process.Start(info) ?? throw new IOException("无法启动更新助手。");
        _applyingUpdate = true;
        Close();
    }
    private void AutomaticUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Vm.Settings.Save();
        if (!Vm.AutomaticUpdates)
        {
            if (!_manualUpdate) _updateCheck?.Cancel();
            Vm.UpdateStatus = "自动更新已关闭";
        }
        else if (!_updateChecking) _ = CheckUpdates(false);
    }
    private void OpenUpdateMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.DataContext = Vm;
        menu.IsOpen = true;
    }
    private void StopUpdates()
    {
        _updateTimer?.Stop();
        _updateLifetime.Cancel();
    }
}
