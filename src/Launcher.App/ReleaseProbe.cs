using Launcher.App.Services;

namespace Launcher.App;

// Explicit release-maintainer command. Downloads and verifies the published package; never executes it or changes networking.
public static class ReleaseProbe
{
    public static async Task<int> RunAsync(string output)
    {
        var testId = Guid.NewGuid().ToString("N");
        AppPaths.Root = Path.Combine(AppPaths.Root, "tests", testId);
        Directory.CreateDirectory(AppPaths.Root);
        new SettingsStore().Save(new LauncherSettings { Subscriptions = [new Subscription { Url = "https://example.invalid/test-only-secret-" + testId }] });
        try
        {
            using var client = ReleaseUpdater.CreateClient();
            var offer = await ReleaseUpdater.CheckAsync(client, current: new Version(0, 0, 0)) ?? throw new IOException("No stable release available.");
            var path = await ReleaseUpdater.DownloadAsync(client, offer, Path.Combine(AppPaths.Root, "published-release"));
            var verified = await ReleaseUpdater.IsInstallerValidAsync(path, offer.Manifest);
            AppPaths.WriteJson(output, new { Verified = verified, Repository = ReleaseSettings.Repository, offer.Manifest.Version, offer.Manifest.Size,
                offer.Manifest.Sha256, SignatureVerified = true, InstallerExecuted = false, NetworkingChanged = false });
            return verified ? 0 : 1;
        }
        catch (Exception ex)
        {
            AppPaths.WriteJson(output, new { Verified = false, ErrorType = ex.GetType().Name });
            return 1;
        }
    }
}
