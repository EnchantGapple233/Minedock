using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MineDock.App;
using MineDock.Core;

internal static class LoaderFeatureChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static async Task CaptureLive(MainWindow window, string folder, Action<bool,string> check)
    {
        var suppress=typeof(MainWindow).GetField("_suppress",Private)!;
        suppress.SetValue(window,true);
        window.Vm.Page=PageKind.Game;
        ((RadioButton)window.FindName("NavGame")).IsChecked=true;
        await (Task)typeof(MainWindow).GetMethod("LoadGameList",Private)!.Invoke(window,null)!;
        window.Vm.Query="1.21.1";
        typeof(MainWindow).GetMethod("FilterGames",Private)!.Invoke(window,null);
        window.Vm.SelectedGame=window.Vm.Games.Single(g=>g.Id=="1.21.1");
        window.Vm.GameLoader="forge";
        suppress.SetValue(window,false);
        await (Task)typeof(MainWindow).GetMethod("UpdateGameSelection",Private)!.Invoke(window,null)!;
        check(window.Vm.Loaders.Count>1 && window.Vm.SelectedLoader is {Stable:true} &&
            ((ComboBox)window.FindName("LoaderVersionBox")).Items.Count==window.Vm.Loaders.Count,
            "Real Forge list is complete in the WPF dropdown and defaults to a stable release");
        foreach(var (theme,width,height,name) in new[] {
            ("Dark",1340d,860d,"live-forge-dark.png"),("Light",1080d,740d,"live-forge-light-compact.png") })
        {
            App.ApplyTheme(theme);window.Width=width;window.Height=height;
            window.UpdateLayout();await Task.Delay(200);
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output=File.Create(Path.Combine(folder,name));encoder.Save(output);
            var button=(Button)window.FindName("PrepareGameButton");
            check(button.IsVisible && button.ActualWidth>180 && window.Vm.Error=="",
                "Live Forge controls render with no application error: "+theme+" "+width+"px");
        }
    }
    public static async Task Run(Func<MainWindow> create, string folder, Action<bool,string> check)
    {
        var window = create();
        var vm = window.Vm;
        var network = (Network)typeof(MainWindow).GetField("_network",Private)!.GetValue(window)!;
        var client = typeof(Network).GetField("_client",Private)!;
        ((HttpClient)client.GetValue(network)!).Dispose();
        var handler = new Handler();
        client.SetValue(network,new HttpClient(handler));
        var suppress = typeof(MainWindow).GetField("_suppress",Private)!;
        suppress.SetValue(window,true);
        vm.Page = PageKind.Game;
        ((RadioButton)window.FindName("NavGame")).IsChecked = true;
        var release = new GameRelease("1.21.1","https://fixture.invalid/game.json",DateTime.Today);
        var games=(GameService)typeof(MainWindow).GetField("_games",Private)!.GetValue(window)!;
        typeof(GameService).GetField("_releases",Private)!.SetValue(games,new List<GameRelease> {release});
        typeof(MainWindow).GetField("_releases",Private)!.SetValue(window,new List<GameRelease>
        { release, new("snapshot-test","https://fixture.invalid/game.json",DateTime.Today,"snapshot"),
            new("alpha-test","https://fixture.invalid/game.json",DateTime.Today,"old_alpha"),
            new("beta-test","https://fixture.invalid/game.json",DateTime.Today,"old_beta") });
        suppress.SetValue(window,false);
        void Filter() => typeof(MainWindow).GetMethod("FilterGames",Private)!.Invoke(window,null);
        Task Load() => (Task)typeof(MainWindow).GetMethod("UpdateGameSelection",Private)!.Invoke(window,null)!;
        Filter();
        check(vm.Games.Count == 1, "Game page defaults to official releases");
        vm.GameType=""; Filter();
        check(vm.Games.Count == 4, "Game page can display snapshots and historical releases");
        vm.GameType="snapshot"; Filter();
        check(vm.Games.Single().Release.Type=="snapshot", "Minecraft type filter selects snapshots");
        vm.GameType="release"; Filter();
        check(vm.GameLoaderChoices.Length == 8, "Game page exposes vanilla and seven loader families");
        foreach (var kind in LoaderService.Kinds)
        {
            suppress.SetValue(window,true);
            vm.GameLoader=kind;
            suppress.SetValue(window,false);
            await Load();
            var combo=(ComboBox)window.FindName("LoaderVersionBox");
            check(vm.Loaders.Count==3 && combo.Items.Count==3, kind+" dropdown displays every returned version: "+vm.LoaderStatus);
            check(vm.SelectedLoader!.Stable && vm.SelectedLoader==vm.Loaders.First(l=>l.Stable),kind+" defaults to newest stable release");
            combo.SelectedIndex=0;
            check(vm.NewId.Contains(vm.SelectedLoader!.Version) && vm.NewId.Contains(vm.LoaderLabel.Replace(' ','-')),
                kind+" selected version is retained in the installation identity");
        }
        handler.DelayForge=true;
        suppress.SetValue(window,true);vm.GameLoader="forge";suppress.SetValue(window,false);
        var old=Load();
        check(vm.SelectedLoader is null && !vm.CanPrepareGame, "Loading a new loader immediately clears the previous selection");
        suppress.SetValue(window,true);vm.GameLoader="quilt";suppress.SetValue(window,false);
        await Load(); await old;
        check(vm.GameLoader=="quilt" && vm.Loaders.Count==3 && vm.LoaderStatus.StartsWith("3"), "Late responses cannot overwrite a newly selected loader");
        handler.Empty=true;
        await Load();
        check(vm.Loaders.Count==0 && !vm.CanPrepareGame && vm.LoaderStatus.Contains("没有适用于"),"No compatible release disables game preparation");
        handler.Empty=false;handler.Fail=true;
        await Load();
        check(vm.Loaders.Count==0 && !vm.CanPrepareGame && vm.LoaderStatus.StartsWith("获取失败"),"Network failure is distinct from an empty compatible version list");
        handler.Fail=false;
        await Load();
        check(vm.CanPrepareGame,"Retry restores the loader list and installation action");
        var count=handler.Requests;
        suppress.SetValue(window,true);vm.GameLoader="vanilla";suppress.SetValue(window,false);
        await Load();
        check(vm.SelectedLoader is null && !vm.HasLoader && vm.CanPrepareGame && handler.Requests==count+1,
            "Vanilla clears the loader and only requests game metadata");
        check(vm.AutomaticUpdates,"Automatic updates are enabled by default");
        vm.AutomaticUpdates=false;vm.Settings.Save();
        check(!Settings.Load().AutomaticUpdates,"Automatic update opt-out persists");
        bool CanUpdate()=>(bool)typeof(MainWindow).GetMethod("CanApplyUpdate",Private)!.Invoke(window,null)!;
        vm.Loading=false;
        check(CanUpdate(),"An idle application permits update replacement");
        vm.Running=true;check(!CanUpdate(),"Active installations defer update replacement");vm.Running=false;
        vm.Preparing=true;check(!CanUpdate(),"Plan preparation defers update replacement");vm.Preparing=false;
        vm.ShowPlan=true;check(!CanUpdate(),"A pending installation plan defers update replacement");vm.ShowPlan=false;
        vm.LoaderLoading=true;check(!CanUpdate(),"Loader version requests defer update replacement");vm.LoaderLoading=false;
        vm.BackgroundBusy=true;check(!CanUpdate(),"Background image processing defers update replacement");vm.BackgroundBusy=false;
        System.Windows.Interop.ComponentDispatcher.PushModal();
        try { check(!CanUpdate(),"Native modal dialogs defer automatic update replacement"); }
        finally { System.Windows.Interop.ComponentDispatcher.PopModal(); }
        suppress.SetValue(window,true);vm.GameLoader="forge";suppress.SetValue(window,false);
        handler.DelayForge=false;
        await Load();
        vm.SelectedLoader = vm.Loaders.Last();
        var preferred = vm.SelectedLoader.Version;
        vm.NewId = "custom-forge-profile";
        ((Button)window.FindName("RefreshInterfaceButton")).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (vm.Refreshing && DateTime.UtcNow < deadline) await Task.Delay(20);
        check(!vm.Refreshing && vm.SelectedLoader?.Version == preferred && vm.NewId == "custom-forge-profile",
            "Interface refresh preserves a manually chosen loader version and custom profile name");
        foreach(var (theme,width,height,name) in new[] {
            ("Dark",1340d,860d,"loaders-dark.png"),("Light",1340d,860d,"loaders-light.png"),("Dark",1080d,740d,"loaders-compact.png") })
        {
            App.ApplyTheme(theme);window.Width=width;window.Height=height;
            window.UpdateLayout();await Task.Delay(100);
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output=File.Create(Path.Combine(folder,name));encoder.Save(output);
            var button=(Button)window.FindName("PrepareGameButton");
            check(button.ActualWidth>180 && button.ActualHeight>=30,"Loader controls fit "+theme+" "+width+"px");
        }
        window.Close();
    }
    private sealed class Handler:HttpMessageHandler
    {
        public bool Empty,Fail,DelayForge;
        public int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Requests++;
            var url=request.RequestUri!;
            if(DelayForge && url.AbsolutePath.Contains("/forge/")) await Task.Delay(220,CancellationToken.None);
            if(Fail) return new(HttpStatusCode.ServiceUnavailable);
            object body=url.AbsolutePath.EndsWith("version_manifest_v2.json")
                ? new { versions = new[] { new { id="1.21.1",type="release",url="https://fixture.invalid/game.json",releaseTime="2024-08-08T12:00:00Z" } } }
                : url.Host=="fixture.invalid" ? new { javaVersion=new { majorVersion=21 } }
                : url.AbsolutePath.Contains("/versions/game") ? new[] { new { version="1.21.1" } }
                : Empty ? Array.Empty<object>()
                : url.AbsolutePath.Contains("/versions/loader/") ? new[] {
                    new { loader=new { version="0.21.0-beta.1",stable=false } },
                    new { loader=new { version="0.20.1",stable=true } },new { loader=new { version="0.19.0",stable=true } } }
                : url.AbsolutePath.Contains("/optifine/") ? new[] {
                    new { mcversion="1.21.1",type="0.21.0",patch="beta.1" },
                    new { mcversion="1.21.1",type="0.20",patch="1" },new { mcversion="1.21.1",type="0.19",patch="0" } }
                : new[] { new { mcversion="1.21.1",version="0.21.0-beta.1" },
                    new { mcversion="1.21.1",version="0.20.1" },new { mcversion="1.21.1",version="0.19.0" } };
            return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(body))};
        }
    }
}
