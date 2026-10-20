using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow
{
    private void RestoreBackground()
    {
        Vm.Settings.BackgroundIntensity = double.IsFinite(Vm.Settings.BackgroundIntensity)
            ? Math.Clamp(Vm.Settings.BackgroundIntensity, 0, 1) : 0.55;
        Vm.BackgroundStretch = Vm.Settings.BackgroundStretch;
        try { Vm.BackgroundImage = BackgroundImages.Load(Vm.Settings.BackgroundImageFile); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Vm.BackgroundError = "已保存的背景无法读取，请重新选择图片。"; }
        Vm.Changed(nameof(Vm.BackgroundName));
        RefreshBackgroundTheme();
    }
    internal void RefreshBackgroundTheme()
    {
        if (Vm.HasBackground)
            Resources["MutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(App.IsDarkTheme ? "#B8C3CD" : "#465664"));
        else Resources.Remove("MutedBrush");
        Vm.Changed(nameof(Vm.BackgroundOpacity));
    }
    private void OpenBackgroundSettings(object sender, RoutedEventArgs e) => BackgroundPopup.IsOpen = !BackgroundPopup.IsOpen;
    private void CloseBackgroundSettings(object sender, RoutedEventArgs e) => BackgroundPopup.IsOpen = false;
    private async void ChooseBackground(object sender, RoutedEventArgs e)
    {
        if (Vm.BackgroundBusy) return;
        var dialog = new OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) { BackgroundPopup.IsOpen = true; return; }
        BackgroundPopup.IsOpen = true;
        try { await ImportBackground(dialog.FileName); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Vm.BackgroundError = "无法设置背景：" + ex.Message; }
    }
    private async Task ImportBackground(string path)
    {
        Vm.BackgroundBusy = true; Vm.BackgroundError = "";
        try
        {
            var imported = await Task.Run(() => BackgroundImages.Import(path));
            var oldFile = Vm.Settings.BackgroundImageFile;
            var oldName = Vm.Settings.BackgroundImageName;
            Vm.Settings.BackgroundImageFile = imported.File;
            Vm.Settings.BackgroundImageName = imported.Name;
            try { Vm.Settings.Save(); }
            catch
            {
                Vm.Settings.BackgroundImageFile = oldFile;
                Vm.Settings.BackgroundImageName = oldName;
                throw;
            }
            Vm.BackgroundImage = imported.Image;
            Vm.Changed(nameof(Vm.BackgroundName));
            RefreshBackgroundTheme();
        }
        finally { Vm.BackgroundBusy = false; }
    }
    private void ResetBackground(object sender, RoutedEventArgs e)
    {
        if (Vm.BackgroundBusy) return;
        Vm.Settings.BackgroundImageFile = "";
        Vm.Settings.BackgroundImageName = "";
        Vm.BackgroundImage = null; Vm.BackgroundError = "";
        Vm.BackgroundIntensity = 0.55; Vm.BackgroundStretch = "UniformToFill";
        Vm.Changed(nameof(Vm.BackgroundName));
        RefreshBackgroundTheme();
        SaveBackgroundSettings();
    }
    private void BackgroundIntensityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || Vm.BackgroundBusy) return;
        Vm.BackgroundIntensity = e.NewValue;
        SaveBackgroundSettings();
    }
    private void BackgroundStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || Vm.BackgroundBusy) return;
        if (sender is ComboBox { SelectedValue: string stretch }) Vm.BackgroundStretch = stretch;
        SaveBackgroundSettings();
    }
    private void SaveBackgroundSettings()
    {
        try { Vm.Settings.Save(); Vm.BackgroundError = ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Vm.BackgroundError = "背景设置保存失败：" + ex.Message; }
    }
}
