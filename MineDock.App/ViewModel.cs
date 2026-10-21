using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using MineDock.Core;

namespace MineDock.App;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Changed(property); return true;
    }
    public void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public record Choice(string Value, string Label)
{
    public override string ToString() => Label;
}
public record GameRow(GameRelease Release, bool Installed)
{
    public string Id => Release.Id;
    public string Date => Release.ReleaseDate.ToString("yyyy.MM.dd");
    public string Status => Installed ? "已安装" : "正式版";
}
public class ProjectRow(SearchProject project) : Observable
{
    public SearchProject Project { get; } = project;
    internal bool IconLoading { get; set; }
    private ImageSource? _icon;
    public ImageSource? Icon { get => _icon; set => Set(ref _icon, value); }
    public string Title => Project.Title;
    public string Description => Project.Description;
    public string IconUrl => Project.IconUrl;
    public string Author => Project.Author;
    public string Downloads => Project.Downloads > 10000 ? $"{Project.Downloads / 10000d:0.#} 万" : Project.Downloads.ToString("N0");
    public string Tags => string.Join("   ·   ", Project.Categories.Take(4));
}
public class MainViewModel : Observable
{
    public Settings Settings { get; }
    public MainViewModel(Settings settings) => Settings = settings;
    private ImageSource? _backgroundImage;
    public ImageSource? BackgroundImage
    {
        get => _backgroundImage;
        set { if (Set(ref _backgroundImage, value)) Changed(nameof(HasBackground)); }
    }
    public bool HasBackground => BackgroundImage is not null;
    public string BackgroundName => HasBackground ? Settings.BackgroundImageName : "未设置背景";
    public double BackgroundIntensity
    {
        get => Settings.BackgroundIntensity;
        set
        {
            var intensity = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.55;
            if (Settings.BackgroundIntensity == intensity) return;
            Settings.BackgroundIntensity = intensity;
            Changed(); Changed(nameof(BackgroundOpacity)); Changed(nameof(BackgroundPercent));
        }
    }
    public double BackgroundOpacity => BackgroundIntensity * (App.IsDarkTheme ? 0.26 : 0.15);
    public string BackgroundPercent => $"{BackgroundIntensity:P0}";
    public string BackgroundStretch
    {
        get => Settings.BackgroundStretch;
        set { Settings.BackgroundStretch = value == "Uniform" ? "Uniform" : "UniformToFill"; Changed(); }
    }
    public Choice[] BackgroundStretchChoices { get; } = [new("UniformToFill", "填充"), new("Uniform", "适应")];
    private string _backgroundError = "";
    public string BackgroundError { get => _backgroundError; set => Set(ref _backgroundError, value); }
    private bool _backgroundBusy;
    public bool BackgroundBusy { get => _backgroundBusy; set { if (Set(ref _backgroundBusy, value)) Changed(nameof(CanChangeBackground)); } }
    public bool CanChangeBackground => !BackgroundBusy;
    public ObservableCollection<GameRow> Games { get; } = [];
    public ObservableCollection<ProjectRow> Projects { get; } = [];
    public ObservableCollection<Instance> Instances { get; } = [];
    public ObservableCollection<LoaderRelease> Loaders { get; } = [];
    public ObservableCollection<ProjectVersion> ProjectVersions { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];
    public ObservableCollection<Choice> GameFilters { get; } = [new("", "全部游戏版本")];
    public ObservableCollection<Choice> LoaderFilters { get; } = [new("", "全部加载器")];
    public ObservableCollection<Choice> CategoryFilters { get; } = [new("", "全部分类")];
    public ObservableCollection<Choice> FeatureFilters { get; } = [new("", "全部特性")];
    public ObservableCollection<Choice> ExtraFilters { get; } = [new("", "全部规格")];
    public ObservableCollection<int> JavaMajors { get; } = [];
    public ObservableCollection<string> InstalledJavas { get; } = [];
    public ObservableCollection<Choice> TaskTargets { get; } = [new("", "全部目标")];
    public Choice[] SourceChoices { get; } = [new("Auto", "自动 · BMCLAPI → 官方"), new("Official", "官方来源"), new("Bmclapi", "BMCLAPI")];
    public Choice[] SortChoices { get; } = [new("relevance", "相关度"), new("downloads", "下载最多"), new("updated", "最近更新"), new("newest", "最新发布"), new("follows", "关注最多")];
    public Choice[] GameLoaderChoices { get; } = [new("vanilla", "原版"), new("fabric", "Fabric")];
    public Choice[] InstalledChoices { get; } = [new("", "全部状态"), new("yes", "已安装"), new("no", "未安装")];
    public Choice[] JavaTypes { get; } = [new("jre", "JRE · 运行版"), new("jdk", "JDK · 开发版")];
    public Choice[] TaskFilters { get; } = [new("", "全部状态"), new("进行中", "进行中"), new("已完成", "已完成"), new("失败", "失败"), new("已取消", "已取消")];
    public Choice[] TaskTypes { get; } = [new("", "全部类型"), new("游戏", "游戏"), new("模组", "模组"), new("Java", "Java"),
        new("光影", "光影"), new("材质包", "材质包"), new("仅下载", "仅下载")];
    private PageKind _page;
    public PageKind Page { get => _page; set { if (Set(ref _page, value)) Changed(null); } }
    public bool IsGame => Page == PageKind.Game;
    public bool IsCatalog => Page is PageKind.Mod or PageKind.Modpack or PageKind.Shader or PageKind.Resourcepack;
    public bool IsJava => Page == PageKind.Java;
    public bool IsTasks => Page == PageKind.Tasks;
    public bool IsFabric => GameLoader == "fabric";
    public bool IsModpack => Page == PageKind.Modpack;
    public bool CanInstallCatalog => IsCatalog && !IsModpack;
    public bool IsShader => Page == PageKind.Shader;
    public bool IsResourcepack => Page == PageKind.Resourcepack;
    public bool ShowLoaderFilter => Page is not PageKind.Resourcepack;
    public bool ShowExtraFilter => Page is PageKind.Shader or PageKind.Resourcepack;
    public string ProjectType => Page switch { PageKind.Modpack => "modpack", PageKind.Shader => "shader", PageKind.Resourcepack => "resourcepack", _ => "mod" };
    public string PageTitle => Page switch { PageKind.Game => "游戏版本", PageKind.Modpack => "整合包", PageKind.Mod => "模组", PageKind.Shader => "光影", PageKind.Resourcepack => "材质包", PageKind.Java => "Java 运行环境", _ => "下载任务" };
    public string PageIcon => Page switch { PageKind.Game => "blocks", PageKind.Modpack => "package", PageKind.Mod => "puzzle", PageKind.Shader => "sparkles", PageKind.Resourcepack => "image", PageKind.Java => "coffee", _ => "download" };
    public string PageEyebrow => IsCatalog ? "MODRINTH" : IsJava ? "ECLIPSE TEMURIN" : IsTasks ? "ACTIVITY" : "MINECRAFT JAVA EDITION";
    public string RootLabel => Settings.MinecraftRoot.Length == 0 ? "未选择 .minecraft" : Settings.MinecraftRoot;
    public string LauncherLabel => Settings.LauncherExecutable.Length == 0 ? "关联启动器" : Settings.LauncherName + " 已关联";
    public string LauncherTooltip => Settings.LauncherExecutable.Length == 0
        ? "关联 PCL 或 HMCL，自动选择游戏目录" : Settings.LauncherExecutable + "\n点击更换 PCL / HMCL";
    public string LaunchLauncherLabel => Settings.LauncherExecutable.Length == 0
        ? "启动启动器" : "启动 " + Settings.LauncherName;
    public bool CanLaunchLauncher => CanPrepare && !LauncherStarting && Settings.LauncherExecutable.Length > 0;
    private bool _launcherStarting;
    public bool LauncherStarting
    {
        get => _launcherStarting;
        set { if (Set(ref _launcherStarting, value)) RefreshActions(); }
    }
    public string SourceValue { get => Settings.Source.ToString(); set { if (Enum.TryParse<SourceMode>(value, out var v)) { Settings.Source = v; Changed(); } } }
    private bool _loading, _preparing, _running, _showPlan;
    public bool Loading
    {
        get => _loading;
        set { if (Set(ref _loading, value)) RefreshPagination(); }
    }
    public bool Preparing { get => _preparing; set { if (Set(ref _preparing, value)) RefreshActions(); } }
    public bool Running { get => _running; set { if (Set(ref _running, value)) RefreshActions(); } }
    public bool CanPrepare => !Preparing && !Running && !LauncherStarting;
    private void RefreshActions() { Changed(nameof(CanPrepare)); Changed(nameof(CanLaunchLauncher)); }
    public bool ShowPlan { get => _showPlan; set => Set(ref _showPlan, value); }
    public int CurrentPage { get; private set; }
    public int TotalProjects { get; private set; }
    public int TotalPages => (int)(((long)TotalProjects + ContentService.SearchPageSize - 1) / ContentService.SearchPageSize);
    public bool CanPreviousPage => !Loading && CurrentPage > 1;
    public bool CanNextPage => !Loading && CurrentPage > 0 && CurrentPage < TotalPages;
    public bool CanJumpPage => !Loading && TotalPages > 0;
    public string ResultCountLabel => $"{TotalProjects:N0} 个项目";
    public string PageCountLabel => $"/ {TotalPages:N0} 页";
    private string _pageInput = "0", _pageError = "";
    public string PageInput
    {
        get => _pageInput;
        set { if (Set(ref _pageInput, value)) PageError = ""; }
    }
    public string PageError { get => _pageError; set => Set(ref _pageError, value); }
    public void SetCatalogPage(int page, int total)
    {
        TotalProjects = Math.Max(0, total);
        CurrentPage = TotalPages == 0 ? 0 : Math.Clamp(page, 1, TotalPages);
        RestorePageInput();
        Changed(nameof(CurrentPage)); Changed(nameof(TotalProjects)); Changed(nameof(TotalPages));
        Changed(nameof(ResultCountLabel)); Changed(nameof(PageCountLabel));
        RefreshPagination();
    }
    public void RestorePageInput()
    {
        PageInput = CurrentPage.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PageError = "";
    }
    private void RefreshPagination()
    {
        Changed(nameof(CanPreviousPage)); Changed(nameof(CanNextPage)); Changed(nameof(CanJumpPage));
    }
    private string _status = "正在连接版本服务", _error = "", _query = "", _gameLoader = "vanilla", _installed = "", _newId = "", _javaRequirement = "—";
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Error { get => _error; set { if (Set(ref _error, value)) Changed(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Query { get => _query; set => Set(ref _query, value); }
    public string GameLoader { get => _gameLoader; set { if (Set(ref _gameLoader, value)) Changed(nameof(IsFabric)); } }
    public string InstalledFilter { get => _installed; set => Set(ref _installed, value); }
    public string NewId { get => _newId; set => Set(ref _newId, value); }
    public string JavaRequirement { get => _javaRequirement; set => Set(ref _javaRequirement, value); }
    private GameRow? _selectedGame;
    public GameRow? SelectedGame { get => _selectedGame; set => Set(ref _selectedGame, value); }
    private Instance? _target;
    public Instance? Target { get => _target; set { if (Set(ref _target, value)) Changed(nameof(TargetDirectory)); } }
    public string TargetDirectory => Target?.DirectoryError ?? Target?.GameDirectory ?? "未选择目标版本";
    private LoaderRelease? _selectedLoader;
    public LoaderRelease? SelectedLoader { get => _selectedLoader; set => Set(ref _selectedLoader, value); }
    private ProjectRow? _selectedProject;
    public ProjectRow? SelectedProject { get => _selectedProject; set => Set(ref _selectedProject, value); }
    private ProjectVersion? _selectedVersion;
    public ProjectVersion? SelectedVersion { get => _selectedVersion; set => Set(ref _selectedVersion, value); }
    private bool _fabricApi = true;
    public bool FabricApi { get => _fabricApi; set => Set(ref _fabricApi, value); }
    private string _filterGame = "", _filterLoader = "", _filterCategory = "", _filterFeature = "", _filterExtra = "", _sort = "relevance";
    public string FilterGame { get => _filterGame; set => Set(ref _filterGame, value ?? ""); }
    public string FilterLoader { get => _filterLoader; set => Set(ref _filterLoader, value ?? ""); }
    public string FilterCategory { get => _filterCategory; set => Set(ref _filterCategory, value ?? ""); }
    public string FilterFeature { get => _filterFeature; set => Set(ref _filterFeature, value ?? ""); }
    public string FilterExtra { get => _filterExtra; set => Set(ref _filterExtra, value ?? ""); }
    public string Sort { get => _sort; set => Set(ref _sort, value); }
    private int _javaMajor = 21;
    private string _javaType = "jre", _taskFilter = "", _progressTitle = "暂无下载任务", _progressDetail = "等待安装计划", _planSummary = "", _planTitle = "";
    private double _progress;
    public int JavaMajor { get => _javaMajor; set => Set(ref _javaMajor, value); }
    public string JavaType { get => _javaType; set => Set(ref _javaType, value); }
    public string JavaDirectory => Settings.JavaDirectory;
    public string TaskFilter { get => _taskFilter; set => Set(ref _taskFilter, value); }
    private string _taskType = "", _taskTarget = "", _javaInstalled = "";
    public string TaskType { get => _taskType; set => Set(ref _taskType, value ?? ""); }
    public string TaskTarget { get => _taskTarget; set => Set(ref _taskTarget, value ?? ""); }
    public string JavaInstalled { get => _javaInstalled; set => Set(ref _javaInstalled, value ?? ""); }
    public string ProgressTitle { get => _progressTitle; set => Set(ref _progressTitle, value); }
    public string ProgressDetail { get => _progressDetail; set => Set(ref _progressDetail, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string PlanSummary { get => _planSummary; set => Set(ref _planSummary, value); }
    public string PlanTitle { get => _planTitle; set => Set(ref _planTitle, value); }
    public ObservableCollection<string> PlanDetails { get; } = [];
    public ObservableCollection<PlannedFile> PlanFiles { get; } = [];
    public void RefreshSettings() { Settings.Save(); Changed(null); }
}
