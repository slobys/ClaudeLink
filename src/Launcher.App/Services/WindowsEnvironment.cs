namespace Launcher.App.Services;

public sealed class EnvironmentJournal
{
    public List<string> IPv6Adapters { get; set; } = [];
    public string? OriginalTimeZone { get; set; }
    public string? AppliedTimeZone { get; set; }
    public bool Restored { get; set; }
}

public static class WindowsEnvironment
{
    public static async Task<bool> IPv6DisabledAsync()
    {
        var (code, output) = await ProcessTools.PowerShellAsync(
            "$ids=@(Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object {Get-NetAdapterBinding -Name $_.Name -ComponentID ms_tcpip6} | Where-Object Enabled);$ids.Count");
        return code == 0 && int.TryParse(output.Trim(), out var count) && count == 0;
    }
    public static async Task ApplyIPv6Async(string journalPath)
    {
        var journal = AppPaths.ReadJson<EnvironmentJournal>(journalPath) ?? new();
        var ids = await ReadEnabledIPv6AdaptersAsync();
        foreach (var id in ids)
        {
            if (!journal.IPv6Adapters.Contains(id)) journal.IPv6Adapters.Add(id);
        }
        journal.Restored = false;
        AppPaths.WriteJson(journalPath, journal); // Persist original bindings BEFORE mutating.
        foreach (var id in ids)
        {
            var script = AdapterScript(id) + "Disable-NetAdapterBinding -Name $adapter.Name -ComponentID ms_tcpip6 -Confirm:$false | Out-Null;" +
                "if((Get-NetAdapterBinding -Name $adapter.Name -ComponentID ms_tcpip6).Enabled){throw 'Binding still enabled'}";
            var result = await ProcessTools.PowerShellAsync(script);
            if (result.Code != 0) throw new IOException("临时关闭 IPv6 未确认成功，将尝试恢复。");
        }
    }
    public static async Task<List<string>> ReadEnabledIPv6AdaptersAsync()
    {
        var (code, output) = await ProcessTools.PowerShellAsync(
            "$ids=@(Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object {$a=$_;$b=Get-NetAdapterBinding -Name $a.Name -ComponentID ms_tcpip6;if($b.Enabled){$a.InterfaceGuid.ToString()}});ConvertTo-Json -InputObject $ids -Compress");
        if (code != 0) throw new IOException("无法读取活动物理网卡的 IPv6 状态。");
        List<string> ids;
        try { ids = JsonSerializer.Deserialize<List<string>>(output.Trim().TrimStart('\uFEFF')) ?? []; }
        catch (JsonException) { throw new IOException("读取网卡状态时返回了无效 JSON；系统环境尚未修改。"); }
        if (ids.Any(id => !Guid.TryParse(id, out _))) throw new InvalidDataException("无效网卡编号。");
        return ids;
    }
    public static async Task ApplyTimeZoneAsync(string journalPath, string target)
    {
        if (!TimeZoneInfo.GetSystemTimeZones().Any(x => x.Id == target)) throw new ArgumentException("Windows 不支持该时区。");
        var current = TimeZoneInfo.Local.Id;
        var journal = AppPaths.ReadJson<EnvironmentJournal>(journalPath) ?? new();
        if (current == target) return;
        journal.OriginalTimeZone ??= current;
        journal.AppliedTimeZone = target;
        journal.Restored = false;
        AppPaths.WriteJson(journalPath, journal);
        var result = await ProcessTools.RunAsync(ProcessTools.Hidden("tzutil.exe", "/s", target));
        TimeZoneInfo.ClearCachedData();
        if (result.Code != 0 || TimeZoneInfo.Local.Id != target) throw new IOException("系统时区修改未确认成功。");
    }
    public static async Task RestoreAsync(string journalPath)
    {
        var journal = AppPaths.ReadJson<EnvironmentJournal>(journalPath);
        if (journal is null || journal.Restored) return;
        var failed = false;
        foreach (var id in journal.IPv6Adapters)
        {
            if (!Guid.TryParse(id, out _)) { failed = true; continue; }
            // GUID lookup survives adapter renames. Missing adapters remain pending for recovery.
            var result = await ProcessTools.PowerShellAsync(AdapterScript(id) +
                "Enable-NetAdapterBinding -Name $adapter.Name -ComponentID ms_tcpip6 -Confirm:$false | Out-Null;" +
                "if(-not (Get-NetAdapterBinding -Name $adapter.Name -ComponentID ms_tcpip6).Enabled){throw 'Not restored'}");
            if (result.Code != 0) failed = true;
        }
        TimeZoneInfo.ClearCachedData();
        if (journal.OriginalTimeZone is not null && TimeZoneInfo.Local.Id == journal.AppliedTimeZone)
        {
            var result = await ProcessTools.RunAsync(ProcessTools.Hidden("tzutil.exe", "/s", journal.OriginalTimeZone));
            TimeZoneInfo.ClearCachedData();
            if (result.Code != 0 || TimeZoneInfo.Local.Id != journal.OriginalTimeZone) failed = true;
        }
        if (failed) throw new IOException("部分系统设置尚未确认恢复，恢复记录已保留。");
        journal.Restored = true;
        AppPaths.WriteJson(journalPath, journal);
    }
    private static string AdapterScript(string id) =>
        "$adapter=Get-NetAdapter -IncludeHidden | Where-Object {$_.InterfaceGuid.ToString() -eq " + ProcessTools.PsLiteral(id) +
        "} | Select-Object -First 1;if(-not $adapter){throw 'Adapter missing'};";
}
