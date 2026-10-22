using System.Windows.Threading;
using MineDock.Core;

namespace MineDock.App;

public partial class MainWindow
{
    private Task? _startupPreload;
    private readonly Dictionary<(PageKind Page, string Context), Task<CatalogView?>> _catalogPreloads = [];

    private Task PreloadStartup() => _startupPreload ??= PreloadStartupCore();

    private async Task PreloadStartupCore()
    {
        var ct = _catalogLifetime.Token;
        async Task WaitIdle()
        {
            while (Vm.Running || Vm.Preparing || Vm.Loading || Vm.LauncherStarting)
                await Task.Delay(500, ct);
        }
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, ct);
            await Task.Delay(600, ct);
            await WaitIdle();
            await EnsureCatalogTags();
            var context = CatalogContext;
            var views = new List<CatalogView>();
            foreach (var page in new[] { PageKind.Mod, PageKind.Modpack, PageKind.Shader, PageKind.Resourcepack })
            {
                await WaitIdle();
                ct.ThrowIfCancellationRequested();
                if (context != CatalogContext) return;
                var key = (page, context);
                if (!_catalogPreloads.TryGetValue(key, out var pending))
                    _catalogPreloads[key] = pending = PreloadCatalog(page, context, ct);
                var view = await pending;
                if (view is not null) views.Add(view);
                await Task.Delay(100, ct);
            }
            foreach (var view in views)
                foreach (var row in view.Rows.Take(6))
                {
                    await WaitIdle();
                    ct.ThrowIfCancellationRequested();
                    if (context != CatalogContext) return;
                    await LoadProjectIcon(row, ct);
                }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    private async Task<CatalogView?> PreloadCatalog(PageKind page, string context, CancellationToken ct)
    {
        try
        {
            bool Fresh(CatalogSnapshot data) => data.Context == context &&
                DateTimeOffset.UtcNow - data.FetchedAt <= TimeSpan.FromHours(24);
            if (_catalogViews.TryGetValue(page, out var existing) && Fresh(existing.Data)) return existing;
            var saved = ReadCatalog(page);
            if (saved is not null && Fresh(saved))
                return new(saved, saved.Projects.Select(p => new ProjectRow(p)).ToArray());
            if (Vm.Page == page && _activeCatalogSearch is not null) return null;
            var target = Vm.Target;
            var filters = saved?.Context == context ? saved.Filters :
                new CatalogState("", target?.GameVersion ?? "",
                    page is PageKind.Mod or PageKind.Modpack && target?.Loader is { } loader && loader != "vanilla"
                        ? loader : "", "", "", "", "downloads");
            var kind = page.ToString().ToLowerInvariant();
            var result = await _content.Search(kind, filters.Query, filters.Game, filters.Loader,
                filters.Category, filters.Sort, 0, ct, filters.Feature, filters.Extra);
            ct.ThrowIfCancellationRequested();
            if (context != CatalogContext) return null;
            if (_catalogViews.TryGetValue(page, out existing) && Fresh(existing.Data)) return existing;
            var data = new CatalogSnapshot(1, page, context, filters, filters, result.Total == 0 ? 0 : 1,
                result.Total, result.Projects.ToArray(), result.Projects.FirstOrDefault()?.Id, null, 0,
                DateTimeOffset.UtcNow, _categories.ToArray(), _loaderTags.ToArray());
            var view = new CatalogView(data, data.Projects.Select(p => new ProjectRow(p)).ToArray());
            _catalogViews[page] = view;
            WriteCatalog(data);
            return view;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }
}
