using System.Text.Json.Nodes;

namespace Launcher.Core;

public static class CoreConfigBuilder
{
    public static JsonObject Build(ProxyNode node, int mixedPort, bool tun, bool routeEnabled = true, IReadOnlyList<ProxyNode>? nodes = null)
    {
        if (mixedPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(mixedPort), "本地端口应为 1024—65535。");
        var hops = ProxyChain.Resolve(node, nodes);
        var outbounds = new JsonArray();
        for (var index = 0; index < hops.Count; index++)
        {
            var proxy = JsonNode.Parse(hops[index].OutboundJson)!.AsObject();
            NodeImporter.Validate(proxy);
            proxy.Remove("detour");
            proxy["tag"] = index == hops.Count - 1 ? "proxy" : "chain-entry";
            if (index > 0) proxy["detour"] = "chain-entry";
            outbounds.Add(proxy);
        }
        outbounds.Add(new JsonObject { ["type"] = "direct", ["tag"] = "direct" });
        // Resolve server hostnames without recursively sending the bootstrap query through this proxy.
        var inbounds = new JsonArray
        {
            new JsonObject { ["type"] = "mixed", ["tag"] = "mixed-in", ["listen"] = "127.0.0.1", ["listen_port"] = mixedPort }
        };
        if (tun) inbounds.Add(new JsonObject
        {
            ["type"] = "tun", ["tag"] = "tun-in", ["interface_name"] = "ClaudeLink",
            ["address"] = new JsonArray("172.29.255.1/30", "fdfe:dcba:9876::1/126"),
            ["mtu"] = 1400, ["auto_route"] = routeEnabled, ["strict_route"] = routeEnabled,
            ["stack"] = "mixed"
        });
        return new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "warn", ["timestamp"] = true },
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject { ["type"] = "local", ["tag"] = "bootstrap" },
                    new JsonObject { ["type"] = "https", ["tag"] = "remote", ["server"] = "1.1.1.1", ["path"] = "/dns-query", ["detour"] = "proxy" }
                },
                ["final"] = "remote", ["strategy"] = "ipv4_only"
            },
            ["inbounds"] = inbounds,
            ["outbounds"] = outbounds,
            ["route"] = new JsonObject
            {
                ["auto_detect_interface"] = true,
                ["default_domain_resolver"] = new JsonObject { ["server"] = "bootstrap", ["strategy"] = "ipv4_only" },
                ["rules"] = new JsonArray
                {
                    new JsonObject { ["action"] = "sniff" },
                    new JsonObject { ["protocol"] = "dns", ["action"] = "hijack-dns" },
                    new JsonObject { ["ip_is_private"] = true, ["action"] = "route", ["outbound"] = "direct" }
                },
                ["final"] = "proxy"
            }
        };
    }

    public static JsonObject RebuildTrusted(JsonObject input, int port)
    {
        if (input["outbounds"] is not JsonArray values || values.Count is < 2 or > 3) throw new FormatException("无效内核配置。");
        var hops = new List<ProxyNode>();
        var proxies = values.Where(x => x?["tag"]?.ToString() != "direct").ToList();
        if (proxies.Count != values.Count - 1) throw new FormatException("无效内核配置。");
        for (var index = 0; index < proxies.Count; index++)
        {
            var value = proxies[index]!.DeepClone().AsObject();
            if (value["tag"]?.ToString() != (index == proxies.Count - 1 ? "proxy" : "chain-entry")
                || value["detour"]?.ToString() != (index == 0 ? null : "chain-entry")) throw new FormatException("链式代理方向或引用无效。");
            value.Remove("tag"); value.Remove("detour"); NodeImporter.Validate(value);
            hops.Add(new ProxyNode { OutboundJson = value.ToJsonString() });
        }
        if (hops.Count == 0) throw new FormatException("缺少代理节点。");
        var node = hops.Count == 1 ? hops[0] : ProxyChain.Create("链式代理", hops[0].Id, hops[1].Id, hops);
        var tun = input["inbounds"]!.AsArray().Any(x => x?["type"]?.ToString() == "tun");
        return Build(node, port, tun, nodes: hops);
    }
}
