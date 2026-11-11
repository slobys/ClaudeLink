using System.Net.Sockets;

namespace Launcher.App.Services;

public sealed class SessionRequest
{
    public int OwnerPid { get; set; }
    public long OwnerStarted { get; set; }
    public int Port { get; set; }
    public string Config { get; set; } = "";
    public bool DisableIPv6 { get; set; }
    public bool Elevated { get; set; }
}
public sealed class HostStatus
{
    public string State { get; set; } = "starting";
    public string Stage { get; set; } = "initializing";
    public string Message { get; set; } = "";
    public int HostPid { get; set; }
    public long HostStarted { get; set; }
    public int CorePid { get; set; }
    public long CoreStarted { get; set; }
}
public sealed class EnvironmentCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TimeZoneId { get; set; } = "";
}
public sealed class EnvironmentReply
{
    public string Id { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public static class EngineHost
{
    public static async Task<int> RunAsync(string id, bool recoveryOnly = false)
    {
        var directory = AppPaths.Session(id);
        var statusPath = Path.Combine(directory, "status.json");
        var journalPath = Path.Combine(directory, "environment.json");
        var configPath = Path.Combine(directory, "config.json");
        var previous = AppPaths.ReadJson<HostStatus>(statusPath);
        if (previous is not null && ProcessTools.IsAlive(previous.HostPid, previous.HostStarted)) return 2;
        using var current = Process.GetCurrentProcess();
        var status = new HostStatus { HostPid = current.Id, HostStarted = current.StartTime.ToUniversalTime().Ticks };
        Process? core = null;
        CoreJob? job = null;
        FileStream? engineLock = null;
        var diagnostics = new SessionDiagnostics(directory);
        var captures = new List<Task>();
        Exception? failure = null;
        int? failedCoreExit = null;
        var failed = false;
        void SetStage(string stage) { status.Stage = stage; AppPaths.WriteJson(statusPath, status); }
        try
        {
            AppPaths.WriteJson(statusPath, status);
            try { engineLock = new FileStream(Path.Combine(AppPaths.Root, "engine.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new IOException("另一个后台会话正在运行或恢复，请稍后重试。"); }
            if (recoveryOnly)
            {
                // A killed host's job should have killed its core; refuse to undo a live session.
                if (previous is not null && ProcessTools.IsAlive(previous.CorePid, previous.CoreStarted))
                    throw new IOException("旧内核仍在运行，请先结束旧会话。");
                await WindowsEnvironment.RestoreAsync(journalPath);
                return 0;
            }
            SetStage("read-session");
            var request = SecretStore.Load<SessionRequest>(Path.Combine(directory, "request.dat"));
            if (!ProcessTools.IsAlive(request.OwnerPid, request.OwnerStarted)) throw new IOException("主界面已退出，本次会话已取消。");
            if (request.Port is < 1024 or > 65535) throw new InvalidDataException("无效的本地端口。");
            SetStage("validate-config");
            var parsed = JsonNode.Parse(request.Config)!.AsObject();
            diagnostics.RedactNode(parsed);
            // Only the application's generated inbounds/config are accepted, never a downloaded full config.
            if (parsed["outbounds"] is not JsonArray outbounds || outbounds.Count != 2) throw new InvalidDataException("无效内核配置。");
            var outbound = outbounds[0]!.DeepClone().AsObject(); outbound.Remove("tag"); NodeImporter.Validate(outbound);
            var tun = parsed["inbounds"]!.AsArray().Any(x => x?["type"]?.ToString() == "tun");
            var node = new ProxyNode { OutboundJson = outbound.ToJsonString() };
            var trustedConfig = CoreConfigBuilder.Build(node, request.Port, tun);
            File.WriteAllText(configPath, trustedConfig.ToJsonString(JsonDefaults.Pretty), new UTF8Encoding(false));
            SetStage("extract-core");
            var binary = CoreAssets.Ensure();
            SetStage("check-config");
            var check = await ProcessTools.RunAsync(ProcessTools.Hidden(binary, "check", "-c", configPath));
            if (check.Code != 0) throw new IOException("内核拒绝此节点配置，请检查协议、TLS 或传输参数。");
            if (request.DisableIPv6)
            {
                SetStage("apply-ipv6");
                await WindowsEnvironment.ApplyIPv6Async(journalPath);
            }
            SetStage("start-core");
            var startInfo = ProcessTools.Hidden(binary, "run", "-c", configPath);
            // Keep a hidden console so normal stop can send Ctrl+C for native TUN cleanup.
            startInfo.CreateNoWindow = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
            core = Process.Start(startInfo) ?? throw new IOException("内核启动失败。");
            captures.Add(diagnostics.CaptureCoreAsync(core.StandardOutput));
            captures.Add(diagnostics.CaptureCoreAsync(core.StandardError));
            status.CorePid = core.Id; status.CoreStarted = core.StartTime.ToUniversalTime().Ticks;
            SetStage("attach-job");
            if (core.HasExited) throw new IOException($"内核启动后立即退出（代码 {core.ExitCode}）。");
            job = new CoreJob(core);
            SetStage("wait-listener");
            var ready = false;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (core.HasExited) break;
                try { using var socket = new TcpClient(); await socket.ConnectAsync(IPAddress.Loopback, request.Port); ready = true; break; }
                catch (SocketException) { await Task.Delay(150); }
            }
            if (!ready) throw new IOException("代理端口未就绪，内核启动失败或端口已被占用。");
            status.State = "running"; status.Message = "内置代理已连接";
            status.Stage = "running";
            AppPaths.WriteJson(statusPath, status);
            while (!core.HasExited && ProcessTools.IsAlive(request.OwnerPid, request.OwnerStarted) && !File.Exists(Path.Combine(directory, "stop.signal")))
            {
                var commandPath = Path.Combine(directory, "environment-request.dat");
                if (File.Exists(commandPath))
                {
                    var reply = new EnvironmentReply();
                    try
                    {
                        var command = SecretStore.Load<EnvironmentCommand>(commandPath);
                        reply.Id = command.Id;
                        if (!request.Elevated) throw new IOException("此会话未请求管理员授权，无法修改系统时区。");
                        await WindowsEnvironment.ApplyTimeZoneAsync(journalPath, command.TimeZoneId);
                        reply.Success = true; reply.Message = "时区已匹配";
                    }
                    catch (Exception) { reply.Message = "时区修改失败，原始恢复记录已保留。"; }
                    File.Delete(commandPath);
                    AppPaths.WriteJson(Path.Combine(directory, "environment-reply.json"), reply);
                }
                await Task.Delay(300);
            }
            if (core.HasExited && !File.Exists(Path.Combine(directory, "stop.signal")))
                throw new IOException("代理内核意外退出，本次环境将恢复。");
        }
        catch (Exception ex)
        {
            failed = true;
            failure = ex;
            try { if (core?.HasExited == true) failedCoreExit = core.ExitCode; } catch (InvalidOperationException) { }
            var native = ex is Win32Exception win ? $"，错误 {win.NativeErrorCode}" : "";
            status.Message = diagnostics.Sanitize($"启动失败（{SessionDiagnostics.StageLabel(status.Stage)}{native}）：{ex.Message}");
        }
        finally
        {
            if (core is not null)
            {
                try { await CoreJob.StopAsync(core); } catch (Exception) { failed = true; status.Message = "内核停止未确认，请使用恢复功能。"; }
                job?.Dispose(); job = null;
                try { await Task.WhenAll(captures).WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) { }
                core.Dispose();
            }
            job?.Dispose();
            try { await WindowsEnvironment.RestoreAsync(journalPath); }
            catch (Exception) { failed = true; status.Message = "部分系统设置尚未恢复，请点击「恢复临时设置」。"; }
            try { File.Delete(configPath); } catch (IOException) { }
            if (failure is not null)
            {
                try { diagnostics.RecordFailure(status.Stage, failure, failedCoreExit); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            status.State = failed ? "failed" : "stopped";
            if (!failed) status.Message = "内核已停止，临时环境已恢复";
            AppPaths.WriteJson(statusPath, status);
            engineLock?.Dispose();
        }
        return failed ? 1 : 0;
    }
}

public sealed class SessionController
{
    public string? Id { get; private set; }
    public bool Connected { get; private set; }
    public int Port { get; private set; }
    public string? NodeId { get; private set; }
    private Process? host;
    public string? DirectoryPath => Id is null ? null : AppPaths.Session(Id);
    public event Action<string>? UnexpectedExit;

    public async Task StartAsync(LauncherSettings settings, ProxyNode node)
    {
        if (Connected) return;
        if (Id is not null) await StopAsync();
        if (PendingRecovery().Count > 0) throw new IOException("发现未完成的系统恢复记录，请先点击恢复临时设置。");
        if (!await PortIsAvailableAsync(settings.ProxyPort)) throw new IOException("本地代理端口已占用，请在设置中修改端口。");
        Id = Guid.NewGuid().ToString("N"); Port = settings.ProxyPort; NodeId = node.Id;
        Directory.CreateDirectory(DirectoryPath!);
        using var owner = Process.GetCurrentProcess();
        var elevated = settings.TunEnabled || settings.DisableIPv6 || settings.MatchTimeZone;
        var request = new SessionRequest
        {
            OwnerPid = owner.Id, OwnerStarted = owner.StartTime.ToUniversalTime().Ticks, Port = Port,
            Config = CoreConfigBuilder.Build(node, Port, settings.TunEnabled).ToJsonString(),
            DisableIPv6 = settings.DisableIPv6, Elevated = elevated
        };
        SecretStore.Save(Path.Combine(DirectoryPath!, "request.dat"), request);
        try
        {
            host = ProcessTools.StartSelf("--engine-host", Id, elevated);
            var until = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < until)
            {
                var status = AppPaths.ReadJson<HostStatus>(Path.Combine(DirectoryPath!, "status.json"));
                if (status?.State == "running") { Connected = true; _ = MonitorAsync(Id); return; }
                if (status?.State == "failed") throw new IOException(status.Message);
                if (host.HasExited) throw new IOException(status?.Message is { Length: > 0 } m ? m : "会话宿主未能启动。");
                await Task.Delay(250);
            }
            throw new TimeoutException("等待内核启动超时。");
        }
        catch
        {
            await StopAsync();
            throw;
        }
    }
    public async Task ApplyTimeZoneAsync(string zone)
    {
        if (!Connected) throw new InvalidOperationException("请先连接。");
        var command = new EnvironmentCommand { TimeZoneId = zone };
        SecretStore.Save(Path.Combine(DirectoryPath!, "environment-request.dat"), command);
        var until = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < until)
        {
            var reply = AppPaths.ReadJson<EnvironmentReply>(Path.Combine(DirectoryPath!, "environment-reply.json"));
            if (reply?.Id == command.Id)
            {
                if (!reply.Success) throw new IOException(reply.Message);
                return;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("等待时区操作超时，已保留恢复记录。");
    }
    public async Task StopAsync(bool allowPendingRecovery = false)
    {
        Connected = false;
        if (DirectoryPath is not null) File.WriteAllText(Path.Combine(DirectoryPath, "stop.signal"), "");
        if (host is not null)
        {
            using var timeout = new CancellationTokenSource(45000);
            try { await host.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { throw new IOException("后台仍在恢复临时设置，请稍后点击恢复。"); }
            finally { host.Dispose(); host = null; }
        }
        if (DirectoryPath is not null)
        {
            var journal = AppPaths.ReadJson<EnvironmentJournal>(Path.Combine(DirectoryPath, "environment.json"));
            if (journal is { Restored: false } && !allowPendingRecovery) throw new IOException("部分环境设置尚未恢复，请点击恢复临时设置。");
            CleanupPlaintext(DirectoryPath);
        }
        Id = null; NodeId = null;
    }
    public static void CleanupStaleSessions()
    {
        if (!Directory.Exists(AppPaths.Sessions)) return;
        foreach (var directory in Directory.EnumerateDirectories(AppPaths.Sessions))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            CleanupPlaintext(directory);
        }
    }
    private static void CleanupPlaintext(string directory)
    {
        var status = AppPaths.ReadJson<HostStatus>(Path.Combine(directory, "status.json"));
        if (status is not null && (ProcessTools.IsAlive(status.HostPid, status.HostStarted) || ProcessTools.IsAlive(status.CorePid, status.CoreStarted))) return;
        var config = Path.Combine(directory, "config.json");
        if (File.Exists(config)) File.Delete(config);
    }
    private async Task MonitorAsync(string id)
    {
        while (Connected && Id == id)
        {
            await Task.Delay(1000);
            if (!Connected || Id != id) break;
            var status = AppPaths.ReadJson<HostStatus>(Path.Combine(AppPaths.Session(id), "status.json"));
            if (status is null || status.State != "running" || !ProcessTools.IsAlive(status.HostPid, status.HostStarted))
            {
                Connected = false;
                UnexpectedExit?.Invoke(status?.Message ?? "后台会话已退出。");
                break;
            }
        }
    }
    public static List<string> PendingRecovery()
    {
        if (!Directory.Exists(AppPaths.Sessions)) return [];
        return Directory.EnumerateDirectories(AppPaths.Sessions)
            .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _))
            .Where(path => AppPaths.ReadJson<EnvironmentJournal>(Path.Combine(path, "environment.json")) is { Restored: false })
            .Where(path =>
            {
                var state = AppPaths.ReadJson<HostStatus>(Path.Combine(path, "status.json"));
                return state is null || !ProcessTools.IsAlive(state.HostPid, state.HostStarted);
            }).Select(Path.GetFileName).Select(x => x!).ToList();
    }
    private static async Task<bool> PortIsAvailableAsync(int port)
    {
        if (port is < 1024 or > 65535) return false;
        var listener = new TcpListener(IPAddress.Loopback, port);
        try { listener.Start(); await Task.CompletedTask; return true; }
        catch (SocketException) { return false; }
        finally { listener.Stop(); }
    }
}
