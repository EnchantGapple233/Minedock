using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MineDock.App;
using MineDock.Core;

internal static class InterfaceFeatureChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static async Task Run(Func<MainWindow> create, string folder, Action<bool, string> check)
    {
        new Settings { AutomaticUpdates = false, Source = SourceMode.Official, Theme = "Dark" }.Save();
        var window = create();
        var handler = Attach(window);
        ShowGames(window);
        var vm = window.Vm;
        var refresh = Named<Button>(window, "RefreshInterfaceButton");
        var retry = Named<Button>(window, "RetryGameListButton");
        var empty = Named<StackPanel>(window, "GameEmptyState");
        var message = Named<TextBlock>(window, "GameEmptyMessage");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.NextManifestGate = gate.Task;
        var loading = Load(window);
        check(vm.GameListLoading && message.Text.Contains("正在获取") && !retry.IsEnabled && !refresh.IsEnabled,
            "Pending game manifests show loading rather than a false no-match message");
        Capture(window, Path.Combine(folder, "game-loading.png"));
        gate.SetResult();
        await loading;
        await Idle(window);
        check(vm.GameListLoaded && vm.Games.Count == 1 && empty.Visibility == Visibility.Collapsed,
            "Successful game loading hides the empty-state panel and retains the release-only default");
        Named<TextBox>(window, "GameSearch").Text = "not-a-minecraft-version";
        check(vm.Games.Count == 0 && message.Text.Contains("当前筛选") && vm.GameListError.Length == 0 &&
            Named<Button>(window, "ClearGameFiltersButton").IsVisible,
            "Only genuine filter misses display no-match and offer to clear filters");
        Capture(window, Path.Combine(folder, "game-filter-empty.png"));
        Click(window, "ClearGameFiltersButton");
        await Idle(window);
        check(vm.Games.Count == 4 && vm.Query == "" && vm.GameType == "" && vm.InstalledFilter == "",
            "Clearing empty-result filters restores every game type without another manifest request");
        vm.GameType = "release";
        Named<TextBox>(window, "GameSearch").Text = "1.21";
        await Idle(window);
        vm.NewId = "my-custom-version";
        handler.FailOfficial = true;
        var requests = handler.OfficialRequests;
        Click(window, "RefreshPageButton");
        await Until(() => !vm.Refreshing && vm.GameListError.Length > 0);
        check(handler.OfficialRequests > requests && vm.Games.Count == 1 && vm.SelectedGame?.Id == "1.21.1" &&
            empty.Visibility == Visibility.Collapsed && vm.NewId == "my-custom-version",
            "A failed refresh preserves the existing game list, selection and custom installation name");
        handler.FailOfficial = false;
        requests = handler.OfficialRequests;
        Click(window, "RefreshInterfaceButton");
        await Until(() => !vm.Refreshing && handler.OfficialRequests > requests);
        await Idle(window);
        check(vm.GameListError == "" && vm.Error == "" && vm.Query == "1.21" && vm.GameType == "release" &&
            vm.SelectedGame?.Id == "1.21.1" && vm.NewId == "my-custom-version",
            "Title-bar refresh reloads games while preserving filters, selection and the custom name");
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.NextManifestGate = gate.Task;
        Click(window, "RefreshInterfaceButton");
        requests = handler.OfficialRequests;
        Click(window, "RefreshInterfaceButton");
        check(!refresh.IsEnabled && handler.OfficialRequests == requests && vm.Refreshing,
            "Repeated interface refresh clicks cannot queue duplicate manifest requests");
        gate.SetResult();
        await Until(() => !vm.Refreshing);
        await Idle(window);
        vm.Running = true;
        check(!refresh.IsEnabled && !Named<Button>(window, "RefreshPageButton").IsEnabled,
            "Both refresh entries disable while an installation is running");
        vm.Running = false;
        vm.Preparing = true;
        check(!refresh.IsEnabled, "Interface refresh defers during plan preparation");
        vm.Preparing = false;

        await CheckMenu(window, folder, check);

        var failed = create();
        var failHandler = Attach(failed);
        ShowGames(failed);
        SetSource(failed, SourceMode.Bmclapi);
        failHandler.FailMirror = true;
        await Load(failed);
        check(!failed.Vm.GameListLoaded && failed.Vm.GameListError.Length > 0 &&
            Named<TextBlock>(failed, "GameEmptyMessage").Text.Contains("无法获取") &&
            !failed.Vm.CanClearGameFilters && failed.Vm.Games.Count == 0,
            "An unavailable locked mirror is reported as a fetch failure rather than a filter miss");
        failed.Width = 1080; failed.Height = 740;
        Capture(failed, Path.Combine(folder, "game-fetch-failed-dark.png"));
        App.ApplyTheme("Light");
        Capture(failed, Path.Combine(folder, "game-fetch-failed-light.png"));
        App.ApplyTheme("Dark");
        Named<ComboBox>(failed, "SourceBox").SelectedValue = "Auto";
        await Until(() => failed.Vm.GameListLoaded && !failed.Vm.GameListLoading && !failed.Vm.LoaderLoading);
        check(failHandler.OfficialRequests > 0 && failed.Vm.GameListError == "" && failed.Vm.Games.Count == 1 &&
            failed.Vm.Settings.Source == SourceMode.Auto,
            "Changing a failed mirror to automatic mode immediately reloads and falls back to official data");
        failed.Close();

        var racing = create();
        var raceHandler = Attach(racing);
        ShowGames(racing);
        SetSource(racing, SourceMode.Official);
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        raceHandler.NextManifestGate = gate.Task;
        raceHandler.IgnoreGateCancellation = true;
        raceHandler.ObsoleteFirstManifest = true;
        var old = Load(racing);
        Named<ComboBox>(racing, "SourceBox").SelectedValue = "Bmclapi";
        await Until(() => !racing.Vm.GameListLoading && racing.Vm.GameListLoaded);
        gate.SetResult();
        await old;
        await Idle(racing);
        var games = (GameService)typeof(MainWindow).GetField("_games", Private)!.GetValue(racing)!;
        check(racing.Vm.Games.Single().Id == "1.21.1" && (await games.Releases()).All(r => r.Id != "obsolete-game"),
            "A cancelled old-source response cannot replace the new list or poison the game-service cache");
        racing.Close();
        window.Close();
        new Settings { AutomaticUpdates = false, Theme = "Dark" }.Save();
        App.ApplyTheme("Dark");
    }

    private static async Task CheckMenu(MainWindow window, string folder, Action<bool, string> check)
    {
        var button = Named<Button>(window, "UpdateButton");
        var menu = button.ContextMenu!;
        var items = menu.Items.OfType<MenuItem>().ToArray();
        var status = items[0];
        var automatic = items.Single(item => item.IsCheckable);
        window.Vm.UpdateStatus = "MineDock 0.1.4 · 已是最新正式版";
        window.Vm.AutomaticUpdates = true;
        foreach (var theme in new[] { "Dark", "Light" })
        {
            App.ApplyTheme(theme);
            Click(window, "UpdateButton");
            await Until(() => menu.IsOpen && menu.ActualWidth > 0);
            menu.UpdateLayout();
            check(Contrast(menu.Foreground, menu.Background) >= 4.5 &&
                items.Where(item => item.IsEnabled).All(item => Contrast(item.Foreground, menu.Background) >= 4.5),
                "Update menu action text has readable themed contrast: " + theme);
            check(status.Header is TextBlock text && Contrast(text.Foreground, menu.Background) >= 4.5,
                "Disabled update-status text remains readable: " + theme);
            var tick = (MineDock.App.Icon)automatic.Template.FindName("MenuCheck", automatic);
            check(automatic.IsChecked && tick.Visibility == Visibility.Visible &&
                Contrast(tick.Foreground, menu.Background) >= 4.5,
                "Automatic-update checked state remains visible: " + theme);
            check(menu.ActualWidth >= 250 && menu.ActualWidth < window.ActualWidth && menu.ActualHeight > 100,
                "Update menu content fits its themed surface: " + theme);
            check(!(bool)typeof(MainWindow).GetMethod("CanApplyUpdate", Private)!.Invoke(window, null)!,
                "Opening update settings defers executable replacement: " + theme);
            Capture(menu, Path.Combine(folder, "update-menu-" + theme.ToLowerInvariant() + ".png"));
            menu.IsOpen = false;
        }
        window.Vm.AutomaticUpdates = false;
        App.ApplyTheme("Dark");
    }

    private static double Contrast(Brush foreground, Brush background)
    {
        static double Luminance(Brush brush)
        {
            var color = ((SolidColorBrush)brush).Color;
            static double Channel(byte value)
            {
                var n = value / 255d;
                return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    private static Handler Attach(MainWindow window)
    {
        var network = (Network)typeof(MainWindow).GetField("_network", Private)!.GetValue(window)!;
        var field = typeof(Network).GetField("_client", Private)!;
        ((HttpClient)field.GetValue(network)!).Dispose();
        var handler = new Handler();
        field.SetValue(network, new HttpClient(handler));
        return handler;
    }

    private static void ShowGames(MainWindow window)
    {
        var suppress = typeof(MainWindow).GetField("_suppress", Private)!;
        suppress.SetValue(window, true);
        window.Vm.Page = PageKind.Game;
        Named<RadioButton>(window, "NavGame").IsChecked = true;
        suppress.SetValue(window, false);
    }
    private static void SetSource(MainWindow window, SourceMode source)
    {
        var suppress = typeof(MainWindow).GetField("_suppress", Private)!;
        suppress.SetValue(window, true);
        window.Vm.SourceValue = source.ToString();
        ((Network)typeof(MainWindow).GetField("_network", Private)!.GetValue(window)!).Source = source;
        suppress.SetValue(window, false);
    }
    private static Task Load(MainWindow window)
        => (Task)typeof(MainWindow).GetMethod("LoadGameList", Private)!.Invoke(window, null)!;
    private static Task Idle(MainWindow window) => Until(() => !window.Vm.LoaderLoading && !window.Vm.GameListLoading);
    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("Interface regression did not reach the expected state.");
    }
    private static T Named<T>(MainWindow window, string name) => (T)window.FindName(name);
    private static void Click(MainWindow window, string name)
        => Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Capture(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path);
        encoder.Save(output);
    }

    private sealed class Handler : HttpMessageHandler
    {
        internal Task? NextManifestGate;
        internal bool FailOfficial, FailMirror, IgnoreGateCancellation, ObsoleteFirstManifest;
        internal int OfficialRequests, MirrorRequests, ManifestRequests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("version_manifest_v2.json"))
                return Json(new { javaVersion = new { majorVersion = 21 } });
            var first = ++ManifestRequests == 1;
            var mirror = request.RequestUri.Host == "bmclapi2.bangbang93.com";
            if (mirror) MirrorRequests++; else OfficialRequests++;
            var gate = NextManifestGate;
            NextManifestGate = null;
            if (gate is not null)
            {
                if (IgnoreGateCancellation) await gate;
                else await gate.WaitAsync(ct);
            }
            if (mirror ? FailMirror : FailOfficial) return new(HttpStatusCode.ServiceUnavailable);
            return Json(new
            {
                versions = new[]
                {
                    new { id = first && ObsoleteFirstManifest ? "obsolete-game" : "1.21.1", type = "release" },
                    new { id = "snapshot-test", type = "snapshot" },
                    new { id = "alpha-test", type = "old_alpha" },
                    new { id = "beta-test", type = "old_beta" }
                }.Select(game => new { game.id, game.type, url = "https://fixture.invalid/" + game.id + ".json",
                    releaseTime = "2024-08-08T12:00:00Z" })
            });
        }
        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(body)) };
    }
}
