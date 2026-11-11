using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Launcher.Core;

public static class NodeImporter
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public const int MaxNodes = 5000;
    private static readonly HashSet<string> Protocols =
        ["vless", "vmess", "trojan", "shadowsocks", "hysteria2", "tuic", "socks", "http", "anytls"];

    public static ImportResult Parse(string text, string groupId = "manual")
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new FormatException("导入内容超过 8 MB。");
        text = text.Trim().TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("请粘贴节点分享链接或订阅内容。");
        var result = new ImportResult();
        if (text.StartsWith('{') || text.StartsWith('['))
        {
            var root = JsonNode.Parse(text) ?? throw new FormatException("JSON 内容为空。");
            IEnumerable<JsonNode?> nodes = root switch
            {
                JsonArray array => array,
                JsonObject obj when obj["outbounds"] is JsonArray array => array,
                JsonObject => [root],
                _ => throw new FormatException("无法识别的节点 JSON。")
            };
            foreach (var item in nodes) TryAdd(result, () => FromOutbound(item!.AsObject()), groupId);
        }
        else if (text.Contains("proxies:", StringComparison.Ordinal) || text.StartsWith("proxies:"))
        {
            var stream = new YamlStream();
            using var reader = new StringReader(text);
            stream.Load(reader);
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
                throw new FormatException("Clash 订阅必须包含一个配置文档。");
            if (Get(root, "proxies") is not YamlSequenceNode proxies)
                throw new FormatException("订阅没有 proxies 节点列表；仅 proxy-providers 的配置暂不支持。");
            foreach (var item in proxies.Children)
                TryAdd(result, () => FromClash(item as YamlMappingNode ?? throw new FormatException("节点不是对象。")), groupId);
        }
        else
        {
            if (!text.Contains("://", StringComparison.Ordinal))
            {
                try { text = DecodeBase64(text); }
                catch (FormatException) { throw new FormatException("无法识别内容。支持分享链接、Base64、Clash YAML 和 sing-box JSON。"); }
            }
            foreach (var line in text.Replace("\r", "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith('#')) continue;
                TryAdd(result, () => FromLink(line), groupId);
            }
        }
        var unique = result.Nodes.DistinctBy(x => x.Fingerprint).ToList();
        result.Nodes.Clear();
        result.Nodes.AddRange(unique);
        return result;
    }

    private static void TryAdd(ImportResult result, Func<ProxyNode> factory, string groupId)
    {
        if (result.Nodes.Count + result.Warnings.Count >= MaxNodes) throw new FormatException("节点数量超过 5000。");
        try
        {
            var node = factory();
            Validate(JsonNode.Parse(node.OutboundJson)!.AsObject());
            node.Name = new string(node.Name.Where(c => !char.IsControl(c)).Take(160).ToArray());
            if (string.IsNullOrWhiteSpace(node.Name)) node.Name = $"{node.Protocol} 节点";
            node.GroupId = groupId;
            result.Nodes.Add(node);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException or OverflowException)
        {
            // Never echo URI, JSON, password, subscription URL, or generic parser exceptions.
            var detail = ex is NodeFormatException ? ex.Message : "链接或字段格式不正确";
            result.Warnings.Add($"第 {result.Nodes.Count + result.Warnings.Count + 1} 项：{detail}。");
        }
    }

    public static string DecodeBase64(string text)
    {
        text = string.Concat(text.Where(c => !char.IsWhiteSpace(c))).Replace('-', '+').Replace('_', '/');
        text = text.PadRight((text.Length + 3) / 4 * 4, '=');
        return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(text));
    }

    private static ProxyNode FromLink(string link)
    {
        var schemeIndex = link.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex < 1) throw new NodeFormatException("缺少协议名称");
        var scheme = link[..schemeIndex].ToLowerInvariant();
        if (scheme == "vmess") return FromVmess(link[8..]);
        if (scheme == "ss") return FromShadowsocks(link);
        if (scheme is "ssr" or "wireguard" or "hysteria")
            throw new NodeFormatException("此协议暂不支持");
        if (scheme is not ("vless" or "trojan" or "hysteria2" or "hy2" or "tuic" or "socks" or "socks5" or "http" or "https" or "anytls"))
            throw new NodeFormatException("此协议暂不支持");
        var uri = new Uri(link);
        var q = ParseQuery(uri.Query);
        var protocol = scheme switch { "hy2" => "hysteria2", "socks5" => "socks", "https" => "http", _ => scheme };
        var port = uri.Port > 0 ? uri.Port : scheme == "https" ? 443 : scheme == "http" ? 80 : -1;
        var o = Base(protocol, uri.Host.Trim('[', ']'), port);
        var userInfo = Uri.UnescapeDataString(uri.UserInfo);
        switch (protocol)
        {
            case "vless":
                o["uuid"] = userInfo;
                if (q.GetValueOrDefault("encryption", "none") != "none")
                    throw new NodeFormatException("此 VLESS 加密扩展尚不支持");
                if (q.TryGetValue("flow", out var flow) && flow.Length > 0) o["flow"] = flow;
                if (q.TryGetValue("packetEncoding", out var packet)) o["packet_encoding"] = packet;
                break;
            case "trojan":
            case "anytls":
                o["password"] = userInfo;
                break;
            case "hysteria2":
                if (q.ContainsKey("mport")) throw new NodeFormatException("Hysteria2 多端口跳跃暂不支持");
                o["password"] = userInfo;
                if (q.GetValueOrDefault("obfs") is { Length: > 0 } obfs)
                    o["obfs"] = new JsonObject { ["type"] = obfs, ["password"] = q.GetValueOrDefault("obfs-password", "") };
                break;
            case "tuic":
                var tuic = userInfo.Split(':', 2);
                if (tuic.Length != 2) throw new NodeFormatException("TUIC 需要 UUID 和密码");
                o["uuid"] = tuic[0]; o["password"] = tuic[1];
                o["congestion_control"] = q.GetValueOrDefault("congestion_control", "cubic");
                o["udp_relay_mode"] = q.GetValueOrDefault("udp_relay_mode", "native");
                o["zero_rtt_handshake"] = Truth(q.GetValueOrDefault("reduce_rtt", q.GetValueOrDefault("zero_rtt_handshake", "")));
                break;
            case "http":
            case "socks":
                if (userInfo.Length > 0)
                {
                    var auth = userInfo.Split(':', 2);
                    o["username"] = auth[0]; o["password"] = auth.ElementAtOrDefault(1) ?? "";
                }
                if (protocol == "socks") o["version"] = "5";
                break;
        }
        var defaultSecurity = protocol is "trojan" or "hysteria2" or "tuic" or "anytls" || scheme == "https" ? "tls" : "none";
        AddTls(o, q, q.GetValueOrDefault("security", defaultSecurity));
        if (protocol is "vless" or "trojan" or "vmess") AddTransport(o, q);
        return Make(o, Uri.UnescapeDataString(uri.Fragment.TrimStart('#')));
    }

    private static ProxyNode FromVmess(string payload)
    {
        var o = JsonNode.Parse(DecodeBase64(payload.Split('#')[0]))!.AsObject();
        string S(string k, string d = "") => o[k]?.ToString() ?? d;
        var output = Base("vmess", S("add"), int.Parse(S("port"), CultureInfo.InvariantCulture));
        output["uuid"] = S("id");
        output["security"] = S("scy", "auto");
        output["alter_id"] = int.TryParse(S("aid"), out var aid) ? aid : 0;
        if (S("type", "none") is not ("none" or "" or "gun"))
            throw new NodeFormatException("VMess 的 TCP 伪装暂不支持");
        var q = new Dictionary<string, string>
        {
            ["type"] = S("net", "tcp"), ["path"] = S("path", "/"), ["host"] = S("host"),
            ["sni"] = S("sni", S("host")), ["alpn"] = S("alpn"), ["fp"] = S("fp"),
            ["serviceName"] = S("path"), ["allowInsecure"] = S("allowInsecure")
        };
        AddTls(output, q, S("tls") == "tls" ? "tls" : "none");
        AddTransport(output, q);
        return Make(output, S("ps"));
    }

    private static ProxyNode FromShadowsocks(string link)
    {
        var raw = link[5..];
        var fragment = "";
        var fragmentAt = raw.IndexOf('#');
        if (fragmentAt >= 0) { fragment = Uri.UnescapeDataString(raw[(fragmentAt + 1)..]); raw = raw[..fragmentAt]; }
        var queryAt = raw.IndexOf('?');
        var query = queryAt >= 0 ? ParseQuery(raw[(queryAt + 1)..]) : [];
        if (query.GetValueOrDefault("plugin") is { Length: > 0 }) throw new NodeFormatException("Shadowsocks 插件暂不支持");
        if (queryAt >= 0) raw = raw[..queryAt];
        if (!raw.Contains('@')) raw = DecodeBase64(raw);
        var at = raw.LastIndexOf('@');
        if (at < 1) throw new NodeFormatException("Shadowsocks 缺少凭据");
        var auth = Uri.UnescapeDataString(raw[..at]);
        if (!auth.Contains(':')) auth = DecodeBase64(auth);
        var credentials = auth.Split(':', 2);
        if (credentials.Length != 2) throw new NodeFormatException("Shadowsocks 缺少密码");
        var address = new Uri("ss://" + raw[(at + 1)..].TrimEnd('/'));
        var o = Base("shadowsocks", address.Host.Trim('[', ']'), address.Port);
        o["method"] = credentials[0]; o["password"] = credentials[1];
        return Make(o, fragment);
    }

    private static ProxyNode FromOutbound(JsonObject input)
    {
        var o = input.DeepClone().AsObject();
        var name = o["tag"]?.ToString() ?? "";
        o.Remove("tag");
        Validate(o);
        return Make(o, name);
    }

    private static ProxyNode FromClash(YamlMappingNode m)
    {
        string S(string k, string d = "") => Scalar(Get(m, k)) ?? d;
        var protocol = S("type") switch { "ss" => "shadowsocks", "hy2" => "hysteria2", "socks5" => "socks", var p => p };
        var o = Base(protocol, S("server"), int.Parse(S("port"), CultureInfo.InvariantCulture));
        foreach (var pair in new[] { ("uuid", "uuid"), ("password", "password"), ("username", "username"), ("cipher", "method"), ("flow", "flow") })
            if (S(pair.Item1).Length > 0) o[pair.Item2] = S(pair.Item1);
        if (protocol == "vmess")
        {
            o["security"] = S("cipher", "auto"); o.Remove("method");
            o["alter_id"] = int.TryParse(S("alterId", "0"), out var aid) ? aid : 0;
        }
        if (S("plugin").Length > 0) throw new NodeFormatException("Shadowsocks 插件暂不支持");
        if (protocol == "socks") o["version"] = "5";
        if (protocol == "hysteria2" && S("ports").Length > 0) throw new NodeFormatException("Hysteria2 多端口跳跃暂不支持");
        if (protocol == "hysteria2" && S("obfs").Length > 0)
            o["obfs"] = new JsonObject { ["type"] = S("obfs"), ["password"] = S("obfs-password") };
        if (protocol == "tuic")
        {
            o["congestion_control"] = S("congestion-controller", "cubic");
            o["udp_relay_mode"] = S("udp-relay-mode", "native");
            o["zero_rtt_handshake"] = Truth(S("reduce-rtt"));
        }
        var reality = Get(m, "reality-opts") as YamlMappingNode;
        var q = new Dictionary<string, string>
        {
            ["sni"] = S("servername", S("sni")), ["fp"] = S("client-fingerprint"),
            ["insecure"] = S("skip-cert-verify"), ["type"] = S("network", "tcp"),
            ["pbk"] = Scalar(reality is null ? null : Get(reality, "public-key")) ?? "",
            ["sid"] = Scalar(reality is null ? null : Get(reality, "short-id")) ?? ""
        };
        if (Get(m, "alpn") is YamlSequenceNode alpn) q["alpn"] = string.Join(',', alpn.Children.Select(x => Scalar(x)));
        var security = reality is not null ? "reality" : Truth(S("tls")) || protocol is "trojan" or "tuic" or "hysteria2" or "anytls" ? "tls" : "none";
        AddTls(o, q, security);
        if (Get(m, "ws-opts") is YamlMappingNode ws)
        {
            q["path"] = Scalar(Get(ws, "path")) ?? "/";
            if (Get(ws, "headers") is YamlMappingNode headers)
            {
                foreach (var header in headers.Children)
                    if (Scalar(header.Key)?.Equals("host", StringComparison.OrdinalIgnoreCase) == true)
                        q["host"] = Scalar(header.Value) ?? "";
            }
            if (Scalar(Get(ws, "max-early-data")) is { } early) q["ed"] = early;
            if (Scalar(Get(ws, "early-data-header-name")) is { } earlyHeader) q["eh"] = earlyHeader;
        }
        if (Get(m, "grpc-opts") is YamlMappingNode grpc) q["serviceName"] = Scalar(Get(grpc, "grpc-service-name")) ?? "";
        if (Get(m, "h2-opts") is YamlMappingNode h2)
        {
            q["path"] = Scalar(Get(h2, "path")) ?? "/";
            if (Get(h2, "host") is YamlSequenceNode host) q["host"] = string.Join(',', host.Children.Select(Scalar));
        }
        if (protocol is "vless" or "vmess" or "trojan") AddTransport(o, q);
        if (o["transport"] is JsonObject builtTransport && builtTransport["type"]?.ToString() == "ws" &&
            Get(m, "ws-opts") is YamlMappingNode wsOptions && Get(wsOptions, "headers") is YamlMappingNode wsHeaders)
        {
            var headers = new JsonObject();
            foreach (var header in wsHeaders.Children)
            {
                if (Scalar(header.Key) is not { Length: > 0 } key || Scalar(header.Value) is not { } value)
                    throw new NodeFormatException("WebSocket 请求头格式暂不支持");
                headers[key] = value;
            }
            builtTransport["headers"] = headers;
        }
        return Make(o, S("name"));
    }

    private static void AddTls(JsonObject o, Dictionary<string, string> q, string security)
    {
        if (security is "none" or "") return;
        if (security is not ("tls" or "reality")) throw new NodeFormatException("不支持此 TLS 安全类型");
        var tls = new JsonObject { ["enabled"] = true };
        var sni = q.GetValueOrDefault("sni", q.GetValueOrDefault("peer", ""));
        if (!string.IsNullOrEmpty(sni)) tls["server_name"] = sni;
        if (Truth(q.GetValueOrDefault("insecure", q.GetValueOrDefault("allowInsecure", "")))) tls["insecure"] = true;
        if (q.GetValueOrDefault("alpn") is { Length: > 0 } alpn)
            tls["alpn"] = new JsonArray(alpn.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        var fp = q.GetValueOrDefault("fp", "");
        if (fp.Length > 0 || security == "reality") tls["utls"] = new JsonObject { ["enabled"] = true, ["fingerprint"] = fp.Length > 0 ? fp : "chrome" };
        if (security == "reality")
        {
            if (q.GetValueOrDefault("pbk") is not { Length: > 0 } key) throw new NodeFormatException("Reality 缺少公钥");
            tls["reality"] = new JsonObject { ["enabled"] = true, ["public_key"] = key, ["short_id"] = q.GetValueOrDefault("sid", "") };
        }
        o["tls"] = tls;
    }

    private static void AddTransport(JsonObject o, Dictionary<string, string> q)
    {
        var type = q.GetValueOrDefault("type", "tcp").ToLowerInvariant();
        if (type is "tcp" or "none" or "") return;
        var transport = new JsonObject();
        switch (type)
        {
            case "ws":
            case "httpupgrade":
                transport["type"] = type;
                transport["path"] = q.GetValueOrDefault("path", "/");
                if (q.GetValueOrDefault("host") is { Length: > 0 } host)
                {
                    if (type == "ws") transport["headers"] = new JsonObject { ["Host"] = host };
                    else transport["host"] = host;
                }
                if (type == "ws" && int.TryParse(q.GetValueOrDefault("ed"), out var early))
                {
                    transport["max_early_data"] = early;
                    transport["early_data_header_name"] = q.GetValueOrDefault("eh", "Sec-WebSocket-Protocol");
                }
                break;
            case "grpc":
                transport["type"] = "grpc";
                transport["service_name"] = q.GetValueOrDefault("serviceName", "");
                break;
            case "h2":
            case "http":
                transport["type"] = "http";
                transport["path"] = q.GetValueOrDefault("path", "/");
                if (q.GetValueOrDefault("host") is { Length: > 0 } hosts)
                    transport["host"] = new JsonArray(hosts.Split(',').Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
                break;
            default: throw new NodeFormatException("此传输方式暂不支持（例如 XHTTP/KCP/QUIC）");
        }
        o["transport"] = transport;
    }

    private static readonly HashSet<string> AllowedRoot =
        ["type", "server", "server_port", "uuid", "password", "username", "method", "flow", "security", "alter_id", "tls",
         "transport", "version", "packet_encoding", "network", "obfs", "up_mbps", "down_mbps", "congestion_control",
         "udp_relay_mode", "zero_rtt_handshake", "heartbeat", "idle_session_check_interval", "idle_session_timeout", "min_idle_session"];

    public static void Validate(JsonObject o)
    {
        if (o["type"]?.ToString() is not { } type || !Protocols.Contains(type))
            throw new NodeFormatException("协议暂不支持，或该条目不是代理节点");
        if (string.IsNullOrWhiteSpace(o["server"]?.ToString())) throw new NodeFormatException("缺少服务器地址");
        if (!int.TryParse(o["server_port"]?.ToString(), out var port) || port is < 1 or > 65535)
            throw new NodeFormatException("服务器端口应为 1—65535");
        foreach (var field in o)
            if (!AllowedRoot.Contains(field.Key)) throw new NodeFormatException("包含尚未支持的节点参数");
        if (type is "vless" or "vmess" or "tuic" && !Guid.TryParse(o["uuid"]?.ToString(), out _))
            throw new NodeFormatException("UUID 格式不正确");
        if (type is "trojan" or "shadowsocks" or "hysteria2" or "tuic" or "anytls" && string.IsNullOrEmpty(o["password"]?.ToString()))
            throw new NodeFormatException("缺少节点密码");
        if (type == "shadowsocks" && string.IsNullOrEmpty(o["method"]?.ToString())) throw new NodeFormatException("缺少加密方式");
        if (o["tls"] is JsonObject tls)
        {
            CheckKeys(tls, ["enabled", "server_name", "insecure", "alpn", "utls", "reality"]);
            if (tls["utls"] is JsonObject utls) CheckKeys(utls, ["enabled", "fingerprint"]);
            if (tls["reality"] is JsonObject reality) CheckKeys(reality, ["enabled", "public_key", "short_id"]);
        }
        if (o["transport"] is JsonObject transport)
            CheckKeys(transport, ["type", "path", "host", "headers", "service_name", "max_early_data", "early_data_header_name", "method", "idle_timeout", "ping_timeout"]);
        if (o["obfs"] is JsonObject obfs) CheckKeys(obfs, ["type", "password"]);
    }

    private static void CheckKeys(JsonObject value, HashSet<string> allowed)
    {
        if (value.Any(x => !allowed.Contains(x.Key))) throw new NodeFormatException("包含尚未支持的嵌套节点参数");
    }
    private static ProxyNode Make(JsonObject o, string name) => new() { Name = string.IsNullOrWhiteSpace(name) ? $"{o["type"]} · {o["server"]}" : name, OutboundJson = o.ToJsonString() };
    private static JsonObject Base(string type, string server, int port) => new() { ["type"] = type, ["server"] = server, ["server_port"] = port };
    private static YamlNode? Get(YamlMappingNode m, string key) => m.Children.TryGetValue(new YamlScalarNode(key), out var n) ? n : null;
    private static string? Scalar(YamlNode? n) => n is YamlScalarNode s ? s.Value : null;
    private static bool Truth(string? s) => s is "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            result[Uri.UnescapeDataString(pieces[0])] = Uri.UnescapeDataString(pieces.ElementAtOrDefault(1) ?? "");
        }
        return result;
    }

    private sealed class NodeFormatException(string message) : FormatException(message);
}
