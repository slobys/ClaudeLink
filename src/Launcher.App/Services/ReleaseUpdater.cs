using System.Reflection;
using System.Text.RegularExpressions;

namespace Launcher.App.Services;

public static class ReleaseSettings
{
    public const string Repository = "slobys/ClaudeLink";
    public const string PublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEgsfjXnQIGksYc3n9Vgdpk7vQFFU66Tj5ggxikImNkvlwyGhQVnD9Isg0hTYRtks0kedbezwfOfZUBg7zo9X2ug==";
    public static Version CurrentVersion
    {
        get { var v = Assembly.GetExecutingAssembly().GetName().Version!; return new(v.Major, v.Minor, Math.Max(0, v.Build)); }
    }
    public static Uri ProjectPage => new("https://github.com/" + Repository);
    public static Uri LatestAssetUri(string name) => new($"https://github.com/{Repository}/releases/latest/download/{Uri.EscapeDataString(name)}");
    public static Uri AssetUri(string version, string name) => new($"https://github.com/{Repository}/releases/download/v{version}/{Uri.EscapeDataString(name)}");
}

public sealed class UpdateManifest
{
    public int Schema { get; set; }
    public string Repository { get; set; } = "";
    public string Version { get; set; } = "";
    public string AssetName { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}
public sealed record UpdateOffer(UpdateManifest Manifest, Uri DownloadUri, Uri ReleasePage);

public static class ReleaseUpdater
{
    public static HttpClient CreateClient(int? proxyPort = null)
    {
        var handler = new SocketsHttpHandler { UseProxy = true, ConnectTimeout = TimeSpan.FromSeconds(8), AutomaticDecompression = DecompressionMethods.All };
        if (proxyPort is { } port) handler.Proxy = new WebProxy($"http://127.0.0.1:{port}");
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeLink/" + ReleaseSettings.CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<UpdateOffer?> CheckAsync(HttpClient client, CancellationToken cancellation = default,
        Version? current = null, string publicKey = ReleaseSettings.PublicKey)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            // Public release downloads do not consume the anonymous REST API quota.
            var payload = await ReadBoundedAsync(client, ReleaseSettings.LatestAssetUri("update.json"), 16384, budget.Token);
            var candidate = JsonSerializer.Deserialize<UpdateManifest>(payload) ?? throw new JsonException();
            if (!TryVersion(candidate.Version, out var version)) throw new IOException("更新版本号无效。");
            // Pin the signature to this version so a concurrent new release cannot mix two manifests.
            var signature = await ReadBoundedAsync(client, ReleaseSettings.AssetUri(candidate.Version, "update.json.sig"), 1024, budget.Token);
            var manifest = VerifyManifest(payload, Encoding.UTF8.GetString(signature).Trim(), version, publicKey);
            if (version <= (current ?? ReleaseSettings.CurrentVersion)) return null;
            return new(manifest, ReleaseSettings.AssetUri(manifest.Version, manifest.AssetName),
                new Uri($"https://github.com/{ReleaseSettings.Repository}/releases/tag/v{manifest.Version}"));
        }
        catch (HttpRequestException)
        {
            // A transport failure can use the API route. Invalid signatures and cancellation never fall back.
            return await CheckViaApiAsync(client, current, publicKey, budget.Token);
        }
    }

    private static async Task<UpdateOffer?> CheckViaApiAsync(HttpClient client, Version? current, string publicKey, CancellationToken cancellation)
    {
        var metadata = await ReadBoundedAsync(client, new Uri($"https://api.github.com/repos/{ReleaseSettings.Repository}/releases/latest"), 256 * 1024, cancellation);
        var release = JsonNode.Parse(metadata) ?? throw new IOException("更新版本信息无效。");
        if (release["draft"]?.GetValue<bool>() == true || release["prerelease"]?.GetValue<bool>() == true) return null;
        var tag = release["tag_name"]?.ToString() ?? "";
        if (!tag.StartsWith('v') || !TryVersion(tag[1..], out var version)) throw new IOException("更新版本号无效。");
        if (version <= (current ?? ReleaseSettings.CurrentVersion)) return null;
        var assets = release["assets"]?.AsArray() ?? throw new IOException("此版本尚未提供安装包。");
        Uri Asset(string name)
        {
            var item = assets.FirstOrDefault(x => x?["name"]?.ToString() == name);
            var expected = ReleaseSettings.AssetUri(version.ToString(), name);
            if (!Uri.TryCreate(item?["browser_download_url"]?.ToString(), UriKind.Absolute, out var actual) || actual.AbsoluteUri != expected.AbsoluteUri)
                throw new IOException("更新附件来源不匹配，已停止。");
            return expected;
        }
        var manifest = await ReadBoundedAsync(client, Asset("update.json"), 16384, cancellation);
        var signature = await ReadBoundedAsync(client, Asset("update.json.sig"), 1024, cancellation);
        var parsed = VerifyManifest(manifest, Encoding.UTF8.GetString(signature).Trim(), version, publicKey);
        var download = Asset(parsed.AssetName);
        return new(parsed, download, new Uri($"https://github.com/{ReleaseSettings.Repository}/releases/tag/v{parsed.Version}"));
    }

    internal static bool TryVersion(string value, out Version version)
    {
        version = new(0, 0, 0);
        return value is { Length: < 32 } && Regex.IsMatch(value, @"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)
            && Version.TryParse(value, out version!);
    }

    internal static UpdateManifest VerifyManifest(byte[] payload, string signature, Version expected, string publicKey)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            if (!key.VerifyData(payload, Convert.FromBase64String(signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new CryptographicException("更新签名校验失败。");
            var value = JsonSerializer.Deserialize<UpdateManifest>(payload) ?? throw new JsonException();
            if (value.Schema != 1 || value.Repository != ReleaseSettings.Repository || !TryVersion(value.Version, out var version)
                || version != expected || value.AssetName != $"ClaudeLink-Setup-{version}.exe" || value.Size is < 2 or > 536870912
                || !Regex.IsMatch(value.Sha256, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
                throw new IOException("更新清单与版本不匹配，已停止。");
            return value;
        }
        catch (FormatException) { throw new CryptographicException("更新签名格式无效。"); }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpClient client, Uri uri, int maximum, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximum) throw new IOException("更新信息超过允许大小。");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var result = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellation)) > 0)
        {
            if (result.Length + count > maximum) throw new IOException("更新信息超过允许大小。");
            result.Write(buffer, 0, count);
        }
        return result.ToArray();
    }

    public static async Task<string> DownloadAsync(HttpClient client, UpdateOffer offer, string directory,
        IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        if (!TryVersion(offer.Manifest.Version, out _) || offer.Manifest.AssetName != $"ClaudeLink-Setup-{offer.Manifest.Version}.exe"
            || offer.DownloadUri != ReleaseSettings.AssetUri(offer.Manifest.Version, offer.Manifest.AssetName)) throw new IOException("安装包来源无效。");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, offer.Manifest.AssetName);
        if (await IsInstallerValidAsync(destination, offer.Manifest, cancellation)) { progress?.Report(100); return destination; }
        var partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            using var response = await client.GetAsync(offer.DownloadUri, HttpCompletionOption.ResponseHeadersRead, budget.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } size && size != offer.Manifest.Size) throw new IOException("安装包大小与签名清单不一致。");
            await using (var input = await response.Content.ReadAsStreamAsync(budget.Token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[65536];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, budget.Token)) > 0)
                {
                    total += count;
                    if (total > offer.Manifest.Size) throw new IOException("安装包超过签名清单大小。");
                    await output.WriteAsync(buffer.AsMemory(0, count), budget.Token);
                    hash.AppendData(buffer, 0, count);
                    progress?.Report(100.0 * total / offer.Manifest.Size);
                }
                if (total != offer.Manifest.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(offer.Manifest.Sha256)))
                    throw new CryptographicException("安装包完整性校验失败，不会执行。");
            }
            File.Move(partial, destination, true);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    public static async Task<bool> IsInstallerValidAsync(string path, UpdateManifest manifest, CancellationToken cancellation = default)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != manifest.Size) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        return CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(stream, cancellation), Convert.FromHexString(manifest.Sha256));
    }

    public static ProcessStartInfo InstallerStartInfo(string path)
    {
        var info = ProcessTools.Hidden(path, "/update", Environment.ProcessId.ToString(), Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(), "/restart");
        info.WindowStyle = ProcessWindowStyle.Normal;
        return info;
    }
}
