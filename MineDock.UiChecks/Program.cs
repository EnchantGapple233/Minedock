using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MineDock.App;
using MineDock.Core;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly List<string> Passed = [];
    private static readonly List<MainWindow> Windows = [];
    private static string Folder = "";
    private static int Result;

    [STAThread]
    private static int Main(string[] args)
    {
        Folder = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? "check-output-ui");
        Directory.CreateDirectory(Folder);
        Environment.SetEnvironmentVariable("MINEDOCK_DATA", Path.Combine(Folder, "state"));
        var app = new App(false);
        app.InitializeComponent();
        App.ApplyTheme("Dark");
        var window = CreateWindow();
        window.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var updateArgument=Array.IndexOf(args,"--update-exe");
                if(updateArgument>=0)
                {
                    await UpdateSmokeChecks.Run(Folder,Path.GetFullPath(args[updateArgument+1]),
                        Path.GetFullPath(args[updateArgument+2]),Check);
                    return;
                }
                if (args.Contains("--loader-install"))
                {
                    await LoaderInstallChecks.Run(Folder, Check, args);
                    return;
                }
                if (args.Contains("--capture-loader"))
                {
                    await LoaderFeatureChecks.CaptureLive(window, Folder, Check);
                    return;
                }
                if (args.Contains("--interface"))
                {
                    await InterfaceFeatureChecks.Run(CreateWindow, Folder, Check);
                    return;
                }
                await Verify(window);
                await CatalogFeatureChecks.Run(CreateWindow, Folder, Check);
                await LoaderFeatureChecks.Run(CreateWindow, Folder, Check);
                await BackgroundFeatureChecks.Run(CreateWindow, Folder, Check);
                await InterfaceFeatureChecks.Run(CreateWindow, Folder, Check);
                if (args.Contains("--live"))
                {
                    new Settings().Save();
                    await VerifyLive(CreateWindow());
                }
            }
            catch (Exception ex)
            {
                Result = 1;
                File.WriteAllText(Path.Combine(Folder, "failure.txt"), ex.ToString());
                Console.WriteLine(ex);
            }
            finally
            {
                File.WriteAllText(Path.Combine(Folder, "results.json"), JsonSerializer.Serialize(
                    new { Passed = Passed.Count, Checks = Passed, Live = args.Contains("--live"),
                        DpiScale = VisualTreeHelper.GetDpi(window).DpiScaleX, ExitCode = Result }, JsonTools.Options));
                foreach (var item in Windows) item.Close();
                Dispatcher.ExitAllFrames();
            }
        });
        Dispatcher.Run();
        return Result;
    }

    private static MainWindow CreateWindow()
    {
        var window = new MainWindow { Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
        var loaded = (RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler), window,
            typeof(MainWindow).GetMethod("OnLoaded", Private)!);
        window.Loaded -= loaded;
        typeof(MainWindow).GetField("_ready", Private)!.SetValue(window, true);
        Windows.Add(window);
        window.Show();
        window.Vm.Page = PageKind.Mod;
        Named<RadioButton>(window, "NavMod").IsChecked = true;
        return window;
    }

    private static T Named<T>(MainWindow window, string name) => (T)window.FindName(name);
    private static Task Search(MainWindow window, int page = 1)
        => (Task)typeof(MainWindow).GetMethod("Search", Private)!.Invoke(window, [page])!;
    private static Task Jump(MainWindow window)
        => (Task)typeof(MainWindow).GetMethod("JumpToPage", Private, Type.EmptyTypes)!.Invoke(window, null)!;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Passed.Add(name);
        Console.WriteLine("PASS: " + name);
    }
    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!ready()) throw new TimeoutException("UI did not reach the expected state.");
    }
    private static void Click(MainWindow window, string name)
        => Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task Verify(MainWindow window)
    {
        var network = (Network)typeof(MainWindow).GetField("_network", Private)!.GetValue(window)!;
        var clientField = typeof(Network).GetField("_client", Private)!;
        ((HttpClient)clientField.GetValue(network)!).Dispose();
        var handler = new CatalogHandler();
        clientField.SetValue(network, new HttpClient(handler));
        await Search(window);
        await Task.Delay(160);
        var previous = Named<Button>(window, "PreviousPageButton");
        var next = Named<Button>(window, "NextPageButton");
        var input = Named<TextBox>(window, "PageNumberBox");
        Check(window.Vm.CurrentPage == 1 && window.Vm.TotalPages == 4 && window.Vm.TotalProjects == 95 &&
            window.Vm.Projects.Count == 30, "First page uses upstream total and 30 results per page");
        Check(!previous.IsEnabled && next.IsEnabled && input.IsEnabled, "First page disables previous navigation");
        Check(previous.ActualWidth == next.ActualWidth && previous.ActualHeight == next.ActualHeight &&
            previous.ActualWidth == 42, "Previous and next buttons have identical fixed dimensions");
        Click(window, "NextPageButton");
        await Until(() => !window.Vm.Loading && window.Vm.CurrentPage == 2);
        Check(handler.LastOffset == 30 && window.Vm.Projects[0].Title.EndsWith("31"),
            "Next button fetches the next offset and updates the visible page");
        Click(window, "PreviousPageButton");
        await Until(() => !window.Vm.Loading && window.Vm.CurrentPage == 1);
        Check(handler.LastOffset == 0, "Previous button returns to the first page");
        input.Text = "3";
        input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
            { RoutedEvent = Keyboard.KeyDownEvent });
        await Until(() => !window.Vm.Loading && window.Vm.CurrentPage == 3);
        Check(handler.LastOffset == 60, "Enter in the page field jumps to the requested page");
        input.Text = "4";
        Click(window, "JumpPageButton");
        await Until(() => !window.Vm.Loading && window.Vm.CurrentPage == 4);
        Check(window.Vm.Projects.Count == 5 && previous.IsEnabled && !next.IsEnabled,
            "Jump button reaches the partial last page and disables next navigation");
        foreach (var invalid in new[] { "", "0", "-1", "5", "1.5", "abc", "999999999999999999999" })
        {
            var count = handler.Searches;
            input.Text = invalid;
            await Jump(window);
            Check(window.Vm.CurrentPage == 4 && window.Vm.PageError.Length > 0 && handler.Searches == count,
                "Invalid page is rejected without a request: " + (invalid.Length == 0 ? "(empty)" : invalid));
        }
        input.Text = "bad";
        input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape)
            { RoutedEvent = Keyboard.KeyDownEvent });
        Check(input.Text == "4" && window.Vm.PageError == "", "Escape restores the committed page number");
        input.Text = " 2 ";
        await Jump(window);
        Check(window.Vm.CurrentPage == 2 && input.Text == "2", "Whitespace around a valid page is accepted and normalized");
        var first = window.Vm.Projects[0];
        handler.Fail = true;
        input.Text = "3";
        await Jump(window);
        Check(window.Vm.CurrentPage == 2 && window.Vm.Projects[0] == first && input.Text == "2" &&
            window.Vm.Error.Length > 0 && !window.Vm.Loading,
            "Failed navigation preserves results, committed page, and editable controls");
        handler.Fail = false;
        window.Vm.Error = "";
        handler.Delay = 180;
        var searches = handler.Searches;
        var pending = Search(window, 3);
        await Task.Delay(20);
        Check(!previous.IsEnabled && !next.IsEnabled && !input.IsEnabled &&
            !Named<Button>(window, "JumpPageButton").IsEnabled, "All pagination controls disable during a request");
        Click(window, "NextPageButton");
        await Jump(window);
        await pending;
        Check(handler.Searches == searches + 1 && window.Vm.CurrentPage == 3,
            "Repeated navigation while loading does not queue extra page requests");
        handler.Delay = 0;
        window.Vm.Query = "changed";
        input.Text = "4";
        await Jump(window);
        Check(window.Vm.CurrentPage == 1 && handler.LastOffset == 0,
            "Changing search criteria resets pagination before applying the new query");
        handler.Total = 95;
        await Search(window, 4);
        handler.Total = 31;
        input.Text = "3";
        await Jump(window);
        Check(window.Vm.CurrentPage == 2 && window.Vm.TotalPages == 2 && window.Vm.Projects.Count == 1,
            "Shrinking results automatically refetch the new last page");
        handler.Total = 0;
        await Search(window);
        Check(window.Vm.CurrentPage == 0 && window.Vm.TotalPages == 0 && window.Vm.Projects.Count == 0 &&
            !previous.IsEnabled && !next.IsEnabled && !input.IsEnabled, "Empty results show zero pages with disabled navigation");
        handler.Total = 95;
        handler.Delay = 220;
        window.Vm.Query = "old";
        var old = Search(window);
        handler.Delay = 0;
        handler.Total = 31;
        window.Vm.Query = "new";
        await Search(window);
        await old;
        Check(window.Vm.TotalProjects == 31 && window.Vm.Projects[0].Title.StartsWith("new") && !window.Vm.Loading,
            "Superseded requests cannot overwrite the latest results or loading state");
        var tint = (Border)next.Template.FindName("Tint", next);
        VisualStateManager.GoToState(next, "MouseOver", true);
        await Task.Delay(140);
        var hover = tint.Opacity;
        VisualStateManager.GoToState(next, "Pressed", true);
        await Task.Delay(140);
        Check(hover > 0 && tint.Opacity > hover && next.ActualWidth == 42, "Hover and pressed transitions keep button geometry stable");
        VisualStateManager.GoToState(next, "Normal", true);
        await Until(() => tint.Opacity == 0);
        Check(tint.Opacity == 0 && next.Template.FindName("FocusRing", next) is Border,
            "Normal state restores appearance and icon buttons include a keyboard focus ring");
        await CheckElevation(window, next);
        CheckPageText(window);
        CheckLayout(window);
        Capture(window, "pagination-fixture-dark.png");
        App.ApplyTheme("Light");
        window.Width = 1080; window.Height = 740;
        await Task.Delay(100);
        CheckLayout(window);
        CheckPageText(window);
        Capture(window, "pagination-fixture-light-compact.png");
        App.ApplyTheme("Dark");
        CheckLauncherAssociation(window);
        await LauncherFeatureChecks.Run(window, Folder, Check);
    }

    private static async Task CheckElevation(MainWindow window, Button button)
    {
        var surface = (Grid)button.Template.FindName("ButtonSurface", button);
        var shadowSurface = (Border)button.Template.FindName("ButtonShadow", button);
        double Lift() => ((TranslateTransform)surface.RenderTransform).Y;
        double Shadow() => ((System.Windows.Media.Effects.DropShadowEffect)shadowSurface.Effect).Opacity;
        VisualStateManager.GoToState(button, "MouseOver", true);
        await Until(() => Lift() < 0 && Shadow() > 0);
        Check(Lift() < 0 && Shadow() > 0, "Button hover lifts its surface and reveals a soft shadow");
        var hit = button.InputHitTest(new Point(button.ActualWidth / 2, button.ActualHeight - .25)) as DependencyObject;
        Check(hit is not null && (hit == button || button.IsAncestorOf(hit)),
            "Raised buttons retain a stable hit target at their lower edge");
        VisualStateManager.GoToState(button, "Pressed", true);
        await Until(() => Lift() > 0 && Shadow() < .2);
        Check(Lift() > 0 && Shadow() < .2, "Pressed buttons settle without changing layout dimensions");
        VisualStateManager.GoToState(button, "Normal", true);
        await Until(() => Lift() == 0 && Shadow() == 0);
        Check(Lift() == 0 && Shadow() == 0, "Button elevation returns completely to rest");
        var nav = Named<RadioButton>(window, "NavMod");
        var navLift = (TranslateTransform)((Grid)nav.Template.FindName("NavVisual", nav)).RenderTransform;
        VisualStateManager.GoToState(nav, "Normal", false);
        Check(nav.IsChecked == true && navLift.Y == 0 &&
            ((Border)nav.Template.FindName("NavRail", nav)).Opacity == 1,
            "Selected navigation rests flat while retaining its color and accent marker");
        VisualStateManager.GoToState(nav, "MouseOver", true);
        await Until(() => navLift.Y == -2);
        Check(((System.Windows.Media.Effects.DropShadowEffect)((Border)nav.Template.FindName("NavSurface", nav)).Effect).Opacity > 0,
            "Navigation hover raises the option and reveals its shadow");
        var navHit = nav.InputHitTest(new Point(nav.ActualWidth / 2, nav.ActualHeight - .25)) as DependencyObject;
        Check(navHit is not null && (navHit == nav || nav.IsAncestorOf(navHit)),
            "Raised navigation retains its full original hit area");
        VisualStateManager.GoToState(nav, "Pressed", true);
        await Until(() => Math.Abs(navLift.Y + .4) < .001);
        Check(((Border)nav.Template.FindName("NavTint", nav)).Opacity > 0,
            "Pressing an option lowers it and adds color feedback");
        VisualStateManager.GoToState(nav, "Normal", true);
        await Until(() => navLift.Y == 0);
        Check(((Border)nav.Template.FindName("NavRail", nav)).Opacity == 1 &&
            ((Border)nav.Template.FindName("NavSurface", nav)).Opacity == 0,
            "Leaving a selected option removes elevation and shadow without removing selection");
        var inactive = Named<RadioButton>(window, "NavTasks");
        VisualStateManager.GoToState(inactive, "MouseOver", true);
        await Until(() => ((TranslateTransform)((Grid)inactive.Template.FindName("NavVisual", inactive)).RenderTransform).Y == -2);
        Check(inactive.IsChecked == false &&
            ((Border)inactive.Template.FindName("NavSurface", inactive)).Opacity == 1,
            "Unselected options also lift on hover without becoming selected");
        VisualStateManager.GoToState(inactive, "Normal", false);
        var list = Named<ListBox>(window, "ProjectList");
        list.SelectedIndex = 0;
        window.UpdateLayout();
        await Task.Delay(180);
        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        var selectionRoot = (Grid)item.Template.FindName("SelectionRoot", item);
        var itemLift = (TranslateTransform)selectionRoot.RenderTransform;
        VisualStateManager.GoToState(item, "Normal", false);
        Check(item.IsSelected && itemLift.Y == 0 &&
            ((Border)item.Template.FindName("SelectionRail", item)).Opacity == 1,
            "Selected results rest flat while retaining their selected color");
        VisualStateManager.GoToState(item, "MouseOver", true);
        await Until(() => itemLift.Y == -2);
        Check(((System.Windows.Media.Effects.DropShadowEffect)((Border)item.Template.FindName("SelectionSurface", item)).Effect).Opacity > 0,
            "Result hover raises the row with a soft shadow");
        var itemHit = item.InputHitTest(new Point(item.ActualWidth / 2, item.ActualHeight - .25)) as DependencyObject;
        Check(itemHit is not null && (itemHit == item || item.IsAncestorOf(itemHit)),
            "Raised result rows retain a stable lower-edge hit area");
        VisualStateManager.GoToState(item, "Normal", true);
        await Until(() => itemLift.Y == 0);
        Check(((Border)item.Template.FindName("SelectionRail", item)).Opacity == 1 &&
            ((Border)item.Template.FindName("SelectionSurface", item)).Opacity == 0,
            "Leaving a selected result restores rest and preserves its selection");
        list.SelectedIndex = 1;
        await Until(() => !item.IsSelected && ((TranslateTransform)selectionRoot.RenderTransform).Y == 0 &&
            ((Border)item.Template.FindName("SelectionRail", item)).Opacity == 0);
        Check(!item.IsSelected && ((TranslateTransform)selectionRoot.RenderTransform).Y == 0 &&
            ((Border)item.Template.FindName("SelectionRail", item)).Opacity == 0,
            "Changing selection removes the previous result's elevation");
        list.SelectedIndex = 0;
    }

    private static void CheckPageText(MainWindow window)
    {
        var input = Named<TextBox>(window, "PageNumberBox");
        var page = window.Vm.CurrentPage;
        var total = window.Vm.TotalProjects;
        window.Vm.SetCatalogPage(71582789, int.MaxValue);
        window.UpdateLayout();
        var host = (ScrollViewer)input.Template.FindName("PART_ContentHost", input);
        host.ApplyTemplate();
        var viewport = (FrameworkElement)host.Template.FindName("PART_ScrollContentPresenter", host);
        var hostBounds = viewport.TransformToAncestor(input).TransformBounds(new Rect(viewport.RenderSize));
        var text = input.GetRectFromCharacterIndex(0).UnionRect(input.GetRectFromCharacterIndex(input.Text.Length - 1, true));
        Check(input.FontSize >= 16 && input.ActualHeight >= 42 &&
            text.Top >= hostBounds.Top - .6 && text.Bottom <= hostBounds.Bottom + .6 &&
            text.Left >= hostBounds.Left - .6 && text.Right <= hostBounds.Right + .6,
            $"Eight-digit page text fits entirely inside its viewport at {window.Width}x{window.Height}");
        CheckLayout(window);
        foreach (var scale in new[] { 1.25, 1.5, 2.0 })
        {
            var image = Render(Named<Grid>(window, "CatalogPager"), scale);
            var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
            image.CopyPixels(pixels, image.PixelWidth * 4, 0);
            Check(pixels.Where((_, i) => i % 4 == 3).Count(value => value > 0) > 100,
                $"Pagination renders nonblank at {scale:P0} raster scale and {window.Width}x{window.Height}");
            Save(image, $"pagination-text-{window.Width}-{scale:0.00}.png");
        }
        window.Vm.SetCatalogPage(page, total);
        window.UpdateLayout();
    }

    private static Rect UnionRect(this Rect left, Rect right) { left.Union(right); return left; }

    private static void CheckLauncherAssociation(MainWindow window)
    {
        var folder = Path.Combine(Folder, "launcher-fixtures");
        var root = Path.Combine(folder, ".minecraft");
        var version = Path.Combine(root, "versions", "fixture");
        Directory.CreateDirectory(version);
        Directory.CreateDirectory(Path.Combine(folder, "PCL"));
        File.WriteAllText(Path.Combine(version, "fixture.json"), """{"id":"fixture","clientVersion":"1.21.1","type":"release"}""");
        File.WriteAllText(Path.Combine(folder, "PCL", "Setup.ini"), "LaunchArgumentIndieV2:0");
        var pcl = Path.Combine(folder, "PCL.exe");
        File.WriteAllText(pcl, "");
        var apply = typeof(MainWindow).GetMethod("ApplyLauncher", Private)!;
        window.Vm.ShowPlan = true;
        window.Vm.PlanFiles.Add(new() { Destination = "previous-target" });
        apply.Invoke(window, [pcl, LauncherKind.Pcl]);
        Check(window.Vm.Settings.MinecraftRoot == root && window.Vm.RootLabel == root &&
            window.Vm.Target?.Id == "fixture" && window.Vm.TargetDirectory == root,
            "Associating PCL immediately updates the root, instance list, target, and resource directory");
        Check(!window.Vm.ShowPlan && window.Vm.PlanFiles.Count == 0 &&
            typeof(MainWindow).GetField("_plan", Private)!.GetValue(window) is null,
            "Changing the launcher discards an installation plan for the previous target");
        var hmcl = Path.Combine(folder, "HMCL.jar");
        File.WriteAllText(hmcl, "");
        File.WriteAllText(Path.Combine(folder, "hmcl.json"), """{"last":"Default","configurations":{"Default":{"gameDir":".minecraft","global":{"gameDirType":1}}}}""");
        window.Vm.Running = true;
        apply.Invoke(window, [hmcl, LauncherKind.Hmcl]);
        Check(window.Vm.Settings.Launcher == LauncherKind.Pcl && !Named<Button>(window, "LauncherButton").IsEnabled,
            "Running installations prevent launcher reassociation");
        window.Vm.Running = false;
        apply.Invoke(window, [hmcl, LauncherKind.Hmcl]);
        Check(window.Vm.LauncherLabel == "HMCL 已关联" && window.Vm.TargetDirectory == version &&
            Settings.Load().Launcher == LauncherKind.Hmcl && Settings.Load().MinecraftRoot == root,
            "Associating HMCL updates the UI, respects isolation, and saves the association");
        var missing = Path.Combine(Folder, "no-game-folder", "PCL.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(missing)!);
        File.WriteAllText(missing, "");
        apply.Invoke(window, [missing, LauncherKind.Pcl]);
        Check(window.Vm.Settings.MinecraftRoot == "" && window.Vm.Target is null && window.Vm.Instances.Count == 0,
            "A launcher with no detected game directory clears the previous target before manual selection");
    }

    private static void CheckLayout(MainWindow window)
    {
        window.UpdateLayout();
        var names = new[] { "CatalogCountText", "PreviousPageButton", "PageNumberBox", "PageCountText", "NextPageButton", "JumpPageButton" };
        var bounds = names.Select(name =>
        {
            var control = Named<FrameworkElement>(window, name);
            return control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
        }).ToArray();
        var list = Named<ListBox>(window, "ProjectList");
        var first = list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        var resultFits = first is null || first.TransformToAncestor(list).TransformBounds(new Rect(first.RenderSize)).Right <= list.ActualWidth + .6;
        Check(bounds.All(r => r.Width > 0 && r.Height > 0 && r.X >= 0 && r.Right <= window.ActualWidth && r.Bottom <= window.ActualHeight) &&
            bounds.Zip(bounds.Skip(1)).All(pair => pair.First.Right <= pair.Second.Left + .6) && resultFits,
            $"Pagination and result rows fit without overlap or horizontal clipping at {window.Width}x{window.Height}");
    }

    private static async Task VerifyLive(MainWindow window)
    {
        foreach (var kind in new[] { PageKind.Mod, PageKind.Modpack, PageKind.Shader, PageKind.Resourcepack })
        {
            window.Vm.Page = kind;
            window.Vm.Query = "";
            window.Vm.Sort = "downloads";
            await Search(window);
            await Search(window, 2);
            Check(window.Vm.CurrentPage == 2 && window.Vm.TotalPages > 1 && window.Vm.Projects.Count > 0 &&
                window.Vm.Error == "", "Live Modrinth second page: " + kind);
        }
        window.Vm.Page = PageKind.Mod;
        await Search(window);
        await Search(window, window.Vm.TotalPages);
        Check(window.Vm.CurrentPage == window.Vm.TotalPages && !window.Vm.CanNextPage &&
            window.Vm.Projects.Count > 0, "Live Modrinth final page can be fetched");
        await Search(window, 2);
        await Task.Delay(1800);
        Capture(window, "pagination-dark.png");
        App.ApplyTheme("Light");
        await Task.Delay(150);
        Capture(window, "pagination-light.png");
        window.Width = 1080; window.Height = 740;
        await Task.Delay(100);
        CheckLayout(window);
        Capture(window, "pagination-compact.png");
        App.ApplyTheme("Dark");
    }

    private static void Capture(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        Save(Render(element, 1), name);
    }
    private static RenderTargetBitmap Render(FrameworkElement element, double scale)
    {
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        if (element is Window) image.Render(element);
        else
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
                drawing.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null,
                    new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            image.Render(visual);
        }
        return image;
    }
    private static void Save(BitmapSource image, string name)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(Path.Combine(Folder, name));
        png.Save(output);
    }

    private sealed class CatalogHandler : HttpMessageHandler
    {
        public int Total = 95, Delay, Searches, LastOffset;
        public bool Fail;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath != "/v2/search")
                return new(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") };
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            var offset = int.Parse(query["offset"]!);
            var total = Total;
            var fail = Fail;
            var title = query["query"] ?? "";
            Searches++; LastOffset = offset;
            await Task.Delay(Delay, ct);
            if (fail) throw new HttpRequestException("Deliberate offline fixture failure.");
            var hits = Enumerable.Range(offset, Math.Max(0, Math.Min(ContentService.SearchPageSize, total - offset)))
                .Select(i => new { project_id = "fixture-" + i, slug = "fixture-" + i, title = title + "Project " + (i + 1),
                    description = "Deterministic pagination fixture", icon_url = "", author = "Fixture", downloads = i,
                    categories = Array.Empty<string>(), project_type = "mod" });
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { hits, total_hits = total, offset,
                    limit = ContentService.SearchPageSize }), Encoding.UTF8, "application/json")
            };
        }
    }
}
