using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MineDock.App;
using MineDock.Core;

internal static class CatalogFeatureChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static async Task Run(Func<MainWindow> create, string folder, Action<bool, string> check)
    {
        new Settings().Save();
        var window = create();
        var handler = Attach(window);
        window.Vm.Query = "kept";
        window.Vm.Sort = "downloads";
        await Search(window);
        await Search(window, 2);
        window.Vm.SelectedProject = window.Vm.Projects[7];
        await Until(() => window.Vm.ProjectVersions.Count == 2);
        window.Vm.SelectedVersion = window.Vm.ProjectVersions[1];
        var selected = window.Vm.SelectedProject;
        var versionId = window.Vm.SelectedVersion.Id;
        window.UpdateLayout();
        var scroll = Children<ScrollViewer>(Named<ListBox>(window, "ProjectList")).First();
        scroll.ScrollToVerticalOffset(7);
        window.UpdateLayout();
        var offset = scroll.VerticalOffset;
        await Navigate(window, PageKind.Modpack);
        await Navigate(window, PageKind.Shader);
        await Navigate(window, PageKind.Resourcepack);
        check(handler.Searches == 5, "Each newly visited resource section loads its list once");
        var searches = handler.Searches;
        var details = handler.Versions;
        var tags = handler.Tags;
        await Navigate(window, PageKind.Mod);
        window.UpdateLayout();
        check(handler.Searches == searches && handler.Versions == details && handler.Tags == tags,
            "Returning to a loaded section sends no list, tag, or repeated version request");
        check(window.Vm.CurrentPage == 2 && window.Vm.Query == "kept" && window.Vm.Sort == "downloads" &&
            ReferenceEquals(window.Vm.SelectedProject, selected) && window.Vm.SelectedVersion?.Id == versionId,
            "Returning restores filters, page, selected row, and selected file version");
        check(Math.Abs(scroll.VerticalOffset - offset) < .01,
            "Returning restores the list scroll offset");
        check(new[] { "mod", "modpack", "shader", "resourcepack" }.All(kind =>
            File.Exists(Path.Combine(Settings.DataDirectory, "catalog-cache", kind + ".json"))),
            "Successful resource lists are written to separate local cache files");
        foreach (var kind in new[] { PageKind.Modpack, PageKind.Shader, PageKind.Resourcepack, PageKind.Mod })
            await Navigate(window, kind);
        check(handler.Searches == searches, "All four resource sections reuse their cached lists on repeated visits");
        Click(window, "RefreshPageButton");
        await Until(() => !window.Vm.Loading && handler.Searches == searches + 1);
        check(handler.LastOffset == 30 && window.Vm.CurrentPage == 2,
            "Explicit refresh fetches new content while retaining the current page");
        window.Vm.Query = "new-query";
        await Search(window);
        check(window.Vm.CurrentPage == 1 && window.Vm.Projects[0].Title.Contains("new-query"),
            "A new search bypasses the saved list and resets to the first page");
        var saved = window.Vm.Projects[0];
        handler.FailSearch = true;
        Click(window, "RefreshPageButton");
        await Until(() => !window.Vm.Loading && window.Vm.Error.Length > 0);
        check(ReferenceEquals(window.Vm.Projects[0], saved),
            "Failed refresh preserves the displayed cached list");
        await Navigate(window, PageKind.Modpack);
        searches = handler.Searches;
        await Navigate(window, PageKind.Mod);
        check(handler.Searches == searches && ReferenceEquals(window.Vm.Projects[0], saved),
            "Failed refresh does not poison the section cache");
        handler.FailSearch = false;
        handler.DelaySearch = 180;
        window.Vm.Query = "cancelled-query";
        var pending = Search(window);
        await Navigate(window, PageKind.Shader);
        await pending;
        check(window.Vm.Projects.All(p => p.Project.ProjectType == "shader") && !window.Vm.Loading,
            "A request cancelled during section switching cannot overwrite the restored section");
        handler.DelaySearch = 0;
        await Navigate(window, PageKind.Mod);
        check(window.Vm.Projects[0].Title.Contains("new-query") &&
            window.Vm.Query == "cancelled-query" && window.Vm.CurrentPage == 1,
            "An unsubmitted or cancelled filter edit is preserved separately from the last successful results");
        window.Vm.Query = "kept";
        await Search(window);
        await Search(window, 2);
        window.Vm.Query = "empty";
        await Search(window);
        await Navigate(window, PageKind.Modpack);
        searches = handler.Searches;
        await Navigate(window, PageKind.Mod, empty: true);
        check(window.Vm.CurrentPage == 0 && window.Vm.Projects.Count == 0 && handler.Searches == searches,
            "A successful empty list is cached and restored without another request");
        window.Vm.Query = "kept";
        await Search(window);
        await Search(window, 2);
        await Navigate(window, PageKind.Tasks);

        new Settings().Save();
        var reopened = create();
        var offline = Attach(reopened);
        offline.FailSearch = true;
        await Navigate(reopened, PageKind.Tasks);
        await Navigate(reopened, PageKind.Mod);
        check(offline.Searches == 0 && offline.Tags == 0 && reopened.Vm.CurrentPage == 2 &&
            reopened.Vm.Query == "kept" && reopened.Vm.Projects.Count == 30,
            "A new window restores a fresh on-disk list without a list or tag request");
        check(offline.Versions == 1 && reopened.Vm.ProjectVersions.Count == 2,
            "Reopened windows fetch selected-project versions once without reloading the list");
        await Navigate(reopened, PageKind.Tasks);
        await Navigate(reopened, PageKind.Mod);
        check(offline.Searches == 0 && offline.Versions == 1,
            "Reopened-window navigation also reuses the fetched version list");

        var cacheFile = Path.Combine(Settings.DataDirectory, "catalog-cache", "modpack.json");
        File.WriteAllText(cacheFile, "{broken");
        var corruptWindow = create();
        var corrupt = Attach(corruptWindow);
        await Navigate(corruptWindow, PageKind.Modpack);
        check(corrupt.Searches == 1 && corruptWindow.Vm.Projects.Count == 30 && corruptWindow.Vm.Error == "",
            "Corrupt disk caches fall back to a normal request without an application error");
        var expired = JsonNode.Parse(File.ReadAllText(cacheFile))!;
        expired["FetchedAt"] = DateTimeOffset.UtcNow.AddDays(-2).ToString("O");
        File.WriteAllText(cacheFile, expired.ToJsonString());
        var expiredWindow = create();
        var expiredHandler = Attach(expiredWindow);
        await Navigate(expiredWindow, PageKind.Modpack);
        check(expiredHandler.Searches == 1 && expiredWindow.Vm.CurrentPage == 1,
            "Disk caches older than 24 hours are refreshed");
        var target = new Instance("new-target", "1.21.1", "fabric", Path.Combine(folder, "target"), true, null);
        window.Vm.Instances.Add(target);
        window.Vm.Target = target;
        await Navigate(window, PageKind.Modpack);
        check(handler.LastFacets.Contains("versions:1.21.1") && handler.LastFacets.Contains("categories:fabric"),
            "A changed game target does not restore lists or filters from the old target");
        await Navigate(window, PageKind.Tasks);
        var blockedPath = Path.Combine(Settings.DataDirectory, "catalog-cache", "shader.json");
        File.Delete(blockedPath);
        Directory.CreateDirectory(blockedPath);
        var blockedWindow = create();
        var blocked = Attach(blockedWindow);
        await Navigate(blockedWindow, PageKind.Shader);
        check(blocked.Searches == 1 && blockedWindow.Vm.Projects.Count > 0 && blockedWindow.Vm.Error == "" &&
            !Directory.GetFiles(Path.GetDirectoryName(blockedPath)!, "*.tmp").Any(),
            "A cache-write failure leaves browsing usable and cleans its temporary file");

        var rapid = create();
        var rapidHandler = Attach(rapid);
        var rapidTarget = new Instance("rapid-target", "1.20.1", "fabric", Path.Combine(folder, "rapid-target"), true, null);
        rapid.Vm.Instances.Add(rapidTarget);
        rapid.Vm.Target = rapidTarget;
        rapidHandler.DelayTags = 100;
        Named<RadioButton>(rapid, "NavModpack").IsChecked = true;
        await Navigate(rapid, PageKind.Resourcepack);
        check(rapidHandler.Tags == 2 && rapidHandler.Searches == 1 &&
            rapid.Vm.Projects.All(p => p.Project.ProjectType == "resourcepack"),
            "Rapid first visits share metadata requests and only fetch the final section");
        rapidHandler.DelayTags = 0;
        rapidHandler.DelayVersions = 160;
        rapid.Vm.Query = "delayed-detail";
        var refreshSearches = rapidHandler.Searches;
        Click(rapid, "RefreshPageButton");
        await Until(() => rapidHandler.Searches == refreshSearches + 1 && !rapid.Vm.Loading);
        await Navigate(rapid, PageKind.Modpack);
        check(rapid.Vm.ProjectVersions.All(v => v.ProjectId.StartsWith("modpack-")),
            "A delayed project-version response cannot replace another section's details");
        var rapidVersionRequests = rapidHandler.Versions;
        await Navigate(rapid, PageKind.Resourcepack);
        check(rapid.Vm.ProjectVersions.All(v => v.ProjectId.StartsWith("resourcepack-")) &&
            rapidHandler.Versions == rapidVersionRequests,
            "Completed background version requests are reused when returning to their section");
    }

    private static FixtureHandler Attach(MainWindow window)
    {
        var network = (Network)typeof(MainWindow).GetField("_network", Private)!.GetValue(window)!;
        var field = typeof(Network).GetField("_client", Private)!;
        ((HttpClient)field.GetValue(network)!).Dispose();
        var handler = new FixtureHandler();
        field.SetValue(network, new HttpClient(handler));
        return handler;
    }
    private static Task Search(MainWindow window, int page = 1)
        => (Task)typeof(MainWindow).GetMethod("Search", Private)!.Invoke(window, [page])!;
    private static async Task Navigate(MainWindow window, PageKind page, bool empty = false)
    {
        Named<RadioButton>(window, "Nav" + page).IsChecked = true;
        await Until(() => window.Vm.Page == page && !window.Vm.Loading &&
            (!window.Vm.IsCatalog || empty || window.Vm.Projects.Count > 0));
        if (window.Vm.IsCatalog && !empty) await Until(() => window.Vm.ProjectVersions.Count == 2);
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!condition()) throw new TimeoutException("Catalog cache UI fixture did not finish.");
    }
    private static T Named<T>(MainWindow window, string name) => (T)window.FindName(name);
    private static void Click(MainWindow window, string name)
        => Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Children<T>(child)) yield return nested;
        }
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        internal int Searches, Tags, Versions, LastOffset, DelaySearch, DelayTags, DelayVersions;
        internal string LastFacets = "";
        internal bool FailSearch;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object response;
            if (path == "/v2/search")
            {
                Searches++;
                var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
                LastOffset = int.Parse(query["offset"]!);
                LastFacets = query["facets"]!;
                var kind = JsonSerializer.Deserialize<string[][]>(LastFacets)![0][0]["project_type:".Length..];
                var text = query["query"] ?? "";
                var total = text == "empty" ? 0 : 75;
                await Task.Delay(DelaySearch, ct);
                if (FailSearch) throw new HttpRequestException("Offline catalog fixture.");
                response = new { total_hits = total, hits = Enumerable.Range(LastOffset, Math.Min(30, total - LastOffset))
                    .Select(i => new { project_id = kind + "-" + i, slug = kind + "-" + i,
                        title = kind + " " + text + " item " + i, description = "Cache fixture",
                        icon_url = "", author = "Fixture", downloads = i, categories = Array.Empty<string>(),
                        project_type = kind }) };
            }
            else if (path.EndsWith("/version"))
            {
                Versions++;
                await Task.Delay(DelayVersions, ct);
                var id = path.Split('/')[3];
                response = Enumerable.Range(1, 2).Select(n => new { id = id + "-v" + n, project_id = id,
                    name = "Version " + n, version_number = n.ToString(), version_type = "release",
                    date_published = $"2026-10-{21 - n}T00:00:00Z", game_versions = new[] { "1.21.1" },
                    loaders = new[] { "fabric" }, dependencies = Array.Empty<object>(),
                    files = new[] { new { url = "https://cdn.modrinth.com/data/" + id + "/file.jar",
                        filename = id + ".jar", size = 100, hashes = new { sha512 = new string('a', 128) }, primary = true } } });
            }
            else if (path == "/v2/tag/category")
            {
                Tags++;
                await Task.Delay(DelayTags, ct);
                response = new[] { "mod", "modpack", "shader", "resourcepack" }.Select(kind =>
                    new { name = "technology", header = "categories", project_type = kind });
            }
            else if (path == "/v2/tag/loader")
            {
                Tags++;
                await Task.Delay(DelayTags, ct);
                response = new[] { new { name = "fabric", supported_project_types = new[] { "mod", "modpack", "shader" } } };
            }
            else response = Array.Empty<object>();
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response),
                Encoding.UTF8, "application/json") };
        }
    }
}
