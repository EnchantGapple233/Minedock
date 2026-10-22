using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow
{
    private readonly Dictionary<PageKind, CatalogView> _catalogViews = [];
    private readonly Dictionary<VersionKey, Task<List<ProjectVersion>>> _versionRequests = [];
    private readonly CancellationTokenSource _catalogLifetime = new();
    private Task? _catalogTagsRequest;
    private string _activeCatalogContext = "";
    private DateTimeOffset _activeCatalogFetchedAt;
    private static string CatalogDirectory => Path.Combine(Settings.DataDirectory, "catalog-cache");
    private string CatalogContext => (Vm.Target?.GameVersion ?? "") + "|" + (Vm.Target?.Loader ?? "");
    private CatalogState CurrentFilters => new(Vm.Query, Vm.FilterGame, Vm.FilterLoader, Vm.FilterCategory,
        Vm.FilterFeature, Vm.FilterExtra, Vm.Sort);

    private void ApplyFilters(CatalogState state)
    {
        Vm.Query = state.Query; Vm.FilterGame = state.Game; Vm.FilterLoader = state.Loader;
        Vm.FilterCategory = state.Category; Vm.FilterFeature = state.Feature; Vm.FilterExtra = state.Extra;
        Vm.Sort = state.Sort;
    }

    private async Task EnsureCatalogTags()
    {
        async Task Fetch()
        {
            _categories = await _content.Categories(_catalogLifetime.Token);
            _loaderTags = await _content.LoaderTags(_catalogLifetime.Token);
        }
        try { await (_catalogTagsRequest ??= Fetch()); }
        catch { _catalogTagsRequest = null; throw; }
    }

    private void SaveCurrentCatalog()
    {
        if (!Vm.IsCatalog || _activeCatalogSearch is null) return;
        var snapshot = new CatalogSnapshot(1, Vm.Page, _activeCatalogContext, CurrentFilters, _activeCatalogSearch,
            Vm.CurrentPage, Vm.TotalProjects, Vm.Projects.Select(p => p.Project).ToArray(),
            Vm.SelectedProject?.Project.Id, Vm.SelectedVersion?.Id,
            VisualChildren<ScrollViewer>(ProjectList).FirstOrDefault()?.VerticalOffset ?? 0,
            _activeCatalogFetchedAt, _categories.ToArray(), _loaderTags.ToArray());
        _catalogViews[Vm.Page] = new(snapshot, Vm.Projects.ToArray());
        WriteCatalog(snapshot);
    }

    private async Task<bool> RestoreCatalog(PageKind page, int navigation)
    {
        var context = CatalogContext;
        if (!_catalogViews.ContainsKey(page) && _catalogPreloads.TryGetValue((page, context), out var pending))
        {
            var search = _search;
            await pending;
            if (navigation != _navigation || context != CatalogContext || _activeCatalogSearch is not null ||
                !ReferenceEquals(search, _search))
                return true;
        }
        if (!_catalogViews.TryGetValue(page, out var view))
        {
            var snapshot = ReadCatalog(page);
            if (snapshot is null) return false;
            view = new(snapshot, snapshot.Projects.Select(p => new ProjectRow(p)).ToArray());
        }
        var data = view.Data;
        if (data.Context != CatalogContext)
        {
            _filterStates.Remove(page);
            return false;
        }
        if (DateTimeOffset.UtcNow - data.FetchedAt > TimeSpan.FromHours(24))
        {
            _filterStates[page] = data.Filters;
            return false;
        }
        _catalogViews[page] = view;
        if (_catalogTagsRequest is null)
        {
            _categories = data.Categories.ToList();
            _loaderTags = data.Loaders.ToList();
            _catalogTagsRequest = Task.CompletedTask;
        }
        _suppress = true;
        try
        {
            PopulateFilters();
            ApplyFilters(data.Filters);
            foreach (var row in view.Rows)
            {
                Vm.Projects.Add(row);
                if (row.Icon is null) _ = LoadProjectIcon(row, _catalogLifetime.Token);
            }
            _activeCatalogSearch = data.Search;
            _activeCatalogContext = data.Context;
            _activeCatalogFetchedAt = data.FetchedAt;
            Vm.SetCatalogPage(data.Page, data.Total);
            UpdateCatalogStatus();
            Vm.SelectedProject = Vm.Projects.FirstOrDefault(p => p.Project.Id == data.SelectedProject)
                ?? Vm.Projects.FirstOrDefault();
        }
        finally { _suppress = false; }
        await Dispatcher.InvokeAsync(() =>
        {
            if (navigation == _navigation)
                VisualChildren<ScrollViewer>(ProjectList).FirstOrDefault()?.ScrollToVerticalOffset(data.ScrollOffset);
        }, DispatcherPriority.Loaded);
        if (navigation == _navigation) await UpdateProjectSelection(data.SelectedVersion);
        return true;
    }

    private void UpdateCatalogStatus()
        => Vm.Status = Vm.TotalProjects == 0 ? "没有匹配结果"
            : $"{Vm.TotalProjects:N0} 个项目 · 第 {Vm.CurrentPage:N0} / {Vm.TotalPages:N0} 页";

    private async Task UpdateProjectSelection(string? preferredVersion = null)
    {
        var project = Vm.SelectedProject;
        var request = ++_projectSelection;
        var navigation = _navigation;
        var page = Vm.Page;
        var criteria = _activeCatalogSearch ?? CurrentFilters;
        var key = new VersionKey(project?.Project.Id ?? "", criteria.Game,
            page is PageKind.Mod or PageKind.Modpack or PageKind.Shader ? criteria.Loader : "");
        bool IsCurrent() => request == _projectSelection && navigation == _navigation &&
            page == Vm.Page && project == Vm.SelectedProject && !_catalogLifetime.IsCancellationRequested;
        Vm.ProjectVersions.Clear();
        Vm.SelectedVersion = null;
        if (project is null) return;
        Task<List<ProjectVersion>>? pending = null;
        try
        {
            if (!_versionRequests.TryGetValue(key, out pending))
            {
                if (_versionRequests.Count >= 8) _versionRequests.Remove(_versionRequests.Keys.First());
                pending = _content.Versions(key.Project, key.Game, key.Loader, _catalogLifetime.Token);
                _versionRequests[key] = pending;
            }
            var versions = await pending;
            if (!IsCurrent()) return;
            foreach (var version in versions) Vm.ProjectVersions.Add(version);
            Vm.SelectedVersion = Vm.ProjectVersions.FirstOrDefault(v => v.Id == preferredVersion)
                ?? Vm.ProjectVersions.FirstOrDefault(v => v.VersionType == "release")
                ?? Vm.ProjectVersions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            if (_versionRequests.GetValueOrDefault(key) == pending) _versionRequests.Remove(key);
            if (IsCurrent()) await Guard(() => Task.FromException(ex));
        }
    }

    private void RefreshCatalogVersions()
    {
        foreach (var project in Vm.Projects)
            foreach (var key in _versionRequests.Keys.Where(k => k.Project == project.Project.Id).ToArray())
                _versionRequests.Remove(key);
    }

    private static CatalogSnapshot? ReadCatalog(PageKind page)
    {
        try
        {
            var file = Path.Combine(CatalogDirectory, page.ToString().ToLowerInvariant() + ".json");
            if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return null;
            var data = JsonSerializer.Deserialize<CatalogSnapshot>(File.ReadAllBytes(file), JsonTools.Options);
            if (data is null || data.Format != 1 || data.Kind != page || data.Context is null ||
                !ValidFilters(data.Filters) || !ValidFilters(data.Search) ||
                data.Categories is null || data.Loaders is null || data.Categories.Any(c => c is null) ||
                data.Loaders.Any(l => l is null || l.ProjectTypes is null) ||
                data.Projects is null || data.Projects.Length > ContentService.SearchPageSize ||
                data.Projects.Any(p => p is null || string.IsNullOrEmpty(p.Id) || p.Categories is null) ||
                data.Total < data.Projects.Length || !double.IsFinite(data.ScrollOffset) || data.ScrollOffset < 0 ||
                data.FetchedAt > DateTimeOffset.UtcNow.AddMinutes(5))
                return null;
            var pages = (int)(((long)data.Total + ContentService.SearchPageSize - 1) / ContentService.SearchPageSize);
            if (data.Total == 0 ? data.Page != 0 : data.Page < 1 || data.Page > pages) return null;
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return null; }
    }

    private static bool ValidFilters(CatalogState? state)
        => state is { Query: not null, Game: not null, Loader: not null, Category: not null,
            Feature: not null, Extra: not null, Sort: not null };

    private static void WriteCatalog(CatalogSnapshot data)
    {
        string? temporary = null;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonTools.Options);
            if (bytes.Length > 1024 * 1024) return;
            Directory.CreateDirectory(CatalogDirectory);
            var file = Path.Combine(CatalogDirectory, data.Kind.ToString().ToLowerInvariant() + ".json");
            temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private record CatalogSnapshot(int Format, PageKind Kind, string Context, CatalogState Filters, CatalogState Search,
        int Page, int Total, SearchProject[] Projects, string? SelectedProject, string? SelectedVersion,
        double ScrollOffset, DateTimeOffset FetchedAt, CategoryTag[] Categories, LoaderTag[] Loaders);
    private record CatalogView(CatalogSnapshot Data, ProjectRow[] Rows);
    private record VersionKey(string Project, string Game, string Loader);
}
