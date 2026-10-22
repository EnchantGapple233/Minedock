using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MineDock.Core;

internal static class UpdateSmokeChecks
{
    public static async Task Run(string folder, string executable, string previous, Action<bool,string> check)
    {
        new Settings { AutomaticUpdates=false }.Save();
        var root=Path.Combine(folder,"update-target");
        Directory.CreateDirectory(root);
        var target=Path.Combine(root,"MineDock.exe");
        var updateDirectory=Path.Combine(Settings.DataDirectory,"updates",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateDirectory);
        var helper=Path.Combine(updateDirectory,"MineDock.Update.exe");
        File.Copy(executable,helper);
        async Task<string> Hash(string path)
        {
            await using var stream=File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream));
        }
        async Task<UpdateRequest> Request(string payload)
        {
            return new(target,payload,await Hash(payload),new FileInfo(payload).Length,await Hash(target),
                int.MaxValue,0,Path.Combine(updateDirectory,"ready"),target+".backup",
                Path.Combine(updateDirectory,"result.json"));
        }
        async Task Apply(UpdateRequest request)
        {
            var path=Path.Combine(updateDirectory,"request.json");
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(request,JsonTools.Options));
            var info=new ProcessStartInfo(helper) {UseShellExecute=false,CreateNoWindow=true};
            info.ArgumentList.Add("--apply-update");info.ArgumentList.Add(path);
            using var process=Process.Start(info)!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(65));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally
            {
                if(!process.HasExited) {process.Kill(true);await process.WaitForExitAsync();}
            }
            var error=Path.ChangeExtension(path,"error.log");
            if(File.Exists(error)) throw new Exception(await File.ReadAllTextAsync(error));
        }
        void StopTestProgram()
        {
            foreach(var process in Process.GetProcessesByName("MineDock"))
            {
                using(process)
                {
                    try
                    {
                        if(process.MainModule?.FileName is { } path &&
                            Path.GetFullPath(path).Equals(target,StringComparison.OrdinalIgnoreCase))
                        {process.Kill(true);process.WaitForExit();}
                    }
                    catch(InvalidOperationException) { }
                }
            }
        }
        try
        {
            File.Copy(previous,target,true);
            var request=await Request(Path.GetFullPath(executable));
            await Apply(request);
            var result=JsonDocument.Parse(await File.ReadAllTextAsync(request.ResultFile));
            check(result.RootElement.GetProperty("success").GetBoolean(),"Published single EXE helper replaces an older executable and starts the new window");
            check(await Hash(target)==await Hash(executable) && FileVersionInfo.GetVersionInfo(target).FileVersion=="0.1.4.0",
                "Actual updater replacement matches the v0.1.4 binary and file version");
            check(File.Exists(request.ReadyFile) && !File.Exists(request.Backup),"Successful startup acknowledges readiness and cleans the temporary backup");
            StopTestProgram();
            File.Delete(request.ReadyFile);
            File.Delete(request.ResultFile);
            var invalid=Path.Combine(updateDirectory,"invalid.exe");
            await File.WriteAllTextAsync(invalid,"MZ-invalid-executable");
            request=await Request(invalid);
            await Apply(request);
            result=JsonDocument.Parse(await File.ReadAllTextAsync(request.ResultFile));
            check(!result.RootElement.GetProperty("success").GetBoolean(),"A candidate that cannot start reports update failure");
            check(await Hash(target)==await Hash(executable),"A failed new executable restores the exact previous working program");
            await Task.Delay(1000);
        }
        finally {StopTestProgram();}
    }
}
