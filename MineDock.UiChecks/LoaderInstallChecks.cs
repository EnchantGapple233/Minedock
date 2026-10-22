using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using MineDock.App;
using MineDock.Core;

internal static class LoaderInstallChecks
{
    public static async Task Run(string folder,Action<bool,string> check, string[] args)
    {
        using var network=new Network {Source=SourceMode.Official};
        var games=new GameService(network);
        var loaders=new LoaderService(network);
        var root=Path.Combine(folder,".minecraft");
        Directory.CreateDirectory(root);
        var settings=new Settings {MinecraftRoot=root,Source=SourceMode.Official,JavaDirectory=Path.Combine(folder,"runtime")};
        await LoaderRuntime.Ensure();
        var engine=new InstallEngine(network) {CacheDirectory=Path.Combine(folder,"download-cache")};
        var evidence=new List<object>();
        foreach(var (kind,game) in new[] { ("fabric","1.21.1"),("quilt","1.21.1"),("forge","1.21.1"),
            ("neoforge","1.21.1"),("optifine","1.21.1"),("legacy-fabric","1.8.9"),("liteloader","1.12.2"),
            ("forge","1.12.2"),("neoforge","1.20.1") })
        {
            var only=args.FirstOrDefault(a=>a.StartsWith("--loader="))?["--loader=".Length..];
            if(only is not null && only!=kind+":"+game) continue;
            Console.WriteLine("LIVE: "+kind+" "+game);
            var releases=await loaders.Releases(kind,game);
            check(releases.Count>0,kind+" "+game+" has live compatible releases");
            var selected=releases.FirstOrDefault(r=>r.Stable)??releases.First();
            var id="MineDock-test-"+game+"-"+kind;
            if(Directory.Exists(Path.Combine(root,"versions",id))) id+="-"+Guid.NewGuid().ToString("N")[..6];
            var plan=await games.Plan(root,game,selected.Version,id,loaderKind:kind);
            plan.Source=settings.Source;
            // This suite tests real clients, launch profiles and libraries, without repeating shared assets.
            plan.Files.RemoveAll(f=>Path.GetRelativePath(root,f.Destination).Replace('\\','/').StartsWith("assets/") ||
                Path.GetRelativePath(root,f.Destination).Replace('\\','/').StartsWith("resources/"));
            await LoaderInstaller.Prepare(plan,settings,network);
            var last=Environment.TickCount64;
            await engine.Execute(plan,new Progress<TransferProgress>(p=>
            {
                if(Environment.TickCount64-last<3000)return;
                last=Environment.TickCount64;
                Console.WriteLine(p.Phase+" "+p.Done+"/"+p.Total+" "+JsonTools.Size(p.Bytes));
            }),default);
            var profile=Path.Combine(root,"versions",id,id+".json");
            check(File.Exists(profile),kind+" "+game+" installs an actual launch profile");
            check(plan.Files.All(f=>File.Exists(f.Destination)),kind+" "+game+" commits every planned file");
            var instances=PclIntegration.Discover(root,"");
            var instance=instances.Single(i=>i.Id==id);
            check(instance.GameVersion==game && instance.Loader==kind,kind+" "+game+" is rediscovered with correct identity");
            var data=JsonNode.Parse(await File.ReadAllTextAsync(profile))!;
            check(data.Str("mainClass").Length>0 || data.Str("inheritsFrom").Length>0 || data["patches"] is JsonArray,
                kind+" "+game+" profile contains launch configuration");
            var jarFiles=plan.Files.Where(f=>f.Destination.EndsWith(".jar",StringComparison.OrdinalIgnoreCase)).ToList();
            foreach(var file in jarFiles)
                using(ZipFile.OpenRead(file.Destination)) { }
            check(jarFiles.Count>0,kind+" "+game+" downloaded JARs are readable archives");
            evidence.Add(new {Kind=kind,Game=game,Loader=selected.Version,Versions=releases.Count,Files=plan.Files.Count,Profile=profile});
            await File.WriteAllTextAsync(Path.Combine(folder,"loader-installations.json"),JsonSerializer.Serialize(evidence,JsonTools.Options));
        }
    }
}
