using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Launcher.Core;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
try
{
    var input = await Console.In.ReadToEndAsync();
    if (Encoding.UTF8.GetByteCount(input) > 16 * 1024 * 1024) throw new FormatException("请求内容过大。");
    var request = JsonNode.Parse(input) ?? throw new FormatException("请求为空。");
    object result = request["operation"]?.ToString() switch
    {
        "parse" => Parse(request),
        "config" => Config(request),
        "merge" => Merge(request),
        "latency-config" => NodeLatency.Build(request["selected"]!.Deserialize<List<ProxyNode>>()!, request["nodes"]!.Deserialize<List<ProxyNode>>()!, request["port"]!.GetValue<int>(), request["secret"]!.ToString()),
        _ => throw new FormatException("不支持的操作。")
    };
    Console.Write(JsonSerializer.Serialize(result, JsonDefaults.Pretty));
    return 0;
}
catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or ArgumentException or IOException)
{
    Console.Write(JsonSerializer.Serialize(new { Error = ex is FormatException ? ex.Message : "节点或配置无效，请检查格式。" }));
    return 1;
}

static object Parse(JsonNode request)
{
    var result = NodeImporter.Parse(request["text"]?.ToString() ?? "");
    return new { Nodes = result.Nodes, Warnings = result.Warnings, Traffic = SubscriptionTraffic.Parse(request["usageHeader"]?.ToString()) };
}
static object Config(JsonNode request)
{
    var node = request["node"]?.Deserialize<ProxyNode>() ?? throw new FormatException("请选择节点。");
    var port = request["port"]?.GetValue<int>() ?? 17890;
    var tun = request["tun"]?.GetValue<bool>() ?? false;
    var config = CoreConfigBuilder.Build(node, port, tun, request["routeEnabled"]?.GetValue<bool>() ?? true, request["nodes"]?.Deserialize<List<ProxyNode>>());
    if (request["platform"]?.ToString() == "macos" && tun)
    {
        var inbound = config["inbounds"]!.AsArray().First(x => x?["type"]?.ToString() == "tun")!.AsObject();
        inbound.Remove("interface_name"); // Darwin allocates a free utun interface.
    }
    return new { Config = config };
}
static object Merge(JsonNode request)
{
    var settings = request["settings"]?.Deserialize<LauncherSettings>() ?? throw new FormatException("配置为空。");
    var subscription = request["subscription"]?.Deserialize<Subscription>() ?? throw new FormatException("订阅为空。");
    var parsed = new ImportResult();
    parsed.Nodes.AddRange(request["nodes"]?.Deserialize<List<ProxyNode>>() ?? []);
    SubscriptionMerge.Replace(settings, subscription, parsed);
    return new { Nodes = settings.Nodes, SelectedNodeId = settings.SelectedNodeId };
}
