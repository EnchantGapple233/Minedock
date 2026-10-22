using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace MineDock.Core;

public sealed class InstallEngine(Network network)
{
    private static readonly SemaphoreSlim Gate = new(1);
    public string CacheDirectory { get; set; } = Path.Combine(Settings.DataDirectory, "cache");
    public string Warning { get; private set; } = "";
    public async Task Execute(InstallPlan plan, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        var backups = new List<JournalItem>();
        var createdJava = new List<string>();
        var transaction = Path.Combine(Settings.DataDirectory, "transactions", plan.Id);
        var journalPath = Path.Combine(transaction, "journal.json");
        async Task Journal(JournalItem item) => await System.IO.File.AppendAllTextAsync(journalPath,
            JsonSerializer.Serialize(item) + "\n", CancellationToken.None);
        try
        {
            Warning = "";
            Directory.CreateDirectory(transaction);
            var total = plan.Files.Count;
            var done = 0;
            long bytes = 0;
            var lastReport = Environment.TickCount64;
            void Report(string message, bool force = false)
            {
                if (!force && Environment.TickCount64 - Interlocked.Read(ref lastReport) < 120) return;
                Interlocked.Exchange(ref lastReport, Environment.TickCount64);
                progress?.Report(new("下载并校验", message, Volatile.Read(ref done), total, Interlocked.Read(ref bytes), plan.Bytes));
            }
            var remote = plan.Files.Where(f => f.Action is FileAction.Download or FileAction.ExtractJava)
                .GroupBy(f => f.Url + "|" + f.Hash).ToList();
            await Parallel.ForEachAsync(remote, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (group, token) =>
            {
                var first = group.First();
                if (first.Action == FileAction.Download && System.IO.File.Exists(first.Destination) && await Network.Valid(first.Destination, first, token))
                    first.CachePath = first.Destination;
                else
                    await network.Download(first, CacheDirectory, amount => { Interlocked.Add(ref bytes, amount); Report(first.Label); }, token);
                foreach (var file in group) { file.CachePath = first.CachePath; Interlocked.Increment(ref done); }
                Report(first.Label, true);
            });
            progress?.Report(new("准备写入", "所有下载已校验，正在提交安装。", done, total, bytes, plan.Bytes));
            var initialCount = plan.Files.Count;
            var loaderStaged = plan.Loader is null;
            for (var index = 0; index < plan.Files.Count || !loaderStaged; index++)
            {
                ct.ThrowIfCancellationRequested();
                if (index == initialCount && !loaderStaged)
                {
                    var generated = await LoaderInstaller.Stage(plan, Path.Combine(transaction, "loader-staging"), progress, ct);
                    foreach (var generatedFile in generated) plan.Add(generatedFile);
                    loaderStaged = true;
                    total = plan.Files.Count;
                    if (index >= total) break;
                }
                var file = plan.Files[index];
                progress?.Report(new("安装文件", file.Label, index, total, bytes, plan.Bytes));
                if (file.Action == FileAction.ExtractJava)
                {
                    if (Directory.Exists(file.Destination)) throw new IOException("Java 目标已存在，停止覆盖。");
                    var staging = file.Destination + ".incoming-" + plan.Id;
                    Directory.CreateDirectory(staging);
                    try
                    {
                        using var zip = ZipFile.OpenRead(file.CachePath);
                        var entries = zip.Entries.Where(e => e.Name.Length > 0).ToList();
                        var prefix = entries.FirstOrDefault()?.FullName.Split('/')[0] + "/";
                        if (entries.Count == 0 || !entries.All(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)))
                            throw new InvalidDataException("Java 压缩包结构无法识别。");
                        long expanded = 0;
                        foreach (var entry in entries)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                                throw new InvalidDataException("Java 包含不支持的符号链接。");
                            expanded += entry.Length;
                            if (expanded > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Java 解包大小超出限制。");
                            var path = JsonTools.Child(staging, entry.FullName[prefix.Length..]);
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            entry.ExtractToFile(path);
                        }
                        if (!System.IO.File.Exists(Path.Combine(staging, "bin", "java.exe")))
                            throw new InvalidDataException("包内没有 bin\\java.exe。");
                        Directory.CreateDirectory(Path.GetDirectoryName(file.Destination)!);
                        createdJava.Add(file.Destination);
                        await System.IO.File.WriteAllTextAsync(Path.Combine(transaction, "java.json"), JsonSerializer.Serialize(createdJava));
                        Directory.Move(staging, file.Destination);
                    }
                    finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                    continue;
                }
                if (file.Action == FileAction.Download && file.CachePath.Equals(file.Destination, StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Action == FileAction.Download && System.IO.File.Exists(file.Destination) && await Network.Valid(file.Destination, file, ct)) continue;
                if (file.Action == FileAction.Local && !await Network.Valid(file.CachePath, file, ct))
                    throw new InvalidDataException("加载器输出在提交前发生变化。");
                Directory.CreateDirectory(Path.GetDirectoryName(file.Destination)!);
                string? backup = null;
                if (System.IO.File.Exists(file.Destination))
                {
                    backup = Path.Combine(transaction, "backup-" + index);
                    System.IO.File.Copy(file.Destination, backup);
                }
                var expectedHash = file.Action == FileAction.Text
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file.Text))) : file.Hash;
                var expectedAlgorithm = file.Action == FileAction.Text ? "SHA256" : file.HashAlgorithm;
                if (expectedHash.Length == 0)
                {
                    await using var cached = System.IO.File.OpenRead(file.CachePath);
                    expectedHash = Convert.ToHexString(await SHA256.HashDataAsync(cached, ct));
                    expectedAlgorithm = "SHA256";
                }
                var journal = new JournalItem(file.Destination, backup, expectedHash, expectedAlgorithm);
                backups.Add(journal);
                await Journal(journal);
                var temporary = file.Destination + ".minedock-" + plan.Id;
                try
                {
                    if (file.Action == FileAction.Text) await System.IO.File.WriteAllTextAsync(temporary, file.Text, ct);
                    else System.IO.File.Copy(file.CachePath, temporary, true);
                    System.IO.File.Move(temporary, file.Destination, true);
                }
                finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
            }
            AddHistory(new(plan.Id, plan.Title, plan.Destination, plan.Type, "已完成", DateTime.Now, $"{plan.Files.Count} 个文件 · {JsonTools.Size(plan.Bytes)}", plan.TargetId));
            await System.IO.File.WriteAllTextAsync(Path.Combine(transaction, "committed"), plan.Title, CancellationToken.None);
            try { Directory.Delete(transaction, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Warning = "安装已完成，事务备份将在下次启动时清理：" + ex.Message; }
            progress?.Report(new("已完成", plan.Title, total, total, plan.Bytes, plan.Bytes));
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var item in backups.AsEnumerable().Reverse())
            {
                try { await Restore(item); }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            foreach (var directory in createdJava)
            {
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            if (rollbackErrors.Count == 0)
            {
                try { if (Directory.Exists(transaction)) Directory.Delete(transaction, true); }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            AddHistory(new(plan.Id, plan.Title, plan.Destination, plan.Type, failure is OperationCanceledException ? "已取消" : "失败",
                DateTime.Now, failure.Message + (rollbackErrors.Count == 0 ? "" : "；回滚未完成：" + rollbackErrors[0].Message), plan.TargetId));
            if (rollbackErrors.Count > 0) throw new IOException("安装失败且部分文件未能恢复。请保留事务记录并关闭占用文件的程序：" + transaction, failure);
            throw;
        }
        finally { Gate.Release(); }
    }
    public static List<HistoryEntry> History()
    {
        try { return JsonSerializer.Deserialize<List<HistoryEntry>>(System.IO.File.ReadAllText(Path.Combine(Settings.DataDirectory, "history.json"))) ?? []; }
        catch { return []; }
    }
    public static void AddHistory(HistoryEntry entry)
    {
        Directory.CreateDirectory(Settings.DataDirectory);
        var history = History(); history.RemoveAll(x => x.Id == entry.Id); history.Insert(0, entry);
        var path = Path.Combine(Settings.DataDirectory, "history.json");
        System.IO.File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(history.Take(100), JsonTools.Options));
        System.IO.File.Move(path + ".tmp", path, true);
    }
    public static List<string> Recover()
    {
        var messages = new List<string>();
        var transactions = Path.Combine(Settings.DataDirectory, "transactions");
        if (!Directory.Exists(transactions)) return messages;
        foreach (var directory in Directory.EnumerateDirectories(transactions))
        {
            try
            {
                if (!System.IO.File.Exists(Path.Combine(directory, "committed")))
                {
                    var journal = Path.Combine(directory, "journal.json");
                    if (System.IO.File.Exists(journal))
                    {
                        var items = new List<JournalItem>();
                        foreach (var line in System.IO.File.ReadLines(journal))
                        {
                            try { if (JsonSerializer.Deserialize<JournalItem>(line) is { } item) items.Add(item); }
                            catch (JsonException) { }
                        }
                        foreach (var item in items.AsEnumerable().Reverse())
                        {
                            Restore(item).GetAwaiter().GetResult();
                        }
                    }
                    var java = Path.Combine(directory, "java.json");
                    if (System.IO.File.Exists(java))
                        foreach (var path in JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(java)) ?? [])
                            if (Directory.Exists(path)) Directory.Delete(path, true);
                    messages.Add("已恢复上次中断的安装：" + Path.GetFileName(directory));
                    var record = History().FirstOrDefault(h => h.Id == Path.GetFileName(directory));
                    if (record is not null) AddHistory(record with { Status = "失败", Message = "上次运行中断，本次已回滚，可重新确认后重试。" });
                }
                Directory.Delete(directory, true);
            }
            catch (Exception ex) { messages.Add("存在需要处理的安装事务：" + ex.Message); }
        }
        return messages;
    }
    private static async Task Restore(JournalItem item)
    {
        if (System.IO.File.Exists(item.Destination))
        {
            if (item.Hash.Length == 0) throw new IOException("恢复记录缺少校验值，请手动处理：" + item.Destination);
            if (!await Network.Valid(item.Destination, new() { Hash = item.Hash, HashAlgorithm = item.Algorithm }))
            {
                if (item.Backup is not null && System.IO.File.Exists(item.Backup))
                {
                    await using var original = System.IO.File.OpenRead(item.Backup);
                    var originalHash = Convert.ToHexString(await SHA256.HashDataAsync(original));
                    if (await Network.Valid(item.Destination, new() { Hash = originalHash, HashAlgorithm = "SHA256" })) return;
                }
                throw new IOException("文件在安装后被其他程序修改，已保留现状：" + item.Destination);
            }
        }
        if (item.Backup is not null) System.IO.File.Copy(item.Backup, item.Destination, true);
        else if (System.IO.File.Exists(item.Destination)) System.IO.File.Delete(item.Destination);
    }
    public record JournalItem(string Destination, string? Backup, string Hash = "", string Algorithm = "SHA256");
}
