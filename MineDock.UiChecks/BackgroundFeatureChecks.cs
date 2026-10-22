using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MineDock.App;
using MineDock.Core;
using SkiaSharp;

internal static class BackgroundFeatureChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static async Task Run(Func<MainWindow> create, string folder, Action<bool, string> check)
    {
        var passed = new List<string>();
        void Check(bool condition, string label)
        {
            check(condition, label);
            passed.Add(label);
        }
        new Settings { AutomaticUpdates = false, Theme = "Dark" }.Save();
        App.ApplyTheme("Dark");
        var window = create();
        Check(!window.Vm.HasBackground, "Existing settings without background keep default appearance");
        var original = Path.Combine(folder, "background-original.jpg");
        using var bitmap = new SKBitmap(96, 64);
        bitmap.Erase(new SKColor(36, 118, 84));
        using var image = SKImage.FromBitmap(bitmap);
        using (var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90))
            File.WriteAllBytes(original, jpeg.ToArray());
        var originalHash = SHA256.HashData(File.ReadAllBytes(original));
        foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Webp })
        {
            using var encoded = image.Encode(format, 90);
            var path = Path.Combine(folder, "background-fixture." + format.ToString().ToLowerInvariant());
            File.WriteAllBytes(path, encoded.ToArray());
            await Import(window, path);
            Check(window.Vm.HasBackground && !window.Vm.BackgroundBusy, format + " import and decode");
        }
        var bmp = Path.Combine(folder, "background-fixture.bmp");
        var bmpEncoder = new BmpBitmapEncoder();
        bmpEncoder.Frames.Add(BitmapFrame.Create((BitmapSource)window.Vm.BackgroundImage!));
        using (var stream = File.Create(bmp)) bmpEncoder.Save(stream);
        await Import(window, bmp);
        Check(window.Vm.HasBackground, "BMP import");
        await Import(window, original);
        using (File.Open(original, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        File.Move(original, original + ".moved", true);
        Check(SHA256.HashData(File.ReadAllBytes(original + ".moved")).SequenceEqual(originalHash),
            "Original image remains unchanged and unlocked");
        var slider = Named<Slider>(window, "BackgroundIntensitySlider");
        slider.Value = 0.8;
        Named<ComboBox>(window, "BackgroundStretchCombo").SelectedValue = "Uniform";
        await Task.Delay(100);
        var saved = Settings.Load();
        Check(Math.Abs(saved.BackgroundIntensity - 0.8) < 0.001 && saved.BackgroundStretch == "Uniform",
            "Slider and layout persist immediately");
        var reopened = create();
        Check(reopened.Vm.HasBackground && reopened.Vm.BackgroundName == Path.GetFileName(original),
            "Reopening restores cached image after original was moved");
        Check(Math.Abs(reopened.Vm.BackgroundIntensity - 0.8) < 0.001 && reopened.Vm.BackgroundStretch == "Uniform",
            "Reopening restores intensity and layout");
        var control = Named<Image>(window, "WindowBackground");
        Check(control.Source is not null && !control.IsHitTestVisible && Math.Abs(control.Opacity - 0.208) < 0.001,
            "Background binds to image and intensity without intercepting controls");
        Named<Button>(window, "BackgroundSettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var popup = Named<Popup>(window, "BackgroundPopup");
        await Task.Delay(200);
        Check(popup.IsOpen && popup.Child is FrameworkElement { ActualWidth: > 300, ActualHeight: > 300 },
            "Title bar image button opens background settings");
        Capture((FrameworkElement)popup.Child!, folder, "background-settings-dark.png");
        popup.IsOpen = false;
        Capture(window, folder, "background-dark.png");
        slider.Value = 0;
        await Task.Delay(50);
        Check(control.Opacity == 0, "Zero intensity hides background");
        slider.Value = 1;
        Check(Math.Abs(control.Opacity - 0.26) < 0.001, "Maximum dark intensity retains text contrast");
        App.ApplyTheme("Light");
        await Task.Delay(100);
        Check(Math.Abs(control.Opacity - 0.15) < 0.001, "Light theme adapts background intensity");
        Capture(window, folder, "background-light.png");
        popup.IsOpen = true;
        await Task.Delay(200);
        Capture((FrameworkElement)popup.Child!, folder, "background-settings-light.png");
        popup.IsOpen = false;
        window.Width = 1080;
        window.Height = 740;
        await Task.Delay(100);
        Capture(window, folder, "background-compact.png");
        popup.IsOpen = true;
        await Task.Delay(100);
        Check(((FrameworkElement)popup.Child!).DesiredSize.Width < window.ActualWidth &&
              ((FrameworkElement)popup.Child).DesiredSize.Height < window.ActualHeight - 42,
            "Settings panel fits minimum window size");
        popup.IsOpen = false;
        var before = window.Vm.Settings.BackgroundImageFile;
        var invalid = Path.Combine(folder, "background-invalid.png");
        File.WriteAllText(invalid, "This is not an image.");
        var rejected = false;
        try { await Import(window, invalid); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && window.Vm.Settings.BackgroundImageFile == before && window.Vm.HasBackground && !window.Vm.BackgroundBusy,
            "Corrupt image preserves existing background and resets busy state");
        typeof(MainWindow).GetMethod("ResetBackground", Private)!.Invoke(window, [null, new RoutedEventArgs()]);
        Check(!window.Vm.HasBackground && Settings.Load().BackgroundImageFile == "" && window.Vm.BackgroundIntensity == 0.55,
            "Restore default clears image and persists defaults");
        var reset = create();
        Check(!reset.Vm.HasBackground, "Reopening after reset keeps default background");
        var missing = Settings.Load();
        missing.BackgroundImageFile = "background-" + new string('0', 64) + ".png";
        missing.Save();
        var unavailable = create();
        Check(!unavailable.Vm.HasBackground && unavailable.Vm.BackgroundError.Length > 0,
            "Missing cache falls back safely and offers reselect");
        missing.BackgroundImageFile = "../outside.png";
        missing.Save();
        var traversal = create();
        Check(!traversal.Vm.HasBackground && traversal.Vm.BackgroundError.Length > 0,
            "Cache path traversal is rejected");
        new Settings { AutomaticUpdates = false, Theme = "Dark" }.Save();
        App.ApplyTheme("Dark");
        Capture(reset, folder, "background-reset.png");
        File.WriteAllText(Path.Combine(folder, "background-checks.json"), JsonSerializer.Serialize(
            new { Passed = passed.Count, Checks = passed, ExitCode = 0 }, JsonTools.Options));
    }

    private static T Named<T>(MainWindow window, string name) => (T)window.FindName(name);
    private static async Task Import(MainWindow window, string file)
        => await (Task)typeof(MainWindow).GetMethod("ImportBackground", Private)!.Invoke(window, [file])!;

    private static void Capture(FrameworkElement element, string folder, string file)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(folder, file));
        encoder.Save(output);
    }
}
