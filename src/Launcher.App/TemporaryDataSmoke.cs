using Launcher.App.Services;

namespace Launcher.App;

public static partial class IntegrationSmoke
{
    private static async Task CheckTemporaryCleanupAsync(Action<bool, string> assert)
    {
        var root = AppPaths.Root;
        var sessionId = Guid.NewGuid().ToString("N");
        var session = AppPaths.Session(sessionId);
        var journals = Path.Combine(session, "environment.json");
        AppPaths.WriteJson(journals, new EnvironmentJournal { Restored = true });
        var parent = Path.Combine(session, "browser-checks");

        var stale = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "synthetic-profile"), "temporary");
        Age(stale);

        var browserId = Guid.NewGuid().ToString("N");
        var owned = Path.Combine(parent, browserId);
        var profile = Path.Combine(owned, "profile");
        CheckBrowser.PrepareProfile(profile, "en-US");
        var info = CheckBrowser.StartInfo(CheckBrowser.Select("auto"), profile, "en-US", null, ["about:blank"]);
        info.CreateNoWindow = true;
        info.WindowStyle = ProcessWindowStyle.Hidden;
        info.ArgumentList.Add("--headless=new");
        info.ArgumentList.Add("--remote-debugging-port=0");
        using var browser = Process.Start(info) ?? throw new IOException("Could not start disposable browser fixture.");
        try
        {
            for (var attempt = 0; attempt < 50 && !File.Exists(Path.Combine(profile, "DevToolsActivePort")); attempt++)
                await Task.Delay(100);
            assert(File.Exists(Path.Combine(profile, "DevToolsActivePort")), "Disposable browser fixture started");
            CheckBrowser.StartProfileWatcher(browser, sessionId, browserId, owned);
            Age(owned);
            var scopedTemp = Path.Combine(root, "fixture-temp");
            var staleTest = Path.Combine(scopedTemp, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staleTest);
            File.WriteAllText(Path.Combine(staleTest, ".active"), "");
            Age(staleTest);
            var activeTest = Path.Combine(scopedTemp, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(activeTest);
            using var lease = File.Open(Path.Combine(activeTest, ".active"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            Age(activeTest);

            var removed = await TemporaryData.SweepAsync(root, scopedTemp);
            assert(removed == 2 && !Directory.Exists(stale) && !Directory.Exists(staleTest) && Directory.Exists(activeTest),
                "Startup cleanup removes stale fixtures and respects an active test lease");
            assert(Directory.Exists(owned) && File.Exists(journals) && File.Exists(Path.Combine(root, "settings.dat")),
                "An open browser profile, saved settings and network recovery records survive cleanup");
        }
        finally
        {
            if (!browser.HasExited) browser.Kill(entireProcessTree: true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await browser.WaitForExitAsync(deadline.Token);
        }
        for (var attempt = 0; attempt < 90 && Directory.Exists(owned); attempt++) await Task.Delay(200);
        assert(!Directory.Exists(owned), "Detached watcher removes only the isolated browser profile after process exit");
    }

    private static void Age(string directory)
    {
        var old = DateTime.UtcNow.AddHours(-2);
        Directory.SetCreationTimeUtc(directory, old);
        Directory.SetLastWriteTimeUtc(directory, old);
    }
}
