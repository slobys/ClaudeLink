using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Launcher.Core;

public sealed class SubscriptionTraffic
{
    public long? UploadBytes { get; set; }
    public long? DownloadBytes { get; set; }
    public long? TotalBytes { get; set; }
    public long? ExpiresUnixSeconds { get; set; }
    public bool WasEstimated { get; set; }
    [JsonIgnore] public decimal? Used => UploadBytes is >= 0 && DownloadBytes is >= 0 ? (decimal)UploadBytes.Value+DownloadBytes.Value : null;
    [JsonIgnore] public bool HasQuota => Used.HasValue && TotalBytes is > 0;
    [JsonIgnore] public double Percent => HasQuota ? (double)Math.Clamp(Used!.Value/TotalBytes!.Value*100,0,100) : 0;
    [JsonIgnore] public DateTimeOffset? Expiry => ExpiresUnixSeconds is > 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(ExpiresUnixSeconds.Value) : null;
    [JsonIgnore] public bool Expired => Expiry is { } expiry && expiry <= DateTimeOffset.UtcNow;
    [JsonIgnore] public string Tone => Expired || (HasQuota && Used >= TotalBytes) ? "Error" : (HasQuota && Percent>=80) || (Expiry is { } date && date-DateTimeOffset.UtcNow<=TimeSpan.FromDays(3)) ? "Warning" : HasQuota ? "Accent" : "Pending";
    [JsonIgnore] public string Status => Expired ? "已过期" : HasQuota && Used>=TotalBytes ? "流量用尽" : HasQuota && Percent>=80 ? "余量较低" : Expiry is { } date && date-DateTimeOffset.UtcNow<=TimeSpan.FromDays(3) ? "即将到期" : HasQuota ? "流量记录" : "信息未提供";
    [JsonIgnore] public string UsedText => HasQuota ? $"已用 {Bytes(Used!.Value)} / 共 {Bytes(TotalBytes!.Value)}" : Used.HasValue ? $"已用 {Bytes(Used.Value)} · 总量未提供" : "服务商未提供流量信息";
    [JsonIgnore] public string RemainingText => HasQuota ? "剩余 "+Bytes(Math.Max(0,TotalBytes!.Value-Used!.Value)) : "剩余流量未知";
    [JsonIgnore] public string PercentText => HasQuota ? (Used> TotalBytes ? "已超出总量" : Percent.ToString("0.#",CultureInfo.InvariantCulture)+"%") : "暂无比例";
    [JsonIgnore] public string ExpiryText => Expiry?.ToLocalTime().ToString("yyyy-MM-dd 到期",CultureInfo.InvariantCulture) ?? "到期时间未提供";
    [JsonIgnore] public string Hint => WasEstimated ? "流量条按旧版汇总估算，更新订阅后获取精确数据。" : "由服务商 subscription-userinfo 提供；流量和到期信息不代表节点连通性。";
    [JsonIgnore] public string? LegacySummary => HasQuota ? $"已用 {Used!.Value/1073741824m:0.0} / {TotalBytes!.Value/1073741824m:0.0} GB" : null;
    public static string Bytes(decimal value)
    {
        var units=new[]{"B","KB","MB","GB","TB","PB","EB"};var i=0;
        while(value>=1024 && i<units.Length-1){value/=1024;i++;}
        return value.ToString("0.#",CultureInfo.InvariantCulture)+" "+units[i];
    }
    public static SubscriptionTraffic? Parse(string? header)
    {
        if(string.IsNullOrWhiteSpace(header) || header.Length>4096)return null;
        var fields=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var part in header.Split(';'))
        {
            var pair=part.Trim().Split('=',2);
            if(pair.Length==2 && long.TryParse(pair[1].Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out var value) && value>=0)fields[pair[0].Trim()]=value;
        }
        if(!fields.Keys.Any(x=>new[]{"upload","download","total","expire"}.Contains(x,StringComparer.OrdinalIgnoreCase)))return null;
        return new() { UploadBytes=fields.GetValueOrDefault("upload",-1) is var u && u>=0 ? u:null,DownloadBytes=fields.GetValueOrDefault("download",-1) is var d && d>=0 ? d:null,TotalBytes=fields.GetValueOrDefault("total",-1) is var t && t>=0 ? t:null,ExpiresUnixSeconds=fields.GetValueOrDefault("expire",-1) is var e && e>0 && e<=253402300799 ? e:null };
    }
    public static SubscriptionTraffic? FromLegacy(string? text)
    {
        if(text==null || text.Length>4096)return null;
        if(text.Contains('='))return Parse(text);
        var match=Regex.Match(text,@"^已用\s+([0-9]+[.,]?[0-9]*)\s*/\s*([0-9]+[.,]?[0-9]*)\s*GB$");
        if(!match.Success || !decimal.TryParse(match.Groups[1].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var used) || !decimal.TryParse(match.Groups[2].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var total) || used>long.MaxValue/1073741824m || total>long.MaxValue/1073741824m)return null;
        return new() { UploadBytes=0,DownloadBytes=(long)(used*1073741824m),TotalBytes=(long)(total*1073741824m),WasEstimated=true };
    }
}
