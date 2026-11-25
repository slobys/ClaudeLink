namespace Launcher.App.Services;

internal static class TemporaryData
{
    internal static string TestParent => Path.Combine(Path.GetTempPath(), "ClaudeLink-tests");

    internal sealed class TestWorkspace : IDisposable
    {
        private readonly string originalRoot = AppPaths.Root;
        private readonly string? originalId = Environment.GetEnvironmentVariable("CLAUDELINK_TEST_ID");
        private readonly FileStream lease;
        internal string DirectoryPath { get; }

        internal TestWorkspace()
        {
            var id = Guid.NewGuid().ToString("N");
            DirectoryPath = Path.Combine(TestParent, id);
            Directory.CreateDirectory(DirectoryPath);
            lease = new FileStream(Path.Combine(DirectoryPath, ".active"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            AppPaths.Root = DirectoryPath;
            Environment.SetEnvironmentVariable("CLAUDELINK_TEST_ID", id);
        }

        public void Dispose()
        {
            AppPaths.Root = originalRoot;
            Environment.SetEnvironmentVariable("CLAUDELINK_TEST_ID", originalId);
            lease.Dispose();
            TryRemoveTree(TestParent, Path.GetFileName(DirectoryPath));
        }
    }

    internal static bool TryRemoveTree(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name is "." or "..") return false;
        var root = Path.GetFullPath(parent);
        var directory = Path.GetFullPath(Path.Combine(root, name));
        if (!string.Equals(Path.GetDirectoryName(directory), root, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (!Directory.Exists(directory)) return true;
            for (var ancestor = new DirectoryInfo(directory); ancestor is not null; ancestor = ancestor.Parent)
                if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) return false;

            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.TryPop(out var path))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                }
            }
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static async Task<int> SweepAsync(string root, string? testParent = null)
    {
        // A single process snapshot protects live Chromium profiles, including legacy folders.
        var commands = await RunningCommandsAsync();
        if (commands is null) return 0;
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var removed = 0;

        void Remove(string parent, DirectoryInfo directory, bool test)
        {
            if (directory.LastWriteTimeUtc >= cutoff || directory.CreationTimeUtc >= cutoff ||
                commands.Any(x => x.Contains(directory.FullName, StringComparison.OrdinalIgnoreCase))) return;
            if (test)
            {
                var leasePath = Path.Combine(directory.FullName, ".active");
                if (File.Exists(leasePath))
                {
                    try { using var lease = File.Open(leasePath, FileMode.Open, FileAccess.Read, FileShare.None); }
                    catch (IOException) { return; }
                    catch (UnauthorizedAccessException) { return; }
                }
                // Older test runs had no lease. Do not clean them while a test is running.
                else if (commands.Any(x => x.Contains("--integration-test", StringComparison.OrdinalIgnoreCase) ||
                                           x.Contains("--smoke-test", StringComparison.OrdinalIgnoreCase) ||
                                           x.Contains("--latency-test", StringComparison.OrdinalIgnoreCase) ||
                                           x.Contains("--verify-release", StringComparison.OrdinalIgnoreCase))) return;
            }
            if (TryRemoveTree(parent, directory.Name)) removed++;
        }

        foreach (var parent in new[] { testParent ?? TestParent, Path.Combine(root, "tests"), Path.Combine(root, "ui-tests") })
            foreach (var run in GuidDirectories(parent)) Remove(parent, run, test: true);

        foreach (var session in GuidDirectories(Path.Combine(root, "sessions")))
        {
            var parent = Path.Combine(session.FullName, "browser-checks");
            foreach (var directory in GuidDirectories(parent)) Remove(parent, directory, test: false);

            // Old versions put the dedicated Chromium profile directly under this name.
            var legacy = new DirectoryInfo(Path.Combine(session.FullName, "browser"));
            if (legacy.Exists && File.Exists(Path.Combine(legacy.FullName, "Local State")))
                Remove(session.FullName, legacy, test: false);
        }
        return removed;
    }

    private static IEnumerable<DirectoryInfo> GuidDirectories(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0) return [];
        return directory.GetDirectories().Where(x => Guid.TryParseExact(x.Name, "N", out _) &&
                                                       (x.Attributes & FileAttributes.ReparsePoint) == 0);
    }

    internal static async Task<string[]?> RunningCommandsAsync()
    {
        try
        {
            var (code, output) = await ProcessTools.PowerShellAsync("$session=(Get-Process -Id $PID).SessionId; " +
                "$processes=@(Get-CimInstance Win32_Process -Filter \"SessionId=$session\" | Where-Object { $_.Name -in 'chrome.exe','msedge.exe','ClaudeNetworkLauncher.exe','sing-box.exe' }); " +
                "if($processes | Where-Object { -not $_.CommandLine -and $_.Name -in 'chrome.exe','msedge.exe' }){exit 1}; " +
                "ConvertTo-Json -InputObject @($processes | Where-Object CommandLine | ForEach-Object CommandLine) -Compress", 15);
            return code == 0 ? JsonSerializer.Deserialize<string[]>(output) : null;
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or TimeoutException or JsonException) { return null; }
    }

    internal static async Task<bool> CleanBrowserAsync(string sessionId, string browserId)
    {
        if (!Guid.TryParseExact(sessionId, "N", out _) || !Guid.TryParseExact(browserId, "N", out _)) return false;
        var parent = Path.Combine(AppPaths.Session(sessionId), "browser-checks");
        var directory = Path.Combine(parent, browserId);
        var commands = await RunningCommandsAsync();
        return commands is not null && !commands.Any(x => x.Contains(directory, StringComparison.OrdinalIgnoreCase)) &&
               TryRemoveTree(parent, browserId);
    }
}
