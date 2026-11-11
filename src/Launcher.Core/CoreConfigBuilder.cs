using System.Text.Json.Nodes;

namespace Launcher.Core;

public static class CoreConfigBuilder
{
    public static JsonObject Build(ProxyNode node, int mixedPort, bool tun, bool routeEnabled = true)
    {
        if (mixedPort is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(mixedPort), "本地端口应为 1024—65535。");
        var proxy = JsonNode.Parse(node.OutboundJson)!.AsObject();
        NodeImporter.Validate(proxy);
        proxy["tag"] = "proxy";
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
            ["outbounds"] = new JsonArray(proxy, new JsonObject { ["type"] = "direct", ["tag"] = "direct" }),
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
}
