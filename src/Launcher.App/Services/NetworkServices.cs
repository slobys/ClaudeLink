using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Launcher.App.Services;

public sealed class CheckResult
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Detail { get; set; } = "";
    public string State { get; set; } = "pending";
    public string StateLabel => State switch { "pass" => "通过", "fail" => "失败", "warn" => "注意", "info" => "参考", _ => Value=="未验证" ? "未验证" : "待检测" };
    public string Symbol => State switch { "pass" => "✓", "fail" => "×", "warn" => "!", _ => "·" };
}
public sealed class NetworkReport
{
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public string GeneralIP { get; set; } = "";
    public string CloudflareIP { get; set; } = "";
    public string ClaudeIP { get; set; } = "";
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string IanaTimeZone { get; set; } = "";
    public string WindowsTimeZone { get; set; } = "";
    public bool ApiReachable { get; set; }
    public bool WebReachable { get; set; }
    public bool SameIPv4 { get; set; }
    public bool IPv6Disabled { get; set; }
    public bool TimeZoneMatches { get; set; }
    public bool Tun { get; set; }
    public int ApiCode { get; set; }
    public int WebCode { get; set; }
    public string ApiFailure { get; set; } = "";
    public int SignInCode { get; set; }
    public int OAuthCode { get; set; }
    public string SignInFailure { get; set; } = "";
    public string OAuthFailure { get; set; } = "";
    public string ExitFailure { get; set; } = "";
    public bool BasicReady => ApiReachable && IPAddress.TryParse(GeneralIP, out _);
    public bool StrictReady => BasicReady && WebReachable && SameIPv4 && Country == "US" && Tun && IPv6Disabled && TimeZoneMatches;
    public List<CheckResult> Results { get; } = [];
    public List<string> BlockingReasons(bool strict)
    {
        var reasons = new List<string>();
        if (!IPAddress.TryParse(GeneralIP, out _)) reasons.Add("代理出口 IP 未验证" + (ExitFailure.Length > 0 ? $"：{ExitFailure}" : ""));
        if (!ApiReachable) reasons.Add(ApiCode > 0 ? $"Anthropic API 返回 HTTP {ApiCode}" : "Anthropic API 请求失败" + (ApiFailure.Length > 0 ? $"：{ApiFailure}" : ""));
        if (!strict) return reasons;
        if (!WebReachable) reasons.Add(WebCode > 0 ? $"Claude 登录页自动探测返回 HTTP {WebCode}（需浏览器确认）" : "Claude 登录页自动探测未验证");
        if (!SameIPv4) reasons.Add("三个出口 IPv4 不一致或未验证");
        if (Country != "US") reasons.Add(Country.Length > 0 ? $"参考出口地区为 {Country}，自定义预设目标为 US" : "出口地区未验证");
        if (!Tun) reasons.Add("TUN 未启用");
        if (!IPv6Disabled) reasons.Add("活动物理网卡 IPv6 未确认关闭");
        if (!TimeZoneMatches) reasons.Add("系统时区未匹配或未验证");
        return reasons;
    }
}

public static class NetworkServices
{
    public const string ClaudeLoginUrl = "https://claude.ai/login";
    public const string SignInUrl = "https://claude.com/";
    public const string OAuthUrl = "https://platform.claude.com/";
    public const string RequirementsUrl = "https://code.claude.com/docs/en/network-config";
    public static HttpClient ProxyClient(int port, int seconds = 15) => new(new SocketsHttpHandler
    {
        Proxy = new WebProxy($"http://127.0.0.1:{port}"), UseProxy = true,
        ConnectTimeout = TimeSpan.FromSeconds(8), AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true
    }) { Timeout = TimeSpan.FromSeconds(seconds) };

    public static async Task<(ImportResult Result, string? Usage, SubscriptionTraffic? Traffic)> DownloadSubscriptionAsync(string url, int? proxyPort = null)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new FormatException("订阅地址必须以 https:// 或 http:// 开头。");
        if (uri.UserInfo.Length > 0) throw new FormatException("请使用订阅令牌地址，暂不支持 URL 中的 HTTP 用户名/密码。");
        using var client = proxyPort is { } port ? ProxyClient(port, 30) :
            new HttpClient(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(10), AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeNetworkLauncher/1.0");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) throw new IOException($"订阅下载失败（HTTP {(int)response.StatusCode}），原节点已保留。");
        if (response.Content.Headers.ContentLength > NodeImporter.MaxBytes) throw new IOException("订阅超过 8 MB，已取消导入。");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
        using var buffer = new MemoryStream();
        var bytes = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(bytes, cancellation.Token)) > 0)
        {
            if (buffer.Length + read > NodeImporter.MaxBytes) throw new IOException("订阅超过 8 MB，已取消导入。");
            buffer.Write(bytes, 0, read);
        }
        var text = Encoding.UTF8.GetString(buffer.ToArray());
        ImportResult parsed;
        try { parsed = await Task.Run(() => NodeImporter.Parse(text)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new IOException("订阅格式无法解析，原节点已保留。请确认提供的是节点订阅。"); }
        if (parsed.Nodes.Count == 0) throw new IOException("订阅未包含可支持的节点，原节点已保留。");
        var traffic = response.Headers.TryGetValues("subscription-userinfo", out var headers) ? SubscriptionTraffic.Parse(headers.FirstOrDefault()) : null;
        return (parsed, traffic?.LegacySummary, traffic);
    }

    private sealed record ProbeResult(int Code, string Text, string Failure);
    private static async Task<ProbeResult> ProbeAsync(HttpClient client, string url, CancellationToken cancellation, int seconds=15)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("ClaudeNetworkLauncher/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
            var bytes = new byte[65536];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(count), budget.Token);
                if (read == 0) break;
                count += read;
            }
            return new((int)response.StatusCode, Encoding.UTF8.GetString(bytes, 0, count), response.IsSuccessStatusCode ? "" : $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return new(0, "", "请求超时"); }
        catch (HttpRequestException ex)
        {
            var reason = ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "DNS 解析失败",
                HttpRequestError.SecureConnectionError => "TLS 握手或证书校验失败",
                HttpRequestError.ProxyTunnelError => "代理隧道建立失败",
                HttpRequestError.ConnectionError => "连接失败或被拒绝",
                _ => "HTTP 传输失败"
            };
            return new(0, "", reason);
        }
        catch (IOException) { return new(0, "", "响应读取失败"); }
    }

    public static async Task<NetworkReport> CheckAsync(int port, bool tun, bool inspectIPv6, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        using var client = ProxyClient(port);
        return await CheckWithClientAsync(client, tun, inspectIPv6, progress, cancellation);
    }
    internal static async Task<NetworkReport> CheckWithClientAsync(HttpClient client, bool tun, bool inspectIPv6, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var report = new NetworkReport { Tun = tun };
        progress?.Report("正在检测代理出口和 API 连通性…");
        var general = ProbeAsync(client, "https://api.ipify.org?format=json", cancellation);
        var cf = ProbeAsync(client, "https://www.cloudflare.com/cdn-cgi/trace", cancellation);
        var claude = ProbeAsync(client, "https://claude.ai/cdn-cgi/trace", cancellation);
        var api = ProbeAsync(client, "https://api.anthropic.com/v1/models", cancellation);
        var web = ProbeAsync(client, ClaudeLoginUrl, cancellation);
        var signIn = ProbeAsync(client, SignInUrl, cancellation,5);
        var oauth = ProbeAsync(client, OAuthUrl, cancellation,5);
        await Task.WhenAll(general, cf, claude, api, web,signIn,oauth);
        cancellation.ThrowIfCancellationRequested();
        report.GeneralIP = JsonValue((await general).Text, "ip");
        report.ExitFailure = (await general).Failure;
        report.CloudflareIP = TraceIP((await cf).Text);
        report.ClaudeIP = TraceIP((await claude).Text);
        if (report.ClaudeIP.Length == 0)
            report.ClaudeIP = TraceIP((await ProbeAsync(client, "https://api.anthropic.com/cdn-cgi/trace", cancellation)).Text);
        report.ApiCode = (await api).Code;
        report.ApiFailure = (await api).Failure;
        report.ApiReachable = report.ApiCode is 200 or 400 or 401 or 404 or 405;
        report.WebCode = (await web).Code;
        report.WebReachable = report.WebCode is >= 200 and < 400;
        report.SignInCode=(await signIn).Code;report.SignInFailure=(await signIn).Failure;
        report.OAuthCode=(await oauth).Code;report.OAuthFailure=(await oauth).Failure;
        var ips = new[] { report.GeneralIP, report.CloudflareIP, report.ClaudeIP };
        report.SameIPv4 = ips.All(IsIPv4) && ips.Distinct().Count() == 1;
        progress?.Report("正在查询出口地区与时区…");
        var reference = report.ClaudeIP.Length > 0 ? report.ClaudeIP : report.GeneralIP;
        if (IPAddress.TryParse(reference, out _))
        {
            var geo = await ProbeAsync(client, "https://ipwho.is/" + reference, cancellation);
            report.Country = JsonValue(geo.Text, "country_code");
            report.City = JsonValue(geo.Text, "city");
            try { report.IanaTimeZone = JsonNode.Parse(geo.Text)?["timezone"]?["id"]?.ToString() ?? ""; }
            catch (JsonException) { }
            catch (InvalidOperationException) { }
            if (report.Country.Length == 0 || report.IanaTimeZone.Length == 0)
            {
                var other = await ProbeAsync(client, "https://ipapi.co/" + reference + "/json/", cancellation);
                if (report.Country.Length == 0) report.Country = JsonValue(other.Text, "country_code");
                if (report.City.Length == 0) report.City = JsonValue(other.Text, "city");
                if (report.IanaTimeZone.Length == 0) report.IanaTimeZone = JsonValue(other.Text, "timezone");
            }
        }
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(report.IanaTimeZone, out var windowsZone)) report.WindowsTimeZone = windowsZone;
        TimeZoneInfo.ClearCachedData();
        report.TimeZoneMatches = report.WindowsTimeZone.Length > 0 && TimeZoneInfo.Local.Id == report.WindowsTimeZone;
        if (inspectIPv6) report.IPv6Disabled = await WindowsEnvironment.IPv6DisabledAsync();
        report.Results.AddRange([
            new() { Label = "普通出口", Value = Empty(report.GeneralIP), State = IsIPv4(report.GeneralIP) ? "pass" : "warn", Detail = report.GeneralIP.Length > 0 ? "通过本次 HTTP 代理独立检测" : report.ExitFailure },
            new() { Label = "Cloudflare 出口", Value = Empty(report.CloudflareIP), State = IsIPv4(report.CloudflareIP) ? "pass" : "warn", Detail = "Cloudflare trace" },
            new() { Label = "Claude 出口", Value = Empty(report.ClaudeIP), State = IsIPv4(report.ClaudeIP) ? "pass" : "warn", Detail = "端点不提供 trace 时显示未验证" },
            new() { Label = "三个 IPv4 出口", Value = report.SameIPv4 ? "一致" : "不一致或未验证", State = report.SameIPv4 ? "pass" : "warn", Detail = "自定义一致性检查，不是官方账户风控结论" },
            new() { Label = "Anthropic API", Value = report.ApiCode > 0 ? $"HTTP {report.ApiCode}" : "请求失败", State = report.ApiReachable ? "pass" : "fail", Detail = report.ApiReachable ? "HTTPS 可达；401 表示未提供认证，未验证账户授权" : report.ApiFailure },
            new() { Label = "Claude 登录页", Value = report.WebCode > 0 ? $"HTTP {report.WebCode}" + (report.WebCode == 403 ? " · 需浏览器确认" : "") : "未验证", State = report.WebReachable ? "pass" : "warn", Detail = report.WebCode == 403 ? "https://claude.ai/login · 自动请求被拒绝，不代表 Chrome 无法打开" : "https://claude.ai/login · 程序 HTTP 探测，不验证账户登录" },
            new() { Label = "登录跳转域名", Value = report.SignInCode>0 ? $"HTTP {report.SignInCode}" : "未验证", State = report.SignInCode is >=200 and <400 ? "pass" : "warn", Detail = "claude.com · "+(report.SignInCode==0 ? report.SignInFailure : "官方登录会访问此域名；本次不验证账户登录") },
            new() { Label = "OAuth 服务域名", Value = report.OAuthCode>0 ? $"HTTP {report.OAuthCode}" : "未验证", State = report.OAuthCode is >=200 and <400 ? "pass" : "warn", Detail = "platform.claude.com · "+(report.OAuthCode==0 ? report.OAuthFailure : "登录与令牌交换使用此域名；未发送登录凭据或令牌") },
            new() { Label = "HTTPS / TLS 校验", Value = new[]{report.ApiCode,report.WebCode,report.SignInCode,report.OAuthCode}.All(x=>x>0) ? "核心域名已完成握手" : "部分域名未验证", State = new[]{report.ApiCode,report.WebCode,report.SignInCode,report.OAuthCode}.All(x=>x>0) ? "pass" : "warn", Detail = "仅当前检测进程的证书校验；HTTP 403 不等于证书失败，也不等于账号封禁" },
            new() { Label = "账户、套餐与额度", Value = "未验证", State = "pending", Detail = "请在官方客户端确认；HTTPS 可达或 HTTP 401 不能证明账户授权" },
            new() { Label = "出口地区", Value = report.Country.Length > 0 ? $"{report.Country} · {report.City}" : "未验证", State = report.Country.Length>0 ? "info" : "pending", Detail = "第三方 IP 定位，仅供自定义预设参考；不判定使用资格" },
            new() { Label = "系统时区", Value = report.TimeZoneMatches ? "已匹配" : "未匹配或未知", State = report.TimeZoneMatches ? "pass" : "warn", Detail = report.IanaTimeZone.Length > 0 ? report.IanaTimeZone : "未获得可靠时区" }
        ]);
        cancellation.ThrowIfCancellationRequested();
        report.CheckedAt = DateTimeOffset.UtcNow;
        return report;
    }
    private static string Empty(string value) => value.Length > 0 ? value : "未验证";
    private static bool IsIPv4(string value) => IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;
    private static string TraceIP(string value) => Regex.Match(value, @"(?m)^ip=([^\r\n]+)").Groups[1].Value.Trim();
    private static string JsonValue(string value, string key)
    {
        try { return JsonNode.Parse(value)?[key]?.ToString() ?? ""; }
        catch (JsonException) { return ""; }
        catch (InvalidOperationException) { return ""; }
    }
}
