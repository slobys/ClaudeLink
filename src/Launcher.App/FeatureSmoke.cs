using Launcher.App.Services;

namespace Launcher.App;

public static class FeatureSmoke
{
    public static async Task RunAsync(Action<bool, string> assert, string outputPath)
    {
        var old = JsonSerializer.Deserialize<LauncherSettings>("{\"ClaudePath\":\"old-cli.exe\"}")!;
        assert(old.LaunchTarget == "desktop" && old.ClaudePath == "old-cli.exe" && old.AutoCheckUpdates, "Existing preferences migrate to desktop default without losing CLI path");
        assert(ClaudeDesktop.IsGuiExecutable(Environment.ProcessPath!), "Real WPF EXE is recognized as a desktop GUI executable");
        var directory = Path.Combine(AppPaths.Root, "feature-tests"); Directory.CreateDirectory(directory);
        var console = Path.Combine(directory, "console.exe");
        var header = new byte[256]; header[0] = 0x4d; header[1] = 0x5a; header[0x3c] = 0x40; header[0x40] = 0x50; header[0x41] = 0x45; header[0x40 + 24 + 68] = 3;
        File.WriteAllBytes(console, header);
        var rejected = false;
        try { await ClaudeDesktop.FindAsync(console); } catch (IOException) { rejected = true; }
        assert(rejected, "Console subsystem CLI cannot be mistaken for desktop client");
        var project = Path.Combine(directory, "桌面 项目[1]&"); Directory.CreateDirectory(project);
        var link = ClaudeDesktop.CodeLink(project);
        assert(Uri.UnescapeDataString(new Uri(link).Query["?folder=".Length..]) == Path.GetFullPath(project), "Desktop Code deep link preserves Unicode, spaces, brackets and ampersand folder");
        var store = ClaudeDesktop.StartInfo(new("unused.exe", true), project);
        var native = ClaudeDesktop.StartInfo(new(Environment.ProcessPath!, false), project);
        assert(store.UseShellExecute && store.FileName == link && native.FileName == Environment.ProcessPath! && native.ArgumentList.Single() == link,
            "Desktop launch uses official URI or GUI executable, never a terminal wrapper");
        var installed = await ClaudeDesktop.FindAsync();
        assert(installed == null || ClaudeDesktop.IsGuiExecutable(installed.Executable), "Registered desktop detection returns a GUI installation when present");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var payload = RandomNumberGenerator.GetBytes(8192); payload[0] = 0x4d; payload[1] = 0x5a;
        var manifest = new UpdateManifest { Schema = 1, Repository = ReleaseSettings.Repository, Version = "99.0.1", AssetName = "ClaudeLink-Setup-99.0.1.exe", Size = payload.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var signature = Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var fixture = new UpdateFixture();
        using var client = new HttpClient(fixture);
        var api = $"https://api.github.com/repos/{ReleaseSettings.Repository}/releases/latest";
        byte[] Metadata(string? replacement = null) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            draft = false, prerelease = false, tag_name = "v99.0.1", assets = new[] { "update.json", "update.json.sig", manifest.AssetName }
                .Select(name => new { name, browser_download_url = replacement != null && name == "update.json" ? replacement : ReleaseSettings.AssetUri(manifest.Version, name).AbsoluteUri })
        });
        fixture.Files[api] = Metadata();
        fixture.Files[ReleaseSettings.AssetUri(manifest.Version, "update.json").AbsoluteUri] = bytes;
        fixture.Files[ReleaseSettings.AssetUri(manifest.Version, "update.json.sig").AbsoluteUri] = Encoding.UTF8.GetBytes(signature);
        fixture.Files[ReleaseSettings.AssetUri(manifest.Version, manifest.AssetName).AbsoluteUri] = payload;
        var offer = await ReleaseUpdater.CheckAsync(client, current: new Version(1, 0, 0), publicKey: publicKey);
        assert(offer?.Manifest.Version == "99.0.1", "Newer stable GitHub release requires a valid ECDSA-signed manifest");
        assert(await ReleaseUpdater.CheckAsync(client, current: new Version(99, 0, 1), publicKey: publicKey) == null
            && await ReleaseUpdater.CheckAsync(client, current: new Version(100, 0, 0), publicKey: publicKey) == null, "Equal and older releases never offer a downgrade");
        fixture.Files[ReleaseSettings.AssetUri(manifest.Version, "update.json.sig").AbsoluteUri] = Encoding.UTF8.GetBytes(Convert.ToBase64String(new byte[64]));
        rejected = false;
        try { await ReleaseUpdater.CheckAsync(client, current: new Version(1, 0, 0), publicKey: publicKey); } catch (CryptographicException) { rejected = true; }
        assert(rejected, "Tampered release signature is rejected before installer download");
        fixture.Files[ReleaseSettings.AssetUri(manifest.Version, "update.json.sig").AbsoluteUri] = Encoding.UTF8.GetBytes(signature);
        fixture.Files[api] = Metadata("https://example.invalid/forged-update.json");
        rejected = false;
        try { await ReleaseUpdater.CheckAsync(client, current: new Version(1, 0, 0), publicKey: publicKey); } catch (IOException) { rejected = true; }
        assert(rejected, "Update metadata cannot redirect manifest trust to another repository or host");
        fixture.Files[api] = Metadata();
        var changedManifest = JsonSerializer.SerializeToUtf8Bytes(new UpdateManifest { Schema = 1, Repository = "other/repo", Version = manifest.Version, AssetName = manifest.AssetName, Size = manifest.Size, Sha256 = manifest.Sha256 });
        rejected = false;
        try { ReleaseUpdater.VerifyManifest(changedManifest, Convert.ToBase64String(key.SignData(changedManifest, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)), new Version(manifest.Version), publicKey); } catch (IOException) { rejected = true; }
        assert(rejected, "Even a signed manifest must match this product and version");

        var destination = await ReleaseUpdater.DownloadAsync(client, offer!, Path.Combine(directory, "download"));
        assert(await ReleaseUpdater.IsInstallerValidAsync(destination, manifest), "Downloaded installer size and SHA-256 match signed release manifest");
        var requestsBeforeCache = fixture.Requests;
        assert(await ReleaseUpdater.DownloadAsync(client, offer!, Path.Combine(directory, "download")) == destination && fixture.Requests == requestsBeforeCache,
            "A fully verified cached installer can be reused without downloading again");
        var corrupt = (byte[])payload.Clone(); corrupt[100] ^= 1;
        fixture.Files[offer!.DownloadUri.AbsoluteUri] = corrupt;
        var corruptDirectory = Path.Combine(directory, "corrupt-download");
        rejected = false;
        try { await ReleaseUpdater.DownloadAsync(client, offer, corruptDirectory); } catch (CryptographicException) { rejected = true; }
        assert(rejected && Directory.GetFiles(corruptDirectory).Length == 0, "Corrupt download is never installed and its partial file is removed");
        fixture.DelayInstaller = true;
        var cancelled = false; var cancelledDirectory = Path.Combine(directory, "cancelled-download");
        using (var cancellation = new CancellationTokenSource(60))
        {
            try { await ReleaseUpdater.DownloadAsync(client, offer, cancelledDirectory, cancellation: cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        }
        assert(cancelled && Directory.GetFiles(cancelledDirectory).Length == 0, "User can cancel update download without leaving an executable or partial file");
        var installInfo = ReleaseUpdater.InstallerStartInfo(destination);
        assert(!installInfo.UseShellExecute && installInfo.ArgumentList[0] == "/update" && installInfo.ArgumentList[1] == Environment.ProcessId.ToString()
            && installInfo.ArgumentList.Last() == "/restart", "Self-update installer waits for the owning launcher and requests restart without shell interpolation");
        AppPaths.WriteJson(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "desktop-update-results.json"), new
        {
            DesktopDetected = installed != null, PackagedDesktop = installed?.Packaged,
            TerminalWrapperUsed = false, SignedUpdateVerified = true, CorruptAndCancelledDownloadsRejected = true
        });
    }

    private sealed class UpdateFixture : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public bool DelayInstaller { get; set; }
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (DelayInstaller && request.RequestUri!.AbsolutePath.EndsWith(".exe", StringComparison.Ordinal)) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Files.TryGetValue(request.RequestUri!.AbsoluteUri, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
