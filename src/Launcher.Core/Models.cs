using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Launcher.Core;

public sealed class ProxyNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新节点";
    public string GroupId { get; set; } = "manual";
    public string OutboundJson { get; set; } = "{}";
    [JsonIgnore] public string Protocol => JsonNode.Parse(OutboundJson)?["type"]?.GetValue<string>() ?? "unknown";
    [JsonIgnore] public string Server => JsonNode.Parse(OutboundJson)?["server"]?.GetValue<string>() ?? "";
    [JsonIgnore] public string Display => $"{Name} · {Protocol.ToUpperInvariant()}";
    [JsonIgnore]
    public string Fingerprint
    {
        get
        {
            var node = JsonNode.Parse(OutboundJson)!.AsObject();
            node.Remove("tag");
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(node).ToJsonString())));
        }
    }
    private static JsonNode Canonical(JsonNode value) => value switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x =>
            new KeyValuePair<string, JsonNode?>(x.Key, x.Value is null ? null : Canonical(x.Value)))),
        JsonArray array => new JsonArray(array.Select(x => x is null ? null : Canonical(x)).ToArray()),
        _ => value.DeepClone()
    };
}

public sealed class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "订阅";
    public string Url { get; set; } = "";
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? Usage { get; set; }
    public override string ToString() => Name;
}

public sealed class LauncherSettings
{
    public List<ProxyNode> Nodes { get; set; } = [];
    public List<Subscription> Subscriptions { get; set; } = [];
    public string? SelectedNodeId { get; set; }
    public List<string> RecentProjects { get; set; } = [];
    public string ProjectDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public int ProxyPort { get; set; } = 17890;
    public bool TunEnabled { get; set; }
    public bool StrictChecks { get; set; }
    public bool DisableIPv6 { get; set; }
    public bool MatchTimeZone { get; set; }
    public string ProcessLanguage { get; set; } = "en-US";
    public string CheckBrowser { get; set; } = "auto";
    public string ClaudePath { get; set; } = "";
    public string LaunchTarget { get; set; } = "desktop";
    public string DesktopPath { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
}

public sealed class ImportResult
{
    public List<ProxyNode> Nodes { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class SubscriptionMerge
{
    // The caller downloads and validates first. A failed update cannot delete old nodes.
    public static void Replace(LauncherSettings settings, Subscription subscription, ImportResult parsed)
    {
        if (parsed.Nodes.Count == 0) throw new InvalidOperationException("订阅没有可用节点，原节点已保留。");
        var old = settings.Nodes.Where(x => x.GroupId == subscription.Id).ToList();
        var selected = old.FirstOrDefault(x => x.Id == settings.SelectedNodeId);
        var additions = parsed.Nodes.DistinctBy(x => x.Fingerprint).ToList();
        foreach (var node in additions)
        {
            node.GroupId = subscription.Id;
            var matching = old.FirstOrDefault(x => x.Fingerprint == node.Fingerprint);
            if (matching is not null) node.Id = matching.Id;
        }
        settings.Nodes.RemoveAll(x => x.GroupId == subscription.Id);
        settings.Nodes.AddRange(additions);
        if (selected is not null && !additions.Any(x => x.Id == selected.Id))
            settings.SelectedNodeId = additions.First().Id;
        subscription.UpdatedAt = DateTimeOffset.Now;
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
}
