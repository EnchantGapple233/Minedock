using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MineDock.App;

public partial class NoticesWindow : Window
{
    public NoticesWindow()
    {
        InitializeComponent();
        var assembly = Assembly.GetExecutingAssembly();
        var files = new List<NoticeFile>
        {
            new("第三方声明与来源", "MineDock.App.Notices.THIRD-PARTY-NOTICES.md"),
            new("启动器版本、校验与源码来源", "MineDock.App.Assets.Launchers.SOURCES.md"),
            new("MineDock · GPL-3.0-or-later", "MineDock.App.Notices.MineDock-LICENSE.txt"),
            new("PCL · 分发许可", "MineDock.App.Assets.Launchers.PCL-DISTRIBUTION.txt"),
            new("PCL · 仓库许可", "MineDock.App.Assets.Launchers.PCL-LICENCE.txt"),
            new("HMCL · GPLv3 与附加条款", "MineDock.App.Assets.Launchers.HMCL-LICENSE.txt"),
            new("HMCL · 项目说明与附加条款", "MineDock.App.Assets.Launchers.HMCL-README.md"),
            new("HMCLauncher · 许可", "MineDock.App.Assets.Launchers.HMCLauncher-LICENSE.txt"),
            new("HMCLauncher · 项目说明与附加条款", "MineDock.App.Assets.Launchers.HMCLauncher-README.md"),
            new("版本更新日志", "MineDock.App.Notices.CHANGELOG.md"),
            new("Lucide · ISC / MIT", "MineDock.App.Assets.Icons.LICENSE")
        };
        files.AddRange(assembly.GetManifestResourceNames().Where(n => n.StartsWith("MineDock.App.Assets.Licenses."))
            .Select(n => new NoticeFile(n["MineDock.App.Assets.Licenses.".Length..], n)));
        NoticeChoice.ItemsSource = files;
        NoticeChoice.SelectedIndex = 0;
    }

    private void SelectNotice(object sender, SelectionChangedEventArgs e)
    {
        if (NoticeText is null || NoticeChoice.SelectedItem is not NoticeFile selected) return;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(selected.Resource);
        if (stream is null) { NoticeText.Text = "许可文件未找到。"; return; }
        using var reader = new StreamReader(stream);
        NoticeText.Text = reader.ReadToEnd();
        NoticeText.ScrollToHome();
    }

    private async void ExportHmclSource(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Title = "导出随附的 HMCL 对应源码",
            FileName = "HMCL-3.16.4-corresponding-source.zip", Filter = "源码 ZIP|*.zip" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            ((Button)sender).IsEnabled = false;
            await BundledLaunchers.Export("HMCL-3.16.4-corresponding-source.zip", picker.FileName);
            NoticeStatus.Text = "已导出：" + picker.FileName;
        }
        catch (Exception ex) { NoticeStatus.Text = ex.Message; }
        finally { ((Button)sender).IsEnabled = true; }
    }

    private void CloseNotices(object sender, RoutedEventArgs e) => Close();
    public record NoticeFile(string Label, string Resource);
}
