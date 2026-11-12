using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Launcher.Core;

public enum LatencyState { Pending, Running, Success, Timeout, Failed, Cancelled, Invalid }
public sealed record NodeLatencyResult(string Fingerprint, LatencyState State, int? Milliseconds = null)
{
    public DateTimeOffset MeasuredAt { get; init; } = DateTimeOffset.Now;
    public bool HasDelay => State == LatencyState.Success && Milliseconds is > 0;
    public string Text => HasDelay ? $"{Milliseconds} ms" : State switch
    {
        LatencyState.Running => "测速中", LatencyState.Timeout => "超时", LatencyState.Failed => "失败",
        LatencyState.Cancelled => "已取消", LatencyState.Invalid => "节点失效", _ => "未测速"
    };
    public string Tone => State is LatencyState.Failed or LatencyState.Invalid ? "Error" : State == LatencyState.Timeout ? "Warning"
        : HasDelay ? Milliseconds <= 300 ? "Success" : Milliseconds <= 1000 ? "Warning" : "Error" : "Pending";
}

public sealed record LatencyPlan(JsonObject Config, Dictionary<string, string> Tags, Dictionary<string, string> Fingerprints, List<string> InvalidNodeIds);
public static class NodeLatency
{
    public const string Target = "https://www.gstatic.com/generate_204";
    public const int TimeoutMs = 3000;
    public const int Parallelism = 12;
    public static string Fingerprint(ProxyNode node, IReadOnlyList<ProxyNode> nodes)
    {
        try { return Hash(string.Join("|", ProxyChain.Resolve(node, nodes).Select(x => x.Id + ":" + x.Fingerprint))); }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException) { return "invalid"; }
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static LatencyPlan Build(IReadOnlyList<ProxyNode> selected, IReadOnlyList<ProxyNode> nodes, int port, string secret)
    {
        if (port is < 1024 or > 65535 || secret.Length < 32 || secret.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new ArgumentException("测速参数无效。");
        var tags = new Dictionary<string,string>(); var fingerprints = new Dictionary<string,string>(); var invalid = new List<string>(); var outbounds = new JsonArray();
        var routes = new Dictionary<string,string>();
        foreach (var node in selected.DistinctBy(x => x.Id))
        {
            fingerprints[node.Id] = Fingerprint(node,nodes);
            try
            {
                var hops = ProxyChain.Resolve(node,nodes); var entries = new List<JsonObject>(); var tag = "n-" + Hash(node.Id);
                for (var i=0; i<hops.Count; i++)
                {
                    var outbound = JsonNode.Parse(hops[i].OutboundJson)!.AsObject(); NodeImporter.Validate(outbound);
                    outbound.Remove("detour"); outbound["tag"] = i == hops.Count-1 ? tag : "e-" + Hash(node.Id);
                    if (i>0) outbound["detour"] = "e-" + Hash(node.Id);
                    entries.Add(outbound);
                }
                // Aliases of the same complete route share one probe; identity/cache keys remain per node.
                var route = Hash(string.Join("|",hops.Select(x=>x.Fingerprint)));
                if(routes.TryGetValue(route,out var sharedTag)) { tags.Add(node.Id,sharedTag); continue; }
                routes.Add(route,tag);
                foreach (var entry in entries) outbounds.Add(entry);
                tags.Add(node.Id,tag);
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException) { invalid.Add(node.Id); }
        }
        outbounds.Add(new JsonObject { ["type"]="direct", ["tag"]="unused-direct" });
        // No inbound or TUN: only authenticated loopback API requests can initiate a measurement.
        var config = new JsonObject
        {
            ["log"]=new JsonObject { ["disabled"]=true }, ["inbounds"]=new JsonArray(), ["outbounds"]=outbounds,
            ["dns"]=new JsonObject { ["servers"]=new JsonArray(new JsonObject { ["type"]="local", ["tag"]="bootstrap" }) },
            ["route"]=new JsonObject { ["auto_detect_interface"]=true, ["default_domain_resolver"]=new JsonObject { ["server"]="bootstrap", ["strategy"]="ipv4_only" }, ["final"]="unused-direct" },
            ["experimental"]=new JsonObject { ["clash_api"]=new JsonObject { ["external_controller"]=$"127.0.0.1:{port}", ["secret"]=secret } }
        };
        return new(config,tags,fingerprints,invalid);
    }
    // Unknown and failed results stay at the bottom in both directions. LINQ ordering preserves ties.
    public static IEnumerable<T> Sort<T>(IEnumerable<T> items, Func<T,NodeLatencyResult?> result, int direction) => direction == 0 ? items :
        items.OrderBy(x => result(x)?.HasDelay == true ? 0 : 1).ThenBy(x => result(x)?.HasDelay == true ? result(x)!.Milliseconds!.Value * (direction == 2 ? -1 : 1) : 0);
}
