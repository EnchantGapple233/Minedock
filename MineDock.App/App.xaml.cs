using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using MineDock.Core;

namespace MineDock.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Settings.DataDirectory).ToUpperInvariant())))[..24];
        _instanceMutex = new Mutex(false, @"Local\MineDock-" + identity);
        var acquired = false;
        try { acquired = _instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            _instanceMutex.Dispose(); _instanceMutex = null;
            MessageBox.Show("MineDock 已在运行。请使用已打开的窗口完成安装。", "MineDock", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        Directory.CreateDirectory(Settings.DataDirectory);
        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(Path.Combine(Settings.DataDirectory, "errors.log"), DateTime.Now + "\n" + args.Exception + "\n");
            MessageBox.Show(args.Exception.Message, "MineDock", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        ApplyTheme(Settings.Load().Theme);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
    public static void ApplyTheme(string theme)
    {
        var dark = theme == "Dark" || (theme == "System" &&
            (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) == 0);
        var colors = dark
            ? new[] { "#111417", "#161A1E", "#1D2228", "#252C33", "#2B333C", "#E8EDF1", "#9DA8B4", "#7AE0B3", "#153A30", "#10271E", "#F3BE70" }
            : new[] { "#F5F7F8", "#EDF1F3", "#FFFFFF", "#E6EBEF", "#D4DDE3", "#17232C", "#61717F", "#167B55", "#E0F1E9", "#FFFFFF", "#A56513" };
        var names = new[] { "Background", "Sidebar", "Surface", "Raised", "Border", "Text", "Muted", "Accent", "AccentSoft", "AccentInk", "Warning" };
        for (var i = 0; i < names.Length; i++)
            Current.Resources[names[i] + "Brush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
}
