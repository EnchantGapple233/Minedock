using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow : Window
{
    public MainViewModel Vm { get; }
    private readonly Network _network = new();
    private readonly GameService _games;
    private readonly ContentService _content;
    private readonly JavaService _java;
    private readonly InstallEngine _engine;
    private List<GameRelease> _releases = [];
    private List<CategoryTag> _categories = [];
    private List<LoaderTag> _loaderTags = [];
    private List<int> _allJavaMajors = [];
    private readonly Dictionary<PageKind, CatalogState> _filterStates = new();
    private InstallPlan? _plan;
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _search;
    private bool _ready, _suppress;
    private int _offset, _gameSelection, _projectSelection, _navigation;
    public MainWindow()
    {
        Vm = new(Settings.Load());
        _network.Source = Vm.Settings.Source;
        _games = new(_network); _content = new(_network); _java = new(_network); _engine = new(_network);
        DataContext = Vm;
        InitializeComponent();
        RestoreBackground();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ready = true;
        var recovered = InstallEngine.Recover();
        if (recovered.Count > 0) Vm.Error = string.Join("\n", recovered);
        LoadInstances();
        RefreshHistory();
        await LoadGameList();
        if (Environment.GetCommandLineArgs().Contains("--capture"))
            await CaptureUi();
    }
    private async Task Guard(Func<Task> action, bool preparation = false)
    {
        Vm.Error = "";
        if (preparation) Vm.Preparing = true;
        try { await action(); }
        catch (OperationCanceledException) { Vm.Status = "操作已取消"; }
        catch (Exception ex)
        {
            Vm.Error = ex.Message;
            File.AppendAllText(Path.Combine(Settings.DataDirectory, "errors.log"), DateTime.Now + "\n" + ex + "\n");
        }
        finally { if (preparation) Vm.Preparing = false; }
    }
    private async Task LoadGameList()
    {
        Vm.Loading = true;
        await Guard(async () =>
        {
            _releases = await _games.Releases();
            Vm.GameFilters.Clear(); Vm.GameFilters.Add(new("", "全部游戏版本"));
            foreach (var game in _releases) Vm.GameFilters.Add(new(game.Id, game.Id));
            FilterGames();
            Vm.SelectedGame ??= Vm.Games.FirstOrDefault();
            Vm.Status = $"{_releases.Count} 个正式版本";
        });
        Vm.Loading = false;
    }
    private void FilterGames()
    {
        var installed = Vm.Instances.Select(i => i.GameVersion).ToHashSet();
        var selected = Vm.SelectedGame?.Id;
        var query = Vm.IsGame ? Vm.Query.Trim() : "";
        var rows = _releases.Where(r => r.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(r => new GameRow(r, installed.Contains(r.Id)))
            .Where(r => Vm.InstalledFilter == "" || (Vm.InstalledFilter == "yes" ? r.Installed : !r.Installed)).ToList();
        Vm.Games.Clear();
        foreach (var row in rows) Vm.Games.Add(row);
        Vm.SelectedGame = rows.FirstOrDefault(r => r.Id == selected) ?? rows.FirstOrDefault();
        if (Vm.IsGame) Vm.Status = $"{rows.Count} 个版本 · {Vm.Instances.Count} 个本地安装";
    }
    private void LoadInstances(string? preferred = null)
    {
        var selectedId = preferred ?? Vm.Target?.Id ?? Vm.Settings.SelectedInstance;
        Vm.Instances.Clear();
        foreach (var instance in PclIntegration.Discover(Vm.Settings.MinecraftRoot, Vm.Settings.PclExecutable))
            Vm.Instances.Add(instance);
        Vm.Target = Vm.Instances.FirstOrDefault(i => i.Id == selectedId) ?? Vm.Instances.FirstOrDefault();
        Vm.Changed(nameof(Vm.TargetDirectory));
    }
    private void ChooseRoot(object sender, RoutedEventArgs e)
    {
        if (Vm.Running || Vm.Preparing) return;
        var dialog = new OpenFolderDialog { Title = "选择目标 .minecraft 文件夹", InitialDirectory = Vm.Settings.MinecraftRoot };
        if (dialog.ShowDialog(this) != true) return;
        Vm.Settings.MinecraftRoot = dialog.FolderName;
        Vm.RefreshSettings(); LoadInstances(); FilterGames();
    }
    private void AssociatePcl(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        var dialog = new OpenFileDialog { Title = "关联 PCL 启动器", Filter = "PCL 启动器 (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) != true) return;
        var info = FileVersionInfo.GetVersionInfo(dialog.FileName);
        if ((info.FileDescription ?? "").Contains("Kingsoft", StringComparison.OrdinalIgnoreCase) ||
            (info.ProductName ?? "").Contains("Internet Security", StringComparison.OrdinalIgnoreCase))
        {
            Vm.Error = "所选文件的产品信息是金山安全安装程序，请选择实际的 PCL 启动器。";
            return;
        }
        var identity = (info.FileDescription ?? "") + " " + (info.ProductName ?? "");
        if (!identity.Contains("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) &&
            !identity.Contains("PCL", StringComparison.OrdinalIgnoreCase))
        {
            Vm.Error = "所选程序的产品信息无法识别为 PCL，请选择实际的 PCL 可执行文件。";
            return;
        }
        Vm.Settings.PclExecutable = dialog.FileName;
        Vm.RefreshSettings(); LoadInstances();
    }
    private void RefreshInstances(object sender, RoutedEventArgs e) { LoadInstances(); FilterGames(); }
    private void TargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || Vm.Target is null) return;
        Vm.Settings.SelectedInstance = Vm.Target.Id;
        Vm.FilterGame = Vm.Target.GameVersion;
        Vm.FilterLoader = Vm.Page is PageKind.Mod or PageKind.Modpack ? (Vm.Target.Loader == "vanilla" ? "" : Vm.Target.Loader) : "";
        Vm.RefreshSettings();
    }
    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (Vm.Running) { Vm.Error = "任务执行期间请保持下载来源不变。"; return; }
        _network.Source = Vm.Settings.Source;
        Vm.Settings.Save();
    }
    private async void Navigate(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton button) return;
        var next = Enum.Parse<PageKind>(button.Tag.ToString()!);
        var navigation = ++_navigation;
        if (next == Vm.Page) return;
        if (Vm.IsCatalog)
            _filterStates[Vm.Page] = new(Vm.Query, Vm.FilterGame, Vm.FilterLoader, Vm.FilterCategory, Vm.FilterFeature, Vm.FilterExtra, Vm.Sort);
        _search?.Cancel();
        Vm.Loading = false;
        _suppress = true;
        Vm.Page = next;
        Vm.Query = "";
        Vm.Error = "";
        Vm.SelectedProject = null;
        Vm.ProjectVersions.Clear();
        Vm.Projects.Clear();
        _suppress = false;
        if (Vm.IsGame) { FilterGames(); await UpdateGameSelection(); }
        else if (Vm.IsCatalog)
        {
            await Guard(async () =>
            {
                if (_categories.Count == 0) _categories = await _content.Categories();
                if (_loaderTags.Count == 0) _loaderTags = await _content.LoaderTags();
                if (navigation != _navigation) return;
                PopulateFilters();
                if (_filterStates.TryGetValue(next, out var state))
                {
                    Vm.Query = state.Query; Vm.FilterGame = state.Game; Vm.FilterLoader = state.Loader;
                    Vm.FilterCategory = state.Category; Vm.FilterFeature = state.Feature; Vm.FilterExtra = state.Extra; Vm.Sort = state.Sort;
                }
                else
                {
                    Vm.FilterGame = Vm.Target?.GameVersion ?? "";
                    Vm.FilterLoader = next is PageKind.Mod or PageKind.Modpack && Vm.Target?.Loader is { } l && l != "vanilla" ? l : "";
                    Vm.Sort = "downloads";
                }
                _offset = 0; await Search();
            });
        }
        else if (Vm.IsJava) await LoadJava();
        else RefreshHistory();
        await Dispatcher.InvokeAsync(EnsureFilterSelections, DispatcherPriority.Loaded);
    }
    private void EnsureFilterSelections()
    {
        foreach (var combo in VisualChildren<ComboBox>(this))
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0 && combo.Items[0] is Choice { Value: "" })
                combo.SetCurrentValue(ComboBox.SelectedIndexProperty, 0);
    }
    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }
    private void PopulateFilters()
    {
        static void Populate(System.Collections.ObjectModel.ObservableCollection<Choice> list, IEnumerable<Choice> values, string all)
        { list.Clear(); list.Add(new("", all)); foreach (var value in values) list.Add(value); }
        var kind = Vm.ProjectType;
        Populate(Vm.CategoryFilters, _categories.Where(t => t.ProjectType == kind && t.Header == "categories").Select(t => new Choice(t.Name, TagLabel(t.Name))), "全部分类");
        Populate(Vm.FeatureFilters, _categories.Where(t => t.ProjectType == kind && t.Header == "features").Select(t => new Choice(t.Name, TagLabel(t.Name))), "全部特性");
        var header = Vm.IsShader ? "performance impact" : "resolutions";
        Populate(Vm.ExtraFilters, _categories.Where(t => t.ProjectType == kind && t.Header == header).Select(t => new Choice(t.Name, TagLabel(t.Name))), Vm.IsShader ? "全部性能标签" : "全部分辨率");
        var loaders = _loaderTags.Where(l => l.ProjectTypes.Contains(kind)).Select(l => l.Name);
        Populate(Vm.LoaderFilters, loaders.Select(l => new Choice(l, l switch { "iris" => "Iris", "optifine" => "OptiFine", "canvas" => "Canvas", "vanilla" => "Vanilla", "fabric" => "Fabric", "forge" => "Forge", "neoforge" => "NeoForge", "quilt" => "Quilt", _ => l })), Vm.IsShader ? "全部光影平台" : "全部加载器");
        Vm.FilterLoader = ""; Vm.FilterCategory = ""; Vm.FilterFeature = ""; Vm.FilterExtra = "";
        Vm.Changed(null);
    }
    private static string TagLabel(string tag) => tag switch
    {
        "adventure" => "冒险", "technology" => "科技", "magic" => "魔法", "optimization" => "性能优化",
        "utility" => "实用工具", "library" => "依赖库", "decoration" => "装饰", "equipment" => "装备",
        "worldgen" => "世界生成", "mobs" => "生物", "food" => "食物", "storage" => "存储",
        "transportation" => "交通", "management" => "管理", "social" => "社交", "economy" => "经济",
        "cursed" => "趣味", "game-mechanics" => "游戏机制", "minigame" => "小游戏",
        "quests" => "任务", "lightweight" => "轻量", "challenging" => "挑战", "multiplayer" => "多人",
        "combat" => "战斗", "kitchen-sink" => "综合", "realistic" => "写实", "fantasy" => "奇幻",
        "cartoon" => "卡通", "semi-realistic" => "半写实", "vanilla-like" => "原版风格", "simplistic" => "简约",
        "themed" => "主题", "tweaks" => "细节调整", "potato" => "极低负载", "low" => "低负载",
        "medium" => "中等负载", "high" => "高负载", "screenshot" => "截图级",
        "bloom" => "泛光", "shadows" => "阴影", "colored-lighting" => "彩色光照", "path-tracing" => "路径追踪",
        "pbr" => "PBR", "reflections" => "反射", "foliage" => "植被", "atmosphere" => "大气",
        "audio" => "音效", "blocks" => "方块", "entities" => "实体", "gui" => "界面", "items" => "物品",
        "locale" => "语言", "models" => "模型", "fonts" => "字体", "core-shaders" => "核心着色器", "sky" => "天空",
        _ => tag
    };
    private async Task Search()
    {
        _search?.Cancel(); _search?.Dispose(); _search = new();
        var token = _search.Token;
        var page = Vm.Page;
        Vm.Loading = true;
        try
        {
            var result = await _content.Search(Vm.ProjectType, Vm.Query.Trim(), Vm.FilterGame, Vm.FilterLoader,
                Vm.FilterCategory, Vm.Sort, _offset, token, Vm.FilterFeature, Vm.FilterExtra);
            if (token.IsCancellationRequested || Vm.Page != page) return;
            Vm.Projects.Clear(); Vm.ProjectVersions.Clear(); Vm.SelectedProject = null;
            foreach (var project in result.Projects)
            {
                var row = new ProjectRow(project);
                Vm.Projects.Add(row);
                _ = LoadProjectIcon(row, token);
            }
            Vm.HasNext = _offset + result.Projects.Count < result.Total;
            Vm.Status = result.Total == 0 ? "没有匹配结果" : $"{result.Total:N0} 个项目 · 第 {_offset / 30 + 1} 页";
            Vm.SelectedProject = Vm.Projects.FirstOrDefault();
        }
        finally { if (!token.IsCancellationRequested) Vm.Loading = false; }
    }
    private static async Task LoadProjectIcon(ProjectRow row, CancellationToken ct)
    {
        var image = await ProjectIcons.Load(row.IconUrl, ct);
        if (!ct.IsCancellationRequested) row.Icon = image;
    }
    private async void SearchCatalog(object sender, RoutedEventArgs e) { _offset = 0; await Guard(Search); }
    private async void SearchKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter && !Vm.Loading) { _offset = 0; await Guard(Search); } }
    private async void NextPage(object sender, RoutedEventArgs e) { if (!Vm.Loading && Vm.HasNext) { _offset += 30; await Guard(Search); } }
    private async void PreviousPage(object sender, RoutedEventArgs e) { if (!Vm.Loading && _offset > 0) { _offset -= 30; await Guard(Search); } }
    private async void ProjectSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _suppress) return;
        var project = Vm.SelectedProject;
        var request = ++_projectSelection;
        Vm.ProjectVersions.Clear();
        if (project is null) return;
        await Guard(async () =>
        {
            var loader = Vm.Page is PageKind.Mod or PageKind.Modpack or PageKind.Shader ? Vm.FilterLoader : "";
            var versions = await _content.Versions(project.Project.Id, Vm.FilterGame, loader);
            if (request != _projectSelection || project != Vm.SelectedProject) return;
            foreach (var version in versions) Vm.ProjectVersions.Add(version);
            Vm.SelectedVersion = Vm.ProjectVersions.FirstOrDefault(v => v.VersionType == "release") ?? Vm.ProjectVersions.FirstOrDefault();
        });
    }
    private void GameSearchChanged(object sender, RoutedEventArgs e) { if (_ready && Vm.IsGame && !_suppress) FilterGames(); }
    private async void GameSelected(object sender, SelectionChangedEventArgs e)
    { if (_ready && !_suppress && Vm.IsGame) await UpdateGameSelection(); }
    private async Task UpdateGameSelection()
    {
        var selected = Vm.SelectedGame;
        var request = ++_gameSelection;
        if (selected is null) return;
        DefaultVersionName();
        await Guard(async () =>
        {
            var metadata = await _games.Metadata(selected.Id);
            if (request != _gameSelection) return;
            Vm.JavaRequirement = (metadata["javaVersion"]?.Num("majorVersion") ?? 8).ToString();
            if (int.TryParse(Vm.JavaRequirement, out var major)) Vm.JavaMajor = major;
            if (Vm.IsFabric) await LoadFabric(selected.Id, request);
        });
    }
    private async Task LoadFabric(string game, int request)
    {
        var loaders = await _games.Loaders(game);
        if (request != _gameSelection) return;
        Vm.Loaders.Clear();
        foreach (var loader in loaders.Where(l => l.Stable)) Vm.Loaders.Add(loader);
        Vm.SelectedLoader = Vm.Loaders.FirstOrDefault();
        DefaultVersionName();
    }
    private async void LoaderKindChanged(object sender, SelectionChangedEventArgs e)
    { if (_ready && !_suppress) { DefaultVersionName(); if (Vm.IsFabric && Vm.SelectedGame is { } g) await Guard(() => LoadFabric(g.Id, _gameSelection)); } }
    private void LoaderSelected(object sender, SelectionChangedEventArgs e) { if (_ready && !_suppress) DefaultVersionName(); }
    private void DefaultVersionName()
    {
        if (Vm.SelectedGame is not { } game) return;
        Vm.NewId = Vm.IsFabric ? $"{game.Id}-Fabric-{Vm.SelectedLoader?.Version ?? "loader"}" : game.Id;
    }
    private void RequireRoot()
    {
        if (Vm.Settings.MinecraftRoot.Length == 0) throw new InvalidOperationException("请先选择目标 .minecraft 文件夹。");
    }
    private Instance RequireTarget() => Vm.Target ?? throw new InvalidOperationException("请先选择已安装的游戏版本。");
    private void ShowPlan(InstallPlan plan)
    {
        if (plan.Files.Count == 0) throw new InvalidOperationException("所有所需文件均已安装，无需新增文件。");
        _plan = plan;
        plan.Details.RemoveAll(d => d.StartsWith("来源模式：", StringComparison.Ordinal));
        plan.Details.Add($"来源模式：{Vm.SourceChoices.First(c => c.Value == Vm.SourceValue).Label}");
        plan.Details.Add("确认前请结束目标游戏及 PCL 中对同一目录执行的安装任务。");
        Vm.PlanTitle = plan.Title;
        Vm.PlanSummary = $"{plan.Files.Count:N0} 个文件 · {JsonTools.Size(plan.Bytes)} · {plan.Conflicts} 个已有文件";
        Vm.PlanDetails.Clear(); Vm.PlanFiles.Clear();
        foreach (var detail in plan.Details) Vm.PlanDetails.Add(detail);
        foreach (var file in plan.Files) Vm.PlanFiles.Add(file);
        Vm.ShowPlan = true;
    }
    private async void PrepareGame(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        await Guard(async () =>
        {
            RequireRoot();
            var game = Vm.SelectedGame ?? throw new InvalidOperationException("请选择游戏版本。");
            var root = Vm.Settings.MinecraftRoot;
            var loader = Vm.IsFabric ? Vm.SelectedLoader?.Version ?? throw new InvalidOperationException("该版本没有可用的 Fabric 稳定版。") : null;
            var id = JsonTools.SafeName(Vm.NewId);
            var plan = await _games.Plan(root, game.Id, loader, id);
            if (loader is not null && Vm.FabricApi)
            {
                var directory = PclIntegration.Resolve(root, id, Vm.Settings.PclExecutable, "fabric");
                if (directory.Error is not null) throw new InvalidOperationException(directory.Error);
                var target = new Instance(id, game.Id, "fabric", directory.Directory, directory.Isolated, null);
                var api = (await _content.Versions("fabric-api", game.Id, "fabric")).FirstOrDefault(v => v.VersionType == "release")
                    ?? throw new InvalidOperationException("没有找到当前游戏的 Fabric API 正式版，可以取消补齐 Fabric API 后安装。");
                _content.ResetInstalledIndex();
                await _content.AddDependencies(plan, api, target, new(), CancellationToken.None);
            }
            ShowPlan(plan);
        }, true);
    }
    private async void PrepareContent(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        await Guard(async () =>
        {
            var version = Vm.SelectedVersion ?? throw new InvalidOperationException("请选择有可用文件的项目版本。");
            if (Vm.Page == PageKind.Modpack)
                throw new InvalidOperationException("本阶段支持整合包搜索和仅下载。自动安装与本地 ZIP 导入将在整合包阶段接入。");
            var target = RequireTarget();
            ShowPlan(await _content.Plan(version, target, Vm.ProjectType));
        }, true);
    }
    private async void DownloadContent(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        await Guard(() =>
        {
            var version = Vm.SelectedVersion ?? throw new InvalidOperationException("请选择可下载的文件版本。");
            var file = version.Primary;
            var dialog = new SaveFileDialog { Title = "仅下载文件", FileName = file.Filename, Filter = "原始文件|*" + Path.GetExtension(file.Filename) };
            if (dialog.ShowDialog(this) != true) return Task.CompletedTask;
            var plan = new InstallPlan { Title = version.Name, Type = "仅下载", Destination = dialog.FileName, TargetId = "仅下载" };
            var planned = ContentService.File(file, Path.GetDirectoryName(dialog.FileName)!, version.Name);
            planned.Destination = dialog.FileName; plan.Add(planned);
            plan.Details.Add("保留原始文件，不写入游戏目录。");
            ShowPlan(plan);
            return Task.CompletedTask;
        });
    }
    private async void DownloadGameJar(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        await Guard(async () =>
        {
            var game = Vm.SelectedGame ?? throw new InvalidOperationException("请选择游戏版本。");
            var metadata = await _games.Metadata(game.Id);
            var dialog = new SaveFileDialog { Title = "仅下载客户端 JAR", FileName = $"minecraft-{game.Id}.jar", Filter = "JAR 文件|*.jar" };
            if (dialog.ShowDialog(this) != true) return;
            var plan = new InstallPlan { Title = $"Minecraft {game.Id} 客户端 JAR", Destination = dialog.FileName, Type = "仅下载", TargetId = "仅下载" };
            GameService.AddDownload(plan, metadata["downloads"]!["client"]!, dialog.FileName, plan.Title);
            plan.Details.Add("仅下载客户端 JAR；这不是完整可启动的游戏安装。");
            ShowPlan(plan);
        });
    }
    private async Task LoadJava()
    {
        Vm.Loading = true;
        await Guard(async () =>
        {
            _allJavaMajors = await _java.Majors();
            RefreshJavaDirectories();
            ApplyJavaFilters();
            Vm.Status = "Eclipse Adoptium · Windows x64";
        });
        Vm.Loading = false;
    }
    private void RefreshJavaDirectories()
    {
        Vm.InstalledJavas.Clear();
        if (!Directory.Exists(Vm.Settings.JavaDirectory)) return;
        foreach (var folder in Directory.EnumerateDirectories(Vm.Settings.JavaDirectory))
            if (File.Exists(Path.Combine(folder, "bin", "java.exe"))) Vm.InstalledJavas.Add(Path.GetFileName(folder));
    }
    private void ApplyJavaFilters()
    {
        if (_allJavaMajors.Count == 0 || _suppress) return;
        var current = Vm.JavaMajor;
        var installed = Vm.InstalledJavas.Where(s => s.EndsWith("-" + Vm.JavaType, StringComparison.OrdinalIgnoreCase))
            .Select(s => Regex.Match(s, @"^jdk-?(\d+)")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        var choices = _allJavaMajors.Where(m => Vm.JavaInstalled == "" || (Vm.JavaInstalled == "yes" ? installed.Contains(m) : !installed.Contains(m))).ToList();
        _suppress = true;
        Vm.JavaMajors.Clear(); foreach (var major in choices) Vm.JavaMajors.Add(major);
        Vm.JavaMajor = choices.Contains(current) ? current : choices.FirstOrDefault();
        _suppress = false;
    }
    private void JavaFiltersChanged(object sender, SelectionChangedEventArgs e) { if (_ready && !_suppress && Vm.IsJava) ApplyJavaFilters(); }
    private void ChooseJavaDirectory(object sender, RoutedEventArgs e)
    {
        if (Vm.Running) return;
        var dialog = new OpenFolderDialog { Title = "选择 Java 多版本安装目录", InitialDirectory = Directory.Exists(Vm.Settings.JavaDirectory) ? Vm.Settings.JavaDirectory : "" };
        if (dialog.ShowDialog(this) != true) return;
        Vm.Settings.JavaDirectory = dialog.FolderName; Vm.RefreshSettings(); RefreshJavaDirectories(); ApplyJavaFilters();
    }
    private async void PrepareJava(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanPrepare) return;
        await Guard(async () =>
        {
            if (Vm.JavaMajor <= 0) throw new InvalidOperationException("请选择 Java 主版本。");
            var release = await _java.Release(Vm.JavaMajor, Vm.JavaType);
            ShowPlan(_java.Plan(release, Vm.Settings.JavaDirectory));
        }, true);
    }
    private async void ExecutePlan(object sender, RoutedEventArgs e)
    {
        if (_plan is null || !Vm.CanPrepare) return;
        var plan = _plan;
        await Guard(async () =>
        {
            EnsureGameStopped(plan);
            Vm.ShowPlan = false; Vm.Running = true; Vm.Progress = 0;
            Vm.ProgressTitle = plan.Title; Vm.ProgressDetail = "等待下载";
            _operation = new();
            var reporter = new Progress<TransferProgress>(p =>
            {
                Vm.ProgressTitle = p.Phase + " · " + plan.Title;
                Vm.ProgressDetail = $"{p.Done:N0} / {p.Total:N0} · {JsonTools.Size(p.Bytes)}" +
                    (p.TotalBytes > 0 ? " / " + JsonTools.Size(p.TotalBytes) : "");
                Vm.Progress = p.Phase switch
                {
                    "已完成" => 100,
                    "准备写入" => 90,
                    "安装文件" => 90 + (p.Total > 0 ? p.Done * 10d / p.Total : 0),
                    _ => p.TotalBytes > 0 ? Math.Clamp(p.Bytes * 90d / p.TotalBytes, 0, 90)
                        : p.Total > 0 ? p.Done * 90d / p.Total : 0
                };
            });
            try
            {
                Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, "plans"));
                await File.WriteAllTextAsync(Path.Combine(Settings.DataDirectory, "plans", plan.Id + ".json"), JsonSerializer.Serialize(plan, JsonTools.Options));
                InstallEngine.AddHistory(new(plan.Id, plan.Title, plan.Destination, plan.Type, "进行中", DateTime.Now, "正在下载并校验", plan.TargetId));
                await Task.Run(() => _engine.Execute(plan, reporter, _operation.Token));
                Vm.Progress = 100; Vm.ProgressTitle = "安装完成 · " + plan.Title;
                Vm.ProgressDetail = $"{plan.Files.Count:N0} 个文件";
                if (plan.Type == "游戏") Vm.Settings.SelectedInstance = plan.TargetId;
                LoadInstances(plan.Type == "游戏" ? plan.TargetId : null); FilterGames(); RefreshJavaDirectories(); ApplyJavaFilters();
                if (_engine.Warning.Length > 0) Vm.Error = _engine.Warning;
            }
            catch (OperationCanceledException) { Vm.ProgressTitle = "已取消 · " + plan.Title; Vm.ProgressDetail = "本次写入已恢复，已校验缓存保留"; }
            catch { Vm.ProgressTitle = "安装失败 · " + plan.Title; Vm.ProgressDetail = "可在任务中重试"; throw; }
            finally { Vm.Running = false; _operation.Dispose(); _operation = null; RefreshHistory(); }
        });
    }
    private void EnsureGameStopped(InstallPlan plan)
    {
        if (plan.Type is "Java" or "仅下载") return;
        var locator = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
        if (locator is null) return;
        try
        {
            dynamic instance = Activator.CreateInstance(locator)!;
            dynamic service = instance.ConnectServer(".", @"root\cimv2");
            foreach (dynamic process in service.ExecQuery("SELECT CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'"))
            {
                string command = process.CommandLine ?? "";
                if (command.Contains(Vm.Settings.MinecraftRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("检测到目标 .minecraft 下的 Java 游戏进程。请退出游戏后安装。");
            }
        }
        catch (InvalidOperationException) { throw; }
        catch { /* File transaction still detects locked files; WMI may be disabled by system policy. */ }
    }
    private void RefreshHistory()
    {
        var history = InstallEngine.History();
        var targets = history.Select(h => h.TargetId).Where(t => t.Length > 0).Distinct().ToList();
        if (!targets.SequenceEqual(Vm.TaskTargets.Skip(1).Select(c => c.Value)))
        {
            var current = Vm.TaskTarget;
            _suppress = true;
            Vm.TaskTargets.Clear(); Vm.TaskTargets.Add(new("", "全部目标"));
            foreach (var target in targets) Vm.TaskTargets.Add(new(target, target));
            Vm.TaskTarget = targets.Contains(current) ? current : "";
            Vm.Changed(nameof(Vm.TaskTarget));
            _suppress = false;
        }
        Vm.History.Clear();
        foreach (var entry in history.Where(h => (Vm.TaskFilter == "" || h.Status == Vm.TaskFilter) &&
            (Vm.TaskType == "" || h.Type == Vm.TaskType) && (Vm.TaskTarget == "" || h.TargetId == Vm.TaskTarget))) Vm.History.Add(entry);
        if (Vm.IsTasks) Vm.Status = $"{Vm.History.Count} 条任务记录";
    }
    private void TaskFilterChanged(object sender, SelectionChangedEventArgs e) { if (_ready && !_suppress) RefreshHistory(); }
    private async void RetryPlan(object sender, RoutedEventArgs e)
    {
        await Guard(async () =>
        {
            if (HistoryList.SelectedItem is not HistoryEntry entry) throw new InvalidOperationException("请先选择需要重试的任务。");
            var path = Path.Combine(Settings.DataDirectory, "plans", entry.Id + ".json");
            if (!File.Exists(path)) throw new InvalidOperationException("该任务没有保存安装计划。");
            var plan = JsonSerializer.Deserialize<InstallPlan>(await File.ReadAllTextAsync(path))!;
            if (plan.Files.Any(f => f.Action == FileAction.ExtractJava && Directory.Exists(f.Destination)))
                throw new InvalidOperationException("Java 目录已存在，不能覆盖重装。");
            ShowPlan(plan);
        });
    }
    private void CancelTask(object sender, RoutedEventArgs e) => _operation?.Cancel();
    private void ClosePlan(object sender, RoutedEventArgs e) => Vm.ShowPlan = false;
    private void ClosePlanBackdrop(object sender, MouseButtonEventArgs e) => Vm.ShowPlan = false;
    private void DismissError(object sender, RoutedEventArgs e) => Vm.Error = "";
    private async void RefreshPage(object sender, RoutedEventArgs e)
    {
        if (Vm.IsGame) { _games.Invalidate(); await LoadGameList(); }
        else if (Vm.IsCatalog) { _offset = 0; await Guard(Search); }
        else if (Vm.IsJava) await LoadJava();
        else RefreshHistory();
    }
    private void OpenProject(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedProject is { } project) Open("https://modrinth.com/" + Vm.ProjectType + "/" + project.Project.Slug);
    }
    private void OpenJavaDirectory(object sender, RoutedEventArgs e)
    { if (Directory.Exists(Vm.Settings.JavaDirectory)) Open(Vm.Settings.JavaDirectory); }
    private static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        Vm.Settings.Theme = Vm.Settings.Theme == "Light" ? "Dark" : "Light";
        App.ApplyTheme(Vm.Settings.Theme); Vm.Settings.Save();
    }
    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Vm.Running) { Vm.Error = "请先取消当前任务并等待回滚完成，再关闭 MineDock。"; e.Cancel = true; return; }
        _search?.Cancel(); Vm.Settings.Save(); _network.Dispose();
    }
    private async Task CaptureUi()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--capture");
        var folder = index + 1 < args.Length ? args[index + 1] : Settings.DataDirectory;
        Directory.CreateDirectory(folder);
        App.ApplyTheme("Dark");
        await Task.Delay(1500);
        SaveCapture(Path.Combine(folder, "game-dark.png"));
        WindowState = WindowState.Normal;
        Width = 1080; Height = 740;
        await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle);
        await Task.Delay(500);
        var compact = new { Width, Height, ActualWidth, ActualHeight, WindowState };
        SaveCapture(Path.Combine(folder, "game-compact.png"));
        Width = 1340; Height = 860;
        NavMod.IsChecked = true;
        for (var n = 0; n < 90 && (Vm.Loading || Vm.Projects.Count == 0); n++) await Task.Delay(500);
        for (var n = 0; n < 30 && Vm.Projects.Take(5).Any(p => p.Icon is null); n++) await Task.Delay(500);
        await Task.Delay(500); SaveCapture(Path.Combine(folder, "mods-dark.png"));
        var modResults = new { Count = Vm.Projects.Count, Versions = Vm.ProjectVersions.Count, Icons = Vm.Projects.Count(p => p.Icon is not null), Error = Vm.Error };
        var filters = VisualChildren<ComboBox>(this).Where(c => c.IsVisible).Select(c => new
        {
            Binding = BindingOperations.GetBinding(c, ComboBox.SelectedValueProperty)?.Path.Path ?? "",
            c.SelectedIndex, Value = c.SelectedValue?.ToString(), Label = c.SelectionBoxItem?.ToString()
        }).ToArray();
        App.ApplyTheme("Light"); await Task.Delay(300); SaveCapture(Path.Combine(folder, "mods-light.png"));
        App.ApplyTheme("Dark");
        NavShader.IsChecked = true;
        for (var n = 0; n < 90 && (Vm.Loading || Vm.Projects.Count == 0); n++) await Task.Delay(500);
        await Task.Delay(1000); SaveCapture(Path.Combine(folder, "shaders-dark.png"));
        var shaderResults = new { Count = Vm.Projects.Count, Filters = Vm.ExtraFilters.Count, Error = Vm.Error };
        NavResourcepack.IsChecked = true;
        for (var n = 0; n < 90 && (Vm.Loading || Vm.Projects.Count == 0); n++) await Task.Delay(500);
        await Task.Delay(1000); SaveCapture(Path.Combine(folder, "resourcepacks-dark.png"));
        var textureResults = new { Count = Vm.Projects.Count, Filters = Vm.ExtraFilters.Count, Error = Vm.Error };
        NavModpack.IsChecked = true;
        for (var n = 0; n < 90 && (Vm.Loading || Vm.Projects.Count == 0); n++) await Task.Delay(500);
        await Task.Delay(1000); SaveCapture(Path.Combine(folder, "modpacks-dark.png"));
        var packResults = new { Count = Vm.Projects.Count, Error = Vm.Error };
        NavJava.IsChecked = true;
        for (var n = 0; n < 90 && (Vm.Loading || Vm.JavaMajors.Count == 0); n++) await Task.Delay(500);
        await Task.Delay(500); SaveCapture(Path.Combine(folder, "java-dark.png"));
        var javaResults = new { Count = Vm.JavaMajors.Count, Error = Vm.Error };
        var release = await _java.Release(21, "jre");
        ShowPlan(_java.Plan(release, Path.Combine(folder, "java-preview")));
        await Task.Delay(300); SaveCapture(Path.Combine(folder, "plan-dark.png"));
        Vm.ShowPlan = false;
        NavTasks.IsChecked = true;
        await Task.Delay(300); SaveCapture(Path.Combine(folder, "tasks-dark.png"));
        await File.WriteAllTextAsync(Path.Combine(folder, "ui-results.json"), JsonSerializer.Serialize(new
        {
            Games = Vm.Games.Count, Mod = modResults, Shader = shaderResults, Resourcepack = textureResults,
            Modpack = packResults, Java = javaResults, Filters = filters, Compact = compact,
            Error = Vm.Error, WindowWidth = ActualWidth, WindowHeight = ActualHeight
        }, JsonTools.Options));
        Close();
    }
    private void SaveCapture(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
    private record CatalogState(string Query, string Game, string Loader, string Category, string Feature, string Extra, string Sort);
}
