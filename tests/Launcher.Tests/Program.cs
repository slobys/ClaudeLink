using System.Text;
using System.Text.Json.Nodes;
using Launcher.Core;

var checks = new List<string>();
var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/test-configs");
Directory.CreateDirectory(output);
void Assert(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks.Add(name); Console.WriteLine("PASS " + name); }
ProxyNode One(string text)
{
    var result = NodeImporter.Parse(text);
    if (result.Nodes.Count != 1) throw new Exception("Expected one node: " + string.Join(";", result.Warnings));
    return result.Nodes[0];
}
const string uuid = "11111111-1111-4111-8111-111111111111";
var defaults = new LauncherSettings();
Assert(defaults.TunEnabled && defaults.StrictChecks && defaults.DisableIPv6 && defaults.MatchTimeZone && defaults.ProcessLanguage == "en-US" && defaults.CheckBrowser == "auto", "Fresh install uses requested network, language and Chrome-first presets");
var previous = System.Text.Json.JsonSerializer.Deserialize<LauncherSettings>("{\"TunEnabled\":false,\"StrictChecks\":false,\"DisableIPv6\":false,\"MatchTimeZone\":false,\"ProcessLanguage\":\"zh-CN\",\"CheckBrowser\":\"edge\"}")!;
Assert(!previous.TunEnabled && !previous.StrictChecks && !previous.DisableIPv6 && !previous.MatchTimeZone && previous.ProcessLanguage == "zh-CN" && previous.CheckBrowser == "edge", "Existing explicit preferences survive the new default presets");
Assert(defaults.Nodes.Count == 0 && defaults.Subscriptions.Count == 0 && defaults.ClaudePath == "" && defaults.DesktopPath == "", "Preset contains no personal nodes, subscriptions or executable paths");
var vless = One($"vless://{uuid}@example.com:443?security=tls&sni=example.com&type=ws&path=%2Fproxy&host=cdn.example.com#测试节点");
var vlessData = JsonNode.Parse(vless.OutboundJson)!;
Assert(vless.Name == "测试节点" && vlessData["transport"]!["path"]!.ToString() == "/proxy", "VLESS TLS WebSocket percent decoding");
var reality = One($"vless://{uuid}@[2001:db8::1]:443?security=reality&sni=example.com&pbk=Nu51xZ9zBRAVrn6nlivxR9CfFOjtHvSCi2BJMVjJXUE&sid=abcd&flow=xtls-rprx-vision#Reality");
Assert(reality.Server == "2001:db8::1" && JsonNode.Parse(reality.OutboundJson)!["tls"]!["utls"]!["fingerprint"]!.ToString() == "chrome", "IPv6 server and Reality fingerprint");
var vmess = One("vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes($$$"""{"v":"2","ps":"VMess","add":"example.com","port":"443","id":"{{{uuid}}}","aid":"0","scy":"auto","net":"grpc","tls":"tls","sni":"example.com","path":"rpc"}""")));
Assert(JsonNode.Parse(vmess.OutboundJson)!["transport"]!["service_name"]!.ToString() == "rpc", "VMess Base64 JSON gRPC");
var ss = One("ss://" + Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-128-gcm:p@ss:word")) + "@127.0.0.1:8388#SS");
Assert(JsonNode.Parse(ss.OutboundJson)!["password"]!.ToString() == "p@ss:word", "Shadowsocks SIP002 passwords preserve colon and at");
var legacySS = One("ss://" + Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-128-gcm:password@example.com:8388")) + "#old");
Assert(legacySS.Server == "example.com", "Shadowsocks legacy whole-authority Base64");
var trojan = One("trojan://p%40ss%3Aword@example.com:443?sni=example.com#Trojan");
Assert(JsonNode.Parse(trojan.OutboundJson)!["password"]!.ToString() == "p@ss:word", "Trojan escaped password");
var hy = One("hy2://secret@example.com:443?sni=example.com&obfs=salamander&obfs-password=test#HY2");
Assert(hy.Protocol == "hysteria2", "Hysteria2 alias and obfuscation");
var tuic = One($"tuic://{uuid}:secret@example.com:443?alpn=h3&congestion_control=bbr#TUIC");
Assert(tuic.Protocol == "tuic", "TUIC UUID and password");
var socks = One("socks5://user:secret@127.0.0.1:1080#Local");
Assert(socks.Protocol == "socks", "SOCKS5 authenticated endpoint");
var http = One("https://user:secret@example.com:443#HTTP");
Assert(JsonNode.Parse(http.OutboundJson)!["tls"]!["enabled"]!.GetValue<bool>(), "HTTPS proxy TLS");
var yaml = $$$"""
proxies:
  - name: YAML Reality
    type: vless
    server: example.com
    port: 443
    uuid: {{{uuid}}}
    tls: true
    servername: example.com
    reality-opts: {public-key: Nu51xZ9zBRAVrn6nlivxR9CfFOjtHvSCi2BJMVjJXUE, short-id: abcd}
    network: ws
    ws-opts: {path: /ws, headers: {Host: cdn.example.com}}
""";
var yamlNode = One(yaml);
Assert(JsonNode.Parse(yamlNode.OutboundJson)!["transport"]!["headers"]!["Host"]!.ToString() == "cdn.example.com", "Clash YAML nested Reality and WebSocket");
var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"vless://{uuid}@example.com:443?security=tls\n" + "trojan://secret@example.com:443"));
Assert(NodeImporter.Parse(base64.TrimEnd('=').Replace('+', '-').Replace('/', '_')).Nodes.Count == 2, "URL-safe unpadded Base64 subscription");
var mixed = NodeImporter.Parse($"ssr://unsupported\nvless://{uuid}@example.com:443?type=xhttp\nvless://{uuid}@example.com:443?security=tls\n");
Assert(mixed.Nodes.Count == 1 && mixed.Warnings.Count == 2 && !string.Join("", mixed.Warnings).Contains(uuid), "Unsupported protocols reported without echoing secrets");
Assert(NodeImporter.Parse("ss://YWVzLTEyOC1nY206c2VjcmV0@example.com:8388?plugin=obfs-local").Nodes.Count == 0, "Unsupported SS plugin rejected");
Assert(NodeImporter.Parse($$$"""{"type":"vless","server":"example.com","server_port":443,"uuid":"{{{uuid}}}","tls":{"enabled":true,"certificate_path":"C:/private"}}""").Nodes.Count == 0, "Imported TLS file paths rejected");
var canonical = JsonNode.Parse(vless.OutboundJson)!.AsObject();
var reversed = new JsonObject(canonical.Reverse().Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value?.DeepClone())));
Assert(vless.Fingerprint == new ProxyNode { OutboundJson = reversed.ToJsonString() }.Fingerprint, "Node identity independent of JSON key ordering");
var subscription = new Subscription { Name = "Test", Url = "https://example.com/private-token" };
vless.GroupId = subscription.Id;
var settings = new LauncherSettings { Nodes = [vless, socks], Subscriptions = [subscription], SelectedNodeId = vless.Id };
var failed = false;
try { SubscriptionMerge.Replace(settings, subscription, new ImportResult()); } catch (InvalidOperationException) { failed = true; }
Assert(failed && settings.Nodes.Count == 2 && settings.SelectedNodeId == vless.Id, "Failed subscription update preserves old nodes and selection");
var update = NodeImporter.Parse(vless.OutboundJson);
SubscriptionMerge.Replace(settings, subscription, update);
Assert(settings.SelectedNodeId == vless.Id && settings.Nodes.Any(x => x.Id == socks.Id), "Subscription replacement preserves stable selection and other groups");
var sizeRejected = false;
try { NodeImporter.Parse(new string('A', NodeImporter.MaxBytes + 1)); } catch (FormatException) { sizeRejected = true; }
Assert(sizeRejected, "Import size bounded");
var list = new[] { vless, reality, vmess, ss, legacySS, trojan, hy, tuic, socks, http, yamlNode };
for (var i = 0; i < list.Length; i++)
{
    var config = CoreConfigBuilder.Build(list[i], 17890, false);
    File.WriteAllText(Path.Combine(output, $"node-{i:00}.json"), config.ToJsonString(JsonDefaults.Pretty));
    Assert(config["inbounds"]![0]!["listen"]!.ToString() == "127.0.0.1", $"Generated {list[i].Protocol} is loopback-only");
}
File.WriteAllText(Path.Combine(output, "tun.json"), CoreConfigBuilder.Build(vless, 17890, true).ToJsonString(JsonDefaults.Pretty));
Assert(CoreConfigBuilder.Build(vless, 17890, true)["inbounds"]!.AsArray().Count == 2, "TUN config adds virtual adapter without system proxy");
var chainNodes = new List<ProxyNode> { vless, http, socks, hy };
vless.Name="香港入口"; http.Name="美国出口";
var chain = ProxyChain.Create("住宅出口", vless.Id, http.Id, chainNodes);
var chainConfig = CoreConfigBuilder.Build(chain, 17890, false, nodes:chainNodes);
Assert(chain.IsChain && chainConfig["outbounds"]![0]!["tag"]!.ToString() == "chain-entry" && chainConfig["outbounds"]![1]!["tag"]!.ToString() == "proxy" && chainConfig["outbounds"]![1]!["detour"]!.ToString() == "chain-entry", "Chain order is local to entry to final exit; final exit dials through entry");
Assert(JsonNode.DeepEquals(chainConfig,CoreConfigBuilder.RebuildTrusted(chainConfig,17890)), "Privileged host rebuild preserves only the validated chain topology");
Assert(JsonNode.Parse(vless.OutboundJson)!["detour"] == null && JsonNode.Parse(http.OutboundJson)!["tag"] == null, "Building a chain never mutates its source nodes");
void Reject(Action action,string label) { var rejected=false;try{action();}catch(FormatException){rejected=true;} Assert(rejected,label); }
Reject(()=>ProxyChain.Create("重复",vless.Id,vless.Id,chainNodes),"Chain rejects identical entry and exit");
Reject(()=>ProxyChain.Create("丢失",vless.Id,"missing",chainNodes),"Chain rejects missing hop references");
Reject(()=>ProxyChain.Create("UDP出口",vless.Id,hy.Id,chainNodes),"Unsupported UDP-only exit is rejected before any environment mutation");
chainNodes.Add(chain);
Reject(()=>ProxyChain.Create("嵌套",chain.Id,http.Id,chainNodes),"Chain nesting cannot create cycles or silently change routing");
var hostile=chainConfig.DeepClone().AsObject();hostile["outbounds"]![1]!["detour"]="proxy";
Reject(()=>CoreConfigBuilder.RebuildTrusted(hostile,17890),"Host rejects reversed or cyclic detours");
var persisted=System.Text.Json.JsonSerializer.Deserialize<ProxyNode>(System.Text.Json.JsonSerializer.Serialize(chain))!;
Assert(persisted.ChainNodeIds.SequenceEqual(chain.ChainNodeIds),"Chain IDs survive JSON settings roundtrip");
var oldNode=System.Text.Json.JsonSerializer.Deserialize<ProxyNode>("{\"Id\":\"legacy\",\"Name\":\"旧节点\",\"GroupId\":\"manual\",\"OutboundJson\":\"{}\"}")!;
Assert(!oldNode.IsChain && oldNode.ChainNodeIds.Count == 0,"Old node data loads without the new optional chain fields");
Assert(NodeRegion.FromName("香港624 中继")?.Code == "HK" && NodeRegion.FromName("20260929-us1")?.Code == "US" && NodeRegion.FromName("🇯🇵 东京")?.Code == "JP" && NodeRegion.FromName("business") == null,"Region hints recognize Chinese, short codes and flags without guessing unknown names");
Assert(NodeRegion.ForNode(chain,chainNodes)?.Code == "US" && NodeRegion.FromName(vless.Name)?.Code == "HK","Chain region follows the exit rather than the entry label");
var chainSettings=new LauncherSettings {Nodes=[vless,http,chain],SelectedNodeId=chain.Id};
SubscriptionMerge.Replace(chainSettings,subscription,NodeImporter.Parse(vless.OutboundJson));
Assert(chainSettings.SelectedNodeId == chain.Id && !ProxyChain.IsMissing(chain,chainSettings.Nodes),"Subscription refresh preserves stable hop references and chain selection");
SubscriptionMerge.Replace(chainSettings,subscription,NodeImporter.Parse("http://127.0.0.1:18081#Changed"));
Assert(chainSettings.SelectedNodeId == chain.Id && ProxyChain.IsMissing(chain,chainSettings.Nodes),"Removed subscription hop marks chain invalid without substituting an exit");
Reject(()=>CoreConfigBuilder.Build(chain,17890,false,nodes:chainSettings.Nodes),"Invalid chain cannot fall back to a single node");
File.WriteAllText(Path.Combine(output,"chain-http.json"),chainConfig.ToJsonString(JsonDefaults.Pretty));
File.WriteAllText(Path.Combine(output,"chain-tun.json"),CoreConfigBuilder.Build(chain,17890,true,nodes:chainNodes).ToJsonString(JsonDefaults.Pretty));
File.WriteAllText(Path.Combine(output,"chain-socks.json"),CoreConfigBuilder.Build(ProxyChain.Create("SOCKS出口",hy.Id,socks.Id,chainNodes),17890,false,nodes:chainNodes).ToJsonString(JsonDefaults.Pretty));
var latencyPlan=NodeLatency.Build(chainNodes,chainNodes,19876,new string('a',64));
Assert(latencyPlan.Config["inbounds"]!.AsArray().Count == 0 && latencyPlan.Config["experimental"]!["clash_api"]!["external_controller"]!.ToString() == "127.0.0.1:19876", "Latency core exposes only an authenticated loopback controller and no traffic or TUN inbound");
Assert(latencyPlan.Tags.Count == chainNodes.Count && latencyPlan.Config["outbounds"]!.AsArray().Single(x=>x?["tag"]?.ToString()==latencyPlan.Tags[chain.Id])!["detour"] != null,"Latency measurement preserves chain entry-to-exit direction");
var brokenLatency=new ProxyNode { ChainNodeIds=["missing","missing-too"] };
var invalidPlan=NodeLatency.Build([brokenLatency,vless],[vless],19876,new string('b',64));
Assert(invalidPlan.InvalidNodeIds.SequenceEqual(new[]{brokenLatency.Id}) && invalidPlan.Tags.ContainsKey(vless.Id),"An invalid chain cannot break a batch or silently fall back to direct");
var fingerprintBefore=NodeLatency.Fingerprint(chain,chainNodes); var oldOutbound=chainNodes[0].OutboundJson;
chainNodes[0].OutboundJson=oldOutbound.Replace("example.com","changed.example.com");
Assert(NodeLatency.Fingerprint(chain,chainNodes)!=fingerprintBefore,"Changing a chain hop invalidates cached latency");chainNodes[0].OutboundJson=oldOutbound;
var delaySamples=new[]{ new NodeLatencyResult("x",LatencyState.Failed),new("x",LatencyState.Success,400),new("x",LatencyState.Pending),new("x",LatencyState.Success,80),new("x",LatencyState.Timeout),new("x",LatencyState.Success,80)};
Assert(NodeLatency.Sort(delaySamples,x=>x,1).Take(3).Select(x=>x.Milliseconds).SequenceEqual(new int?[]{80,80,400}) && NodeLatency.Sort(delaySamples,x=>x,2).Take(3).Select(x=>x.Milliseconds).SequenceEqual(new int?[]{400,80,80}),"Both delay sort directions keep measured values first with stable ties");
Assert(NodeLatency.Sort(delaySamples,x=>x,2).Skip(3).Select(x=>x.State).SequenceEqual(new[]{LatencyState.Failed,LatencyState.Pending,LatencyState.Timeout}) && NodeLatency.Sort(delaySamples,x=>x,0).SequenceEqual(delaySamples),"Untested and failed nodes remain last; original ordering can be restored");
File.WriteAllText(Path.Combine(output,"latency.json"),latencyPlan.Config.ToJsonString(JsonDefaults.Pretty));
var latencyAlias=One(vless.OutboundJson);
var aliasPlan=NodeLatency.Build([vless,latencyAlias],[vless,latencyAlias],19876,new string('c',64));
Assert(aliasPlan.Tags[vless.Id]==aliasPlan.Tags[latencyAlias.Id] && aliasPlan.Config["outbounds"]!.AsArray().Count==2 && aliasPlan.Fingerprints[vless.Id]!=aliasPlan.Fingerprints[latencyAlias.Id],"Equivalent routes share a probe without sharing node identity or cache validity");
Assert(NodeLatency.TimeoutMs==3000 && NodeLatency.Parallelism==12,"Fast latency profile uses bounded 12-way concurrency and a three second timeout");
File.WriteAllText(Path.Combine(output, "results.json"), System.Text.Json.JsonSerializer.Serialize(new { Passed = checks.Count, Checks = checks }, JsonDefaults.Pretty));
Console.WriteLine($"All {checks.Count} regression assertions passed.");
