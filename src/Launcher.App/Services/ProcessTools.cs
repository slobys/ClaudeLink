using System.Reflection;

namespace Launcher.App.Services;

public sealed record ProcessResult(int Code, string StandardOutput, string StandardError);

public static class ProcessTools
{
    public static ProcessStartInfo Hidden(string path, params string[] args)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
    public static async Task<(int Code, string Output)> RunAsync(ProcessStartInfo info, int timeoutSeconds = 30, CancellationToken cancellation = default)
    {
        var result = await RunDetailedAsync(info, timeoutSeconds, cancellation);
        return (result.Code, result.StandardOutput + result.StandardError);
    }
    public static async Task<ProcessResult> RunDetailedAsync(ProcessStartInfo info, int timeoutSeconds = 30, CancellationToken cancellation = default)
    {
        info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using var process = Process.Start(info) ?? throw new IOException("无法启动所需的后台进程。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw;
        }
    }
    public static string PsLiteral(string value) => "'" + value.Replace("'", "''") + "'";
    public static async Task<(int Code, string Output)> PowerShellAsync(string script, int timeout = 30)
    {
        var result = await RunDetailedAsync(Hidden(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(
                "$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);" + script))), timeout);
        // PowerShell 5.1 can emit successful module-import progress as CLIXML on stderr.
        // Structured output must NEVER be concatenated with diagnostic/error streams.
        return (result.Code, result.StandardOutput);
    }
    public static bool IsAlive(int pid, long started)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == started; }
        catch (ArgumentException) { return false; }
        catch (Win32Exception) { return false; }
        catch (InvalidOperationException) { return false; }
    }
    public static Process StartSelf(string command, string id, bool elevate)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("无效会话。");
        var exe = Environment.ProcessPath!;
        var info = elevate
            ? new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, Arguments = command + " " + id }
            : Hidden(exe, command, id);
        return Process.Start(info) ?? throw new IOException("无法启动会话宿主。");
    }
}

public static class CoreAssets
{
    public const string Version = "1.14.4";
    public static string Ensure()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var manifestStream = assembly.GetManifestResourceStream("Core.manifest.json")!;
        using var manifest = JsonDocument.Parse(manifestStream);
        var expected = manifest.RootElement.GetProperty("exeSha256").GetString()!;
        var dir = Path.Combine(AppPaths.Root, "core", Version);
        var path = Path.Combine(dir, "sing-box.exe");
        Directory.CreateDirectory(dir);
        if (!File.Exists(path) || !HashMatches(path, expected))
        {
            using var resource = assembly.GetManifestResourceStream("Core.sing-box.exe") ?? throw new FileNotFoundException("内置代理内核缺失。");
            var temp = path + ".tmp";
            using (var file = File.Create(temp)) resource.CopyTo(file);
            if (!HashMatches(temp, expected)) { File.Delete(temp); throw new CryptographicException("内核完整性校验失败。"); }
            File.Move(temp, path, true);
            using var license = assembly.GetManifestResourceStream("Core.LICENSE.txt")!;
            using var target = File.Create(Path.Combine(dir, "LICENSE.txt"));
            license.CopyTo(target);
        }
        return path;
    }
    private static bool HashMatches(string path, string expected)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(file)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}

// The OS closes this job if the host crashes, preventing an orphan proxy/TUN core.
public sealed class CoreJob : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Basic { public long A, B; public uint Limit; public UIntPtr C, D; public uint E; public UIntPtr F; public uint G, H; }
    [StructLayout(LayoutKind.Sequential)] private struct Counters { public ulong A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)] private struct Extended { public Basic Basic; public Counters Io; public UIntPtr A, B, C, D; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    private delegate bool ControlHandler(uint ctrl);
    private static readonly ControlHandler Handler = _ => true;
    [DllImport("kernel32.dll")] private static extern bool SetConsoleCtrlHandler(ControlHandler handler, bool add);
    [DllImport("kernel32.dll")] private static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
    private IntPtr handle;
    public CoreJob(Process core)
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) throw new Win32Exception();
        var info = new Extended { Basic = new Basic { Limit = 0x2000 } };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<Extended>());
        try
        {
            Marshal.StructureToPtr(info, memory, false);
            if (!SetInformationJobObject(handle, 9, memory, (uint)Marshal.SizeOf<Extended>()))
            {
                var code = Marshal.GetLastPInvokeError();
                Dispose();
                throw new Win32Exception(code, "配置内核进程监护失败。");
            }
            if (!AssignProcessToJobObject(handle, core.Handle))
            {
                var code = Marshal.GetLastPInvokeError();
                Dispose();
                throw new Win32Exception(code, "关联内核进程监护失败。");
            }
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public static async Task StopAsync(Process core)
    {
        if (core.HasExited) return;
        if (AttachConsole((uint)core.Id))
        {
            try { SetConsoleCtrlHandler(Handler, true); GenerateConsoleCtrlEvent(0, 0); await Task.Delay(250); }
            finally { FreeConsole(); }
            using var timeout = new CancellationTokenSource(4000);
            try { await core.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { }
        }
        if (!core.HasExited) { core.Kill(true); await core.WaitForExitAsync(); }
    }
    public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
}
