using System.Net.Sockets;
using System.Net.WebSockets;
using Launcher.App.Services;

namespace Launcher.App;

// Uses disposable test data and loopback fixtures. Never loads user nodes or enables TUN.
public static partial class IntegrationSmoke
{
    public static async Task<int> RunAsync(string outputPath)
    {
        var checks = new List<string>();
        var testId = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("CLAUDELINK_TEST_ID", testId);
        AppPaths.Root = Path.Combine(AppPaths.Root, "tests", testId);
        Directory.CreateDirectory(AppPaths.Root);
        SessionController? controller = null;
        void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            checks.Add(name);
            AppPaths.WriteJson(outputPath, new { Passed = checks.Count, Checks = checks, InProgress = true });
        }
        try
        {
            var secret = "test-only-secret-" + testId;
            var settings = new LauncherSettings { TunEnabled = false, StrictChecks = false, DisableIPv6 = false, MatchTimeZone = false };
            settings.Subscriptions.Add(new Subscription { Name = "Fixture", Url = "https://example.invalid/" + secret });
            var store = new SettingsStore(); store.Save(settings);
            var bytes = File.ReadAllBytes(Path.Combine(AppPaths.Root, "settings.dat"));
            Assert(!Encoding.UTF8.GetString(bytes).Contains(secret), "DPAPI encrypted storage does not expose subscription token");
            Assert(store.Load().Subscriptions[0].Url.EndsWith(secret), "DPAPI round trip for current user");
            await CheckPowerShellStreamsAsync(Assert);
            await CheckNetworkReportingAsync(Assert);
            await FeatureSmoke.RunAsync(Assert, outputPath);
            await CheckBrowserLanguagesAsync(outputPath, Assert);
            await CheckProxyChainAsync(Assert);
            await CheckNodeLatencyAsync(Assert);

            await using var proxy = new HttpFixture("launcher-loopback-ok");
            var node = NodeImporter.Parse($"http://127.0.0.1:{proxy.Port}#test-proxy").Nodes.Single();
            settings.Nodes = [node]; settings.SelectedNodeId = node.Id; settings.ProxyPort = FreePort();
            var conflict = new TcpListener(IPAddress.Loopback, settings.ProxyPort); conflict.Start();
            var rejected = false;
            try { await new SessionController().StartAsync(settings, node); } catch (IOException) { rejected = true; }
            finally { conflict.Stop(); }
            Assert(rejected, "Occupied proxy port rejected before session changes");

            controller = new SessionController();
            await controller.StartAsync(settings, node);
            var directory = controller.DirectoryPath!;
            var status = AppPaths.ReadJson<HostStatus>(Path.Combine(directory, "status.json"))!;
            Assert(controller.Connected && ProcessTools.IsAlive(status.CorePid, status.CoreStarted), "Independent host starts real bundled core");
            var secondSettings = new LauncherSettings { ProxyPort = FreePort(), TunEnabled = false, StrictChecks = false, DisableIPv6 = false, MatchTimeZone = false };
            var second = new SessionController(); var secondRejected = false;
            try { await second.StartAsync(secondSettings, node); } catch (IOException) { secondRejected = true; }
            Assert(secondRejected && ProcessTools.IsAlive(status.CorePid, status.CoreStarted), "Background session lock prevents overlapping engines without stopping active core");
            using (var client = NetworkServices.ProxyClient(controller.Port, 10))
            {
                var body = await client.GetStringAsync("http://198.51.100.8/launcher-check");
                Assert(body == "launcher-loopback-ok" && proxy.Requests > 0, "HTTP mixed inbound routes through configured outbound to local fixture");
            }
            await controller.StopAsync();
            Assert(!ProcessTools.IsAlive(status.CorePid, status.CoreStarted), "Normal stop terminates core process");
            Assert(!File.Exists(Path.Combine(directory, "config.json")), "Normal stop removes plaintext runtime credentials");
            Assert(await IsPortClosedAsync(settings.ProxyPort), "Normal stop releases proxy port");

            await controller.StartAsync(settings, node);
            directory = controller.DirectoryPath!;
            status = AppPaths.ReadJson<HostStatus>(Path.Combine(directory, "status.json"))!;
            using (var host = Process.GetProcessById(status.HostPid)) { host.Kill(); await host.WaitForExitAsync(); }
            await Task.Delay(1200);
            Assert(!ProcessTools.IsAlive(status.CorePid, status.CoreStarted), "Windows job kills core when host is forcibly terminated");
            await controller.StopAsync();
            Assert(!File.Exists(Path.Combine(directory, "config.json")), "Interrupted host plaintext configuration cleaned after process verification");

            await CheckOwnerDeathAsync(node, Assert);
            await CheckShadowsocksAsync(Assert);
            await CheckSubscriptionAsync(Assert);
            await CheckClaudeEnvironmentAsync(Assert);

            var recoveryId = Guid.NewGuid().ToString("N");
            var recoveryDirectory = AppPaths.Session(recoveryId); Directory.CreateDirectory(recoveryDirectory);
            AppPaths.WriteJson(Path.Combine(recoveryDirectory, "environment.json"), new EnvironmentJournal());
            Assert(SessionController.PendingRecovery().Contains(recoveryId), "Stale recovery journal detected before new session");
            using (var recovery = ProcessTools.StartSelf("--recover", recoveryId, false))
            {
                using var timeout = new CancellationTokenSource(10000); await recovery.WaitForExitAsync(timeout.Token);
                Assert(recovery.ExitCode == 0 && AppPaths.ReadJson<EnvironmentJournal>(Path.Combine(recoveryDirectory, "environment.json"))!.Restored, "Recovery helper completes idempotent empty journal");
            }
            Assert(SessionController.PendingRecovery().Count == 0, "Completed recovery clears pending gate");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            AppPaths.WriteJson(outputPath, new { Passed = checks.Count, Checks = checks, LiveTunTested = false, RealSubscriptionTested = false, TestData = AppPaths.Root });
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(outputPath + ".failure.log", ex.ToString());
            try { if (controller is not null) await controller.StopAsync(); } catch { }
            AppPaths.WriteJson(outputPath, new { Passed = checks.Count, Checks = checks, Failed = ex.Message, ErrorType = ex.GetType().Name, TestData = AppPaths.Root });
            return 1;
        }
    }

    private static async Task CheckNetworkReportingAsync(Action<bool, string> assert)
    {
        var fixture = new NetworkFixtureHandler();
        using var client = new HttpClient(fixture);
        var report = await NetworkServices.CheckWithClientAsync(client, true, false);
        report.IPv6Disabled = true; report.TimeZoneMatches = true;
        assert(report.ApiCode == 401 && report.ApiReachable && report.BasicReady, "API 401 represents HTTPS reachability without claiming account authorization");
        assert(report.WebCode == 403 && !report.WebReachable && !report.StrictReady, "Website 403 remains a separate strict-check failure");
        assert(fixture.LoginRequested && report.Results.Single(x => x.Label == "Claude 登录页").Detail.Contains("不代表 Chrome"), "Website probe targets requested login URL and separates automated rejection from browser access");
        assert(report.BlockingReasons(false).Count == 0 && report.BlockingReasons(true).Single().Contains("403"), "Strict launch prompt identifies exact website failure despite passing basic API check");
        using var dnsClient = new HttpClient(new NetworkFixtureHandler(HttpRequestError.NameResolutionError));
        var dns = await NetworkServices.CheckWithClientAsync(dnsClient, false, false);
        assert(!dns.ApiReachable && dns.ApiFailure.Contains("DNS") && dns.BlockingReasons(false).Any(x => x.Contains("DNS")), "Failed API probe preserves DNS failure category");
        using var tlsClient = new HttpClient(new NetworkFixtureHandler(HttpRequestError.SecureConnectionError));
        var tls = await NetworkServices.CheckWithClientAsync(tlsClient, false, false);
        assert(!tls.ApiReachable && tls.ApiFailure.Contains("TLS"), "Failed API probe preserves TLS failure category");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var propagated = false;
        try { await NetworkServices.CheckWithClientAsync(client, false, false, cancellation: cancelled.Token); }
        catch (OperationCanceledException) { propagated = true; }
        assert(propagated, "User-cancelled network check propagates cancellation");
    }
    private sealed class NetworkFixtureHandler(HttpRequestError? failure = null) : HttpMessageHandler
    {
        public bool LoginRequested { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.RequestUri!;
            if (failure is { } error && uri.Host == "api.anthropic.com" && uri.AbsolutePath == "/v1/models")
                return Task.FromException<HttpResponseMessage>(new HttpRequestException(error, "Fixture network failure"));
            var status = HttpStatusCode.OK;
            var body = "";
            if (uri.Host == "api.ipify.org") body = """{"ip":"198.51.100.8"}""";
            else if (uri.AbsolutePath == "/cdn-cgi/trace") body = "ip=198.51.100.8\nloc=US\n";
            else if (uri.Host == "api.anthropic.com") { status = HttpStatusCode.Unauthorized; body = "{}"; }
            else if (uri.Host == "claude.ai" && uri.AbsolutePath == "/login") { LoginRequested = true; status = HttpStatusCode.Forbidden; body = "<html>Fixture denied</html>"; }
            else if (uri.Host == "claude.ai") throw new InvalidOperationException("Unexpected Claude webpage probe target.");
            else if (uri.Host == "ipwho.is") body = """{"country_code":"US","city":"Fixture","timezone":{"id":"America/Los_Angeles"}}""";
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") });
        }
    }
    private static async Task CheckBrowserLanguagesAsync(string outputPath, Action<bool, string> assert)
    {
        var installed = CheckBrowser.Installed();
        assert(installed.Count > 0, "Installed Chromium browser available for native language verification");
        assert(CheckBrowser.Select("auto").Id == (installed.Any(x => x.Id == "chrome") ? "chrome" : "edge"),
            "Automatic browser selection prefers installed Chrome");
        var results = new List<object>();
        foreach (var browser in installed)
        {
            const string privacyHtml = """
<!doctype html><meta charset="utf-8"><input id="ipMaskToggle" type="checkbox"><a class="ip-link" id="ip">198.51.100.24</a>
<script>
window.__ipMaskOn=false;
window.applyAllIpMasks=()=>{ip.textContent=window.__ipMaskOn?'198.51.*.*':'198.51.100.24'};
document.addEventListener('DOMContentLoaded',()=>{ipMaskToggle.checked=false;ipMaskToggle.addEventListener('change',()=>{window.__ipMaskOn=ipMaskToggle.checked;applyAllIpMasks()})});
setTimeout(()=>{ip.textContent='198.51.100.24';applyAllIpMasks();window.dynamicIPReady=true},120);
</script>
""";
            await using (var privacyPage = new HttpFixture(privacyHtml) { ContentType = "text/html; charset=utf-8" })
            {
                var profile = Path.Combine(AppPaths.Root, "browser-tests", browser.Id + "-privacy");
                CheckBrowser.PrepareProfile(profile, "en-US");
                var privacyState = await ReadNativeBrowserStateAsync(browser, profile, "en-US", $"http://127.0.0.1:{privacyPage.Port}/claude/", privacy: true);
                assert(privacyState["hidden"]!.GetValue<bool>() && privacyState["masked"]!.GetValue<bool>() && privacyState["reloaded"]!.GetValue<bool>(),
                    browser.Name + " dedicated page enables IP hiding before dynamic results and after reload");
                assert(privacyState["pages"]!.GetValue<int>() == 1, browser.Name + " dedicated browser contains only the requested check page");
            }
            foreach (var language in new[] { "en-US", "zh-CN" })
            {
                await using var page = new HttpFixture(CheckBrowser.LanguagePage(language)) { ContentType = "text/html; charset=utf-8" };
                var profile = Path.Combine(AppPaths.Root, "browser-tests", browser.Id + "-" + language);
                CheckBrowser.PrepareProfile(profile, language);
                // Use the same native window mode as production: this Edge build does not support dump-dom reliably.
                var state = await ReadNativeBrowserStateAsync(browser, profile, language, $"http://127.0.0.1:{page.Port}/language");
                var actualLanguage = state["language"]!.ToString();
                var languages = state["languages"]!.AsArray().Select(x => x!.ToString()).ToArray();
                assert(actualLanguage == language && languages.FirstOrDefault() == language,
                    browser.Name + " native navigator.language/languages follow selected " + language);
                var acceptLanguage = System.Text.RegularExpressions.Regex.Match(page.LastRequest, @"(?im)^Accept-Language:\s*([^\r\n]+)").Groups[1].Value;
                assert(acceptLanguage.StartsWith(language, StringComparison.OrdinalIgnoreCase),
                    browser.Name + " native HTTP Accept-Language follows selected " + language);
                results.Add(new { Browser = browser.Name, Mode = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "headless" : "window", Requested = language, NavigatorLanguage = actualLanguage, Languages = languages, AcceptLanguage = acceptLanguage, IntlLocale = state["locale"]!.ToString() });
                AppPaths.WriteJson(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "browser-language-results.json"), results);
            }
        }
        AppPaths.WriteJson(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "browser-language-results.json"), results);
    }

    private static async Task<JsonNode> ReadNativeBrowserStateAsync(BrowserInstallation browser, string profile, string language, string url, bool privacy = false)
    {
        var info = CheckBrowser.StartInfo(browser, profile, language, null, [privacy ? "about:blank" : url]);
        info.WindowStyle = ProcessWindowStyle.Hidden;
        info.ArgumentList.Add("--remote-debugging-port=0");
        info.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        // Hosted runners have no interactive desktop. Keep local verification in the actual window mode.
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true") info.ArgumentList.Add("--headless=new");
        using var process = Process.Start(info) ?? throw new IOException("Cannot start native browser test.");
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var socket = new ClientWebSocket();
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        try
        {
            if (privacy && !await BrowserPrivacy.ConfigureAsync(profile, url)) throw new IOException("IP privacy setup failed.");
            var portFile = Path.Combine(profile, "DevToolsActivePort");
            string? port = null;
            while (port == null)
            {
                // Edge may hand off startup to a new process before writing the port file.
                try
                {
                    await using var stream = new FileStream(portFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    var lines = (await reader.ReadToEndAsync(budget.Token)).Split('\n');
                    if (lines.Length > 1 && int.TryParse(lines[0].Trim(), out var candidate) && candidate is > 0 and < 65536
                        && lines[1].StartsWith("/devtools/browser/", StringComparison.Ordinal)) port = candidate.ToString();
                }
                catch (IOException) { } // The startup writer may briefly hold the file exclusively.
                if (port == null) await Task.Delay(100, budget.Token);
            }
            Uri? target = null;
            while (target == null)
            {
                var targets = JsonNode.Parse(await client.GetStringAsync("http://127.0.0.1:" + port + "/json/list", budget.Token))!.AsArray();
                var page = targets.FirstOrDefault(x => x?["type"]?.ToString() == "page" && x["url"]?.ToString() == url);
                if (page?["webSocketDebuggerUrl"] is { } address) target = new Uri(address.ToString());
                else await Task.Delay(100, budget.Token);
            }
            await socket.ConnectAsync(target, budget.Token);
            if (privacy)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes("{\"id\":99,\"method\":\"Page.reload\"}").AsMemory(), WebSocketMessageType.Text, true, budget.Token);
                await Task.Delay(500, budget.Token);
            }
            var id = 0;
            while (true)
            {
                var requestId = ++id;
                var expression = privacy
                    ? "window.dynamicIPReady && performance.navigation.type===1 && document.documentElement.dataset.claudeLinkIpHidden==='true' ? JSON.stringify({hidden:ipMaskToggle.checked,masked:ip.textContent==='198.51.*.*',reloaded:true}) : ''"
                    : "document.getElementById('browser-state')?.textContent || ''";
                var command = JsonSerializer.SerializeToUtf8Bytes(new { id = requestId, method = "Runtime.evaluate", @params = new { expression, returnByValue = true } });
                await socket.SendAsync(command.AsMemory(), WebSocketMessageType.Text, true, budget.Token);
                while (true)
                {
                    using var message = new MemoryStream();
                    var buffer = new byte[8192];
                    ValueWebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(buffer.AsMemory(), budget.Token);
                        if (received.MessageType == WebSocketMessageType.Close) throw new IOException(browser.Name + " closed its test page.");
                        message.Write(buffer, 0, received.Count);
                    } while (!received.EndOfMessage);
                    var response = JsonNode.Parse(message.ToArray())!;
                    if (response["id"]?.GetValue<int>() != requestId) continue;
                    if (response["error"] != null) throw new IOException("Native browser evaluation failed.");
                    var value = response["result"]?["result"]?["value"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var state = JsonNode.Parse(value)!;
                        if (privacy)
                        {
                            var pages = JsonNode.Parse(await client.GetStringAsync("http://127.0.0.1:" + port + "/json/list", budget.Token))!.AsArray();
                            state["pages"] = pages.Count(x => x?["type"]?.ToString() == "page");
                        }
                        return state;
                    }
                    break;
                }
                await Task.Delay(100, budget.Token);
            }
        }
        finally
        {
            if (socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(1000);
                try { await socket.SendAsync(Encoding.UTF8.GetBytes("{\"id\":999,\"method\":\"Browser.close\"}").AsMemory(), WebSocketMessageType.Text, true, closing.Token); }
                catch (WebSocketException) { }
                catch (OperationCanceledException) { }
            }
            using var stopped = new CancellationTokenSource(3000);
            try { await process.WaitForExitAsync(stopped.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
            // Also cover Edge's detached startup process, restricted to this fresh test profile.
            var profileLiteral = ProcessTools.PsLiteral(profile);
            var nameLiteral = ProcessTools.PsLiteral(Path.GetFileName(browser.Path));
            await ProcessTools.PowerShellAsync("Get-CimInstance Win32_Process -Filter \"Name=" + nameLiteral + "\" | Where-Object { $_.CommandLine -like '*--user-data-dir=*' -and $_.CommandLine.Contains(" + profileLiteral + ") -and $_.CommandLine -notlike '*--type=*' } | ForEach-Object { $testBrowser = Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; if ($null -ne $testBrowser -and [Math]::Abs(($testBrowser.StartTime.ToUniversalTime() - $_.CreationDate.ToUniversalTime()).TotalSeconds) -lt 1) { & taskkill.exe /PID $testBrowser.Id /T /F | Out-Null } }", 10);
        }
    }

    private static async Task CheckPowerShellStreamsAsync(Action<bool, string> assert)
    {
        const string json = "[\"stream-regression-ok\"]";
        var script = "$ProgressPreference='Continue';Write-Progress -Activity 'Preparing regression fixture' -Status 'load' -PercentComplete 25;Write-Warning 'stream regression warning';'" + json + "'";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        var oldResult = await ProcessTools.RunDetailedAsync(ProcessTools.Hidden(path, "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);" + script))));
        assert(oldResult.Code == 0 && oldResult.StandardError.Contains("CLIXML") && JsonNode.Parse(oldResult.StandardOutput)!.AsArray().Count == 1,
            "PowerShell regression fixture emits valid JSON and separate progress/warning CLIXML");
        var mergedRejected = false;
        try { JsonNode.Parse(oldResult.StandardOutput + oldResult.StandardError); } catch (JsonException) { mergedRejected = true; }
        assert(mergedRejected, "Original stdout-plus-stderr merge reproduces JSON startup failure");
        var fixedResult = await ProcessTools.PowerShellAsync(script);
        assert(fixedResult.Code == 0 && JsonNode.Parse(fixedResult.Output)![0]!.ToString() == "stream-regression-ok",
            "PowerShell structured output remains valid despite progress and warnings");
        var adapters = await WindowsEnvironment.ReadEnabledIPv6AdaptersAsync();
        assert(adapters.All(id => Guid.TryParse(id, out _)), "Actual physical IPv6 state read succeeds without modifying bindings");
        var virtualFixture = await ProcessTools.PowerShellAsync("function Get-NetAdapter {[pscustomobject]@{HardwareInterface=$false;Status='Up';Name='virtual'}};function Get-NetAdapterBinding {throw 'Virtual binding must not be queried'};" + WindowsEnvironment.EnabledIPv6Query);
        assert(virtualFixture.Code == 0 && JsonNode.Parse(virtualFixture.Output)!.AsArray().Count == 0,
            "Virtual-only Windows host yields an empty physical IPv6 list without ObjectNotFound");
        var physicalFixture = await ProcessTools.PowerShellAsync("function Get-NetAdapter {[pscustomobject]@{HardwareInterface=$true;Status='Up';Name='physical';InterfaceGuid='f76dc73e-76e8-46ad-bb5d-d40b341cb3cb'};[pscustomobject]@{HardwareInterface=$false;Status='Up';Name='virtual'}};function Get-NetAdapterBinding {param($Name,$ComponentID);if($Name -ne 'physical'){throw 'Unexpected adapter'};[pscustomobject]@{Enabled=$true}};" + WindowsEnvironment.EnabledIPv6Query);
        assert(physicalFixture.Code == 0 && JsonNode.Parse(physicalFixture.Output)!.AsArray().Single()!.ToString() == "f76dc73e-76e8-46ad-bb5d-d40b341cb3cb",
            "Physical IPv6 filtering preserves enabled real adapters and excludes virtual adapters");
        var missingFixture = await ProcessTools.PowerShellAsync("function Get-NetAdapter {[pscustomobject]@{HardwareInterface=$true;Status='Up';Name='unbound'}};function Get-NetAdapterBinding {Write-Error -ErrorId 'CmdletizationQuery_NotFound' -Message 'fixture binding absent' -ErrorAction Stop};" + WindowsEnvironment.EnabledIPv6Query);
        assert(missingFixture.Code == 0 && JsonNode.Parse(missingFixture.Output)!.AsArray().Count == 0,
            "A physical adapter without the IPv6 binding is correctly treated as unbound");
        var deniedFixture = await ProcessTools.PowerShellAsync("function Get-NetAdapter {[pscustomobject]@{HardwareInterface=$true;Status='Up';Name='denied'}};function Get-NetAdapterBinding {throw 'fixture access denied'};" + WindowsEnvironment.EnabledIPv6Query);
        assert(deniedFixture.Code != 0, "Binding access failures remain failures rather than being called IPv6 disabled");
        var diagnostics = new SessionDiagnostics(AppPaths.Root);
        diagnostics.RedactNode(JsonNode.Parse("""{"type":"trojan","server":"fixture-secret-host.invalid","password":"fixture-secret-password"}""")!);
        var sanitized = diagnostics.Sanitize("fixture-secret-password at fixture-secret-host.invalid https://example.invalid/?token=fixture-token");
        assert(!sanitized.Contains("fixture-secret-password") && !sanitized.Contains("fixture-secret-host.invalid") && !sanitized.Contains("fixture-token"),
            "Session diagnostics redact node credentials and URLs");
    }

    private static async Task CheckOwnerDeathAsync(ProxyNode node, Action<bool, string> assert)
    {
        using var owner = Process.Start(ProcessTools.Hidden("powershell.exe", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 60"))!;
        var id = Guid.NewGuid().ToString("N"); var directory = AppPaths.Session(id); Directory.CreateDirectory(directory);
        var port = FreePort();
        SecretStore.Save(Path.Combine(directory, "request.dat"), new SessionRequest
        {
            OwnerPid = owner.Id, OwnerStarted = owner.StartTime.ToUniversalTime().Ticks,
            Port = port, Config = CoreConfigBuilder.Build(node, port, false).ToJsonString()
        });
        using var host = ProcessTools.StartSelf("--engine-host", id, false);
        try
        {
            var status = await WaitStatusAsync(directory, "running");
            owner.Kill(); await owner.WaitForExitAsync();
            using var timeout = new CancellationTokenSource(12000); await host.WaitForExitAsync(timeout.Token);
            var stopped = AppPaths.ReadJson<HostStatus>(Path.Combine(directory, "status.json"));
            assert(stopped?.State == "stopped" && !ProcessTools.IsAlive(status.CorePid, status.CoreStarted), "Host watchdog stops core after owning application exits");
        }
        finally
        {
            if (!owner.HasExited) owner.Kill();
            if (!host.HasExited) { File.WriteAllText(Path.Combine(directory, "stop.signal"), ""); await host.WaitForExitAsync(); }
        }
    }
    private static async Task CheckShadowsocksAsync(Action<bool, string> assert)
    {
        await using var echo = new HttpFixture("shadowsocks-roundtrip-ok");
        var core = CoreAssets.Ensure(); var serverPort = FreePort(); var clientPort = FreePort();
        var serverPath = Path.Combine(AppPaths.Root, "ss-server.json");
        var clientPath = Path.Combine(AppPaths.Root, "ss-client.json");
        var server = new JsonObject
        {
            ["log"] = new JsonObject { ["disabled"] = true },
            ["inbounds"] = new JsonArray(new JsonObject { ["type"] = "shadowsocks", ["listen"] = "127.0.0.1", ["listen_port"] = serverPort, ["method"] = "aes-128-gcm", ["password"] = "test-password" }),
            ["outbounds"] = new JsonArray(new JsonObject { ["type"] = "direct" })
        };
        var node = NodeImporter.Parse($"ss://{Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-128-gcm:test-password"))}@127.0.0.1:{serverPort}").Nodes.Single();
        var config = CoreConfigBuilder.Build(node, clientPort, false);
        // Force this private loopback fixture through the encrypted protocol for the integration test.
        config["route"]!["rules"] = new JsonArray();
        config["log"] = new JsonObject { ["disabled"] = true };
        File.WriteAllText(serverPath, server.ToJsonString()); File.WriteAllText(clientPath, config.ToJsonString());
        using var serverProcess = Process.Start(ProcessTools.Hidden(core, "run", "-c", serverPath))!;
        using var clientProcess = Process.Start(ProcessTools.Hidden(core, "run", "-c", clientPath))!;
        try
        {
            await WaitPortAsync(serverPort); await WaitPortAsync(clientPort);
            using var client = NetworkServices.ProxyClient(clientPort, 10);
            var body = await client.GetStringAsync($"http://127.0.0.1:{echo.Port}/encrypted-test");
            assert(body == "shadowsocks-roundtrip-ok" && echo.Requests > 0, "Real Shadowsocks encrypted client/server round trip through official core");
        }
        finally
        {
            if (!clientProcess.HasExited) { clientProcess.Kill(true); await clientProcess.WaitForExitAsync(); }
            if (!serverProcess.HasExited) { serverProcess.Kill(true); await serverProcess.WaitForExitAsync(); }
            File.Delete(serverPath); File.Delete(clientPath);
        }
    }
    private static async Task CheckSubscriptionAsync(Action<bool, string> assert)
    {
        var link = "trojan://test-password@example.com:443?sni=example.com#Fixture";
        await using var server = new HttpFixture(Convert.ToBase64String(Encoding.UTF8.GetBytes(link)));
        var subscription = new Subscription { Name = "Fixture subscription", Url = $"http://127.0.0.1:{server.Port}/sub" };
        var settings = new LauncherSettings { Subscriptions = [subscription] };
        var downloaded = await NetworkServices.DownloadSubscriptionAsync(subscription.Url);
        SubscriptionMerge.Replace(settings, subscription, downloaded.Result);
        assert(settings.Nodes.Count == 1 && subscription.UpdatedAt is not null, "Subscription HTTP download parses and commits nodes");
        var original = settings.Nodes[0].Id; server.StatusCode = 500;
        var failed = false;
        try { var next = await NetworkServices.DownloadSubscriptionAsync(subscription.Url); SubscriptionMerge.Replace(settings, subscription, next.Result); }
        catch (IOException) { failed = true; }
        assert(failed && settings.Nodes[0].Id == original, "HTTP failure preserves existing subscription nodes");
        server.StatusCode = 200; server.Body = "<html>invalid subscription</html>";
        failed = false;
        try { var next = await NetworkServices.DownloadSubscriptionAsync(subscription.Url); SubscriptionMerge.Replace(settings, subscription, next.Result); }
        catch (IOException) { failed = true; }
        assert(failed && settings.Nodes[0].Id == original, "Malformed subscription preserves existing nodes");
    }
    private static async Task CheckClaudeEnvironmentAsync(Action<bool, string> assert)
    {
        var directory = Path.Combine(AppPaths.Root, "work [样例] 'quote' & space");
        Directory.CreateDirectory(directory);
        var snapshotPath = Path.Combine(directory, "child.json");
        var fakeCli = Path.Combine(directory, "mock-claude.cmd");
        var payload = "@{proxy=$env:HTTP_PROXY;https=$env:HTTPS_PROXY;lang=$env:LANG;cwd=[Environment]::CurrentDirectory;pscwd=(Get-Location).Path} | ConvertTo-Json | Set-Content -LiteralPath " + ProcessTools.PsLiteral(snapshotPath) + " -Encoding UTF8";
        File.WriteAllText(fakeCli, "@echo off\r\npowershell.exe -NoProfile -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(payload)) + "\r\nexit /b 0\r\n", Encoding.ASCII);
        var originalProxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
        var script = ClaudeTerminal.BuildScript(fakeCli, directory, directory, 17890, "en-US");
        var result = await ProcessTools.PowerShellAsync(script);
        var snapshot = AppPaths.ReadJson<Dictionary<string, string>>(snapshotPath);
        assert(snapshot is not null, "Windows PowerShell UTF-8 BOM completion/JSON is readable");
        assert(result.Code == 0 && snapshot?.GetValueOrDefault("proxy") == "http://127.0.0.1:17890" && snapshot.GetValueOrDefault("cwd") == directory, "Claude wrapper passes process proxy and quoted Unicode project directory");
        assert(snapshot?.GetValueOrDefault("lang") == "en_US.UTF-8" && Environment.GetEnvironmentVariable("HTTP_PROXY") == originalProxy, "Claude environment is scoped to child process");
        assert(File.Exists(Path.Combine(directory, "claude-complete.json")), "Claude wrapper writes real completion signal");
    }
    private static async Task<HostStatus> WaitStatusAsync(string directory, string expected)
    {
        for (var i = 0; i < 100; i++)
        {
            var status = AppPaths.ReadJson<HostStatus>(Path.Combine(directory, "status.json"));
            if (status?.State == expected) return status;
            if (status?.State == "failed") throw new IOException(status.Message);
            await Task.Delay(150);
        }
        throw new TimeoutException("Fixture host timeout");
    }
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    private static async Task WaitPortAsync(int port)
    {
        for (var i = 0; i < 100; i++)
        {
            try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port); return; }
            catch (SocketException) { await Task.Delay(100); }
        }
        throw new TimeoutException("Fixture port timeout");
    }
    private static async Task<bool> IsPortClosedAsync(int port)
    {
        try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port); return false; }
        catch (SocketException) { return true; }
    }

    private sealed class HttpFixture : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task loop;
        private int requests;
        public string Body { get; set; }
        public int StatusCode { get; set; } = 200;
        public string ContentType { get; set; } = "text/plain; charset=utf-8";
        public string LastRequest { get; private set; } = "";
        public string LastTunnelRequest { get; private set; } = "";
        public int Requests => requests;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public HttpFixture(string body) { Body = body; listener.Start(); loop = ListenAsync(); }
        private async Task ListenAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                    _ = RespondAsync(client);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        }
        private async Task RespondAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    await using var stream = client.GetStream();
                    var request = await ReadHeaderAsync(stream);
                    if (request.Length == 0) return;
                    if (request.StartsWith("CONNECT ", StringComparison.Ordinal))
                    {
                        LastTunnelRequest = request;
                        await stream.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), cancellation.Token);
                        request = await ReadHeaderAsync(stream);
                        if (request.Length == 0) return;
                    }
                    Interlocked.Increment(ref requests);
                    LastRequest = request;
                    var body = Encoding.UTF8.GetBytes(Body);
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {StatusCode} Fixture\r\nContent-Type: {ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header, cancellation.Token); await stream.WriteAsync(body, cancellation.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
            }
        }
        private async Task<string> ReadHeaderAsync(NetworkStream stream)
        {
            var bytes = new List<byte>(); var one = new byte[1];
            while (bytes.Count < 16384)
            {
                if (await stream.ReadAsync(one, cancellation.Token) == 0) break;
                bytes.Add(one[0]);
                if (bytes.Count >= 4 && bytes[^4] == 13 && bytes[^3] == 10 && bytes[^2] == 13 && bytes[^1] == 10) break;
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
        public async ValueTask DisposeAsync() { cancellation.Cancel(); listener.Stop(); await loop; cancellation.Dispose(); }
    }
}
