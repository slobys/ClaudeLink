import AppKit
import CryptoKit
import Foundation
import Darwin

@main struct NativeTests {
    @MainActor static func main() async {
        var checks:[String]=[]
        func check(_ condition:Bool,_ text:String) throws { guard condition else { throw ClientError.message(text) }; checks.append(text); print("PASS "+text) }
        do {
            AppPaths.root=FileManager.default.temporaryDirectory.appendingPathComponent("ClaudeLink-tests-"+UUID().uuidString)
            let defaults=Settings()
            try check(defaults.TunEnabled && defaults.StrictChecks && defaults.DisableIPv6 && defaults.MatchTimeZone && defaults.ProcessLanguage == "en-US" && defaults.CheckBrowser == "auto","Fresh install applies requested network and Chrome-first presets")
            try check(defaults.Nodes.isEmpty && defaults.Subscriptions.isEmpty && defaults.ClaudePath.isEmpty && defaults.DesktopPath.isEmpty,"Preset contains no personal nodes or executable paths")
            let samples=(0..<5).map { ProxyNode(Id:"delay-\($0)",Name:"Delay",GroupId:"manual",OutboundJson:"{\"type\":\"socks\",\"server\":\"127.0.0.1\",\"server_port\":1080}") }
            let ms:[Int?]=[nil,400,80,nil,80]
            let delays=Dictionary(uniqueKeysWithValues:samples.enumerated().map{($0.element.Id,NodeLatencyResult(fingerprint:"fixture",state:ms[$0.offset] == nil ? .failed : .success,milliseconds:ms[$0.offset]))})
            try check(NodeLatency.sort(samples,1,{delays[$0.Id]}).map(\.Id)==["delay-2","delay-4","delay-1","delay-0","delay-3"],"Latency ascending sort preserves ties and keeps missing results last")
            try check(NodeLatency.sort(samples,2,{delays[$0.Id]}).map(\.Id)==["delay-1","delay-2","delay-4","delay-0","delay-3"],"Latency descending sort keeps failed nodes below measured nodes")
            try check(NodeLatency.sort(samples,0,{delays[$0.Id]}).map(\.Id)==samples.map(\.Id),"Latency original ordering is recoverable")
            try check(NodeLatencyResult(fingerprint:"fixture",state:.timeout).text == "超时" && NodeLatencyResult(fingerprint:"fixture",state:.failed).text == "失败" && NodeLatencyResult(fingerprint:"fixture",state:.success,milliseconds:0).hasDelay == false,"Latency failures never become zero millisecond successes")
            let privacyScript=try BrowserPrivacy.script()
            try check(BrowserPrivacy.checkURL == "https://ip.net.coffee/claude/" && privacyScript.contains("ipMaskToggle") && privacyScript.replacingOccurrences(of:"\\/",with:"/").contains("https://ip.net.coffee") && !privacyScript.contains("__CLAUDELINK_ORIGIN__"),"Dedicated browser bundles scoped IP-hiding script and a single requested destination")
            let parsed=try await Bridge.parse("socks5://user:secret@127.0.0.1:1080#Local")
            try check(parsed.Nodes.count == 1,"Shared bridge imports SOCKS credentials")
            let node=parsed.Nodes[0]
            let legacyNode=try JSONDecoder().decode(ProxyNode.self,from:Data("{\"Id\":\"legacy\",\"Name\":\"旧节点\",\"GroupId\":\"manual\",\"OutboundJson\":\"{}\"}".utf8))
            try check(!legacyNode.isChain && legacyNode.ChainNodeIds.isEmpty,"Legacy nodes decode without optional chain fields")
            try check(NodeRegion.fromName("香港624 中继")?.Code == "HK" && NodeRegion.fromName("20260929-us1")?.Code == "US" && NodeRegion.fromName("business") == nil,"Region hints match names and codes without guessing unknown countries")
            var entry=node; entry.Id="chain-entry-test"; entry.Name="香港入口"
            var exitNode=node; exitNode.Id="chain-exit-test"; exitNode.Name="美国出口"
            let chain=try ProxyChain.create("住宅出口链路",entry.Id,exitNode.Id,[entry,exitNode],id:"chain-test")
            try check(chain.isChain && NodeRegion.resolve(chain,[entry,exitNode])?.Code == "US","Chain card region follows its final exit")
            var invalid=false; do { _=try ProxyChain.create("重复",entry.Id,entry.Id,[entry,exitNode]) } catch { invalid=true }
            try check(invalid,"Chain editor model rejects identical entry and exit")
            try check(ProxyChain.invalid(chain,[entry]),"Missing exit remains invalid rather than selecting another node")
            let chainConfig=try await Bridge.config(chain,port:17992,tun:false,route:false,nodes:[entry,exitNode])
            let chainObject=try JSONSerialization.jsonObject(with:chainConfig) as! [String:Any], outbounds=chainObject["outbounds"] as! [[String:Any]]
            try check(outbounds.count == 3 && outbounds[0]["tag"] as? String == "chain-entry" && outbounds[1]["detour"] as? String == "chain-entry","Shared bridge builds entry-to-exit detour for native Mac")
            let chainPath=AppPaths.root.appendingPathComponent("chain.json");try AppPaths.write(chainConfig,to:chainPath)
            let chainCheck=try await Commands.run(Bundle.main.resourceURL!.appendingPathComponent("sing-box").path,["check","-c",chainPath.path]);try check(chainCheck.code == 0,"Official Darwin core accepts two-hop proxy chain")
            let chainRoundtrip=try JSONDecoder().decode(ProxyNode.self,from:JSONEncoder().encode(chain))
            try check(chainRoundtrip.ChainNodeIds == [entry.Id,exitNode.Id],"Native chain IDs survive settings encoding")
            try check(NSImage(contentsOf:Bundle.main.resourceURL!.appendingPathComponent("Flags/us.png")) != nil,"Bundled country flag artwork is readable by native AppKit")
            let subscription=Subscription(Id:UUID().uuidString,Name:"Test",Url:"https://example.invalid/sub")
            var original=Settings(); var subscribed=node; subscribed.GroupId=subscription.Id; original.Nodes=[subscribed]; original.SelectedNodeId=subscribed.Id
            let merged=try await Bridge.merge(original,subscription,parsed.Nodes)
            try check(merged.Nodes.count == 1 && merged.SelectedNodeId == subscribed.Id && merged.Nodes[0].Id == subscribed.Id,"Subscription refresh preserves selected node identity")
            for tun in [false,true] {
                let config=try await Bridge.config(node,port:17991,tun:tun,route:false)
                let value=try JSONSerialization.jsonObject(with:config) as! [String:Any]
                let inbounds=value["inbounds"] as! [[String:Any]]
                try check(!tun || inbounds.first(where:{$0["type"] as? String == "tun"})?["interface_name"] == nil,"Darwin TUN lets OS allocate utun (\(tun))")
                let path=AppPaths.root.appendingPathComponent("config-\(tun).json"); try AppPaths.write(config,to:path)
                let result=try await Commands.run(Bundle.main.resourceURL!.appendingPathComponent("sing-box").path,["check","-c",path.path]); try check(result.code == 0,"Official Darwin core validates generated configuration (\(tun))")
            }
            var settings=Settings(); settings.Nodes=[node]; settings.Subscriptions=[Subscription(Id:UUID().uuidString,Name:"Private",Url:"https://example.invalid/token-private")]
            settings.TunEnabled=false; settings.StrictChecks=false; settings.DisableIPv6=false; settings.MatchTimeZone=false; settings.ProcessLanguage="zh-CN"; settings.CheckBrowser="edge"
            try SecureSettings.save(settings); let loaded=try SecureSettings.load(); try check(loaded.Nodes == settings.Nodes && loaded.Subscriptions == settings.Subscriptions,"Keychain AES-GCM settings roundtrip")
            try check(!loaded.TunEnabled && !loaded.StrictChecks && !loaded.DisableIPv6 && !loaded.MatchTimeZone && loaded.ProcessLanguage == "zh-CN" && loaded.CheckBrowser == "edge","Existing explicit preferences survive new preset defaults")
            let encrypted=try Data(contentsOf:AppPaths.root.appendingPathComponent("settings.dat")); try check(!String(decoding:encrypted,as:UTF8.self).contains("token-private"),"Encrypted config hides subscription tokens")
            var corrupted=encrypted; corrupted[corrupted.count-1] ^= 1; try AppPaths.write(corrupted,to:AppPaths.root.appendingPathComponent("settings.dat"))
            var rejected=false; do { _=try SecureSettings.load() } catch { rejected=true }; try check(rejected,"Corrupted ciphertext is rejected without replacing data")
            var report=NetworkReport(); report.generalIP="198.51.100.24"; report.cloudflareIP=report.generalIP; report.claudeIP=report.generalIP; report.apiCode=401; report.webCode=403; report.country="US"; report.tun=true; report.ipv6Disabled=true; report.zoneMatches=true
            try check(report.basicReady && !report.strictReady,"HTTP 401 means transport reachable; HTTP 403 fails strict webpage check")
            Web.rows(&report); try check(report.rows.contains(where:{$0.label == "Claude 登录页" && $0.level == .warning}),"Webpage warning uses semantic warning state")
            try check(Web.claudeLoginURL == "https://claude.ai/login" && report.rows.contains(where:{$0.label == "Claude 登录页" && $0.detail.contains("不代表浏览器")}),"Requested login URL and browser confirmation guidance are shown independently")
            let hostile=Data("{}".utf8), signature=Data(Data(repeating:0,count:64).base64EncodedString().utf8); rejected=false; do { _=try Updater.verified(hostile,signature) } catch { rejected=true }; try check(rejected,"Update rejects forged signature")
            let fixture=URL(fileURLWithPath:FileManager.default.currentDirectoryPath).appendingPathComponent("tests/fixtures/update-macos-"+BuildInfo.arch+".json")
            let signed=try Data(contentsOf:fixture), validSignature=try Data(contentsOf:URL(fileURLWithPath:fixture.path+".sig")); let manifest=try Updater.verified(signed,validSignature)
            try check(manifest.Version == "0.0.0","Pinned release key verifies architecture-specific public fixture")
            var tampered=signed; tampered[tampered.count-2] ^= 1; rejected=false; do { _=try Updater.verified(tampered,validSignature) } catch { rejected=true }; try check(rejected,"Signed manifest rejects one-byte tampering")
            let discoveryPath=fixture.deletingLastPathComponent().appendingPathComponent("discovery-macos-"+BuildInfo.arch+".json")
            let discoveryBytes=try Data(contentsOf:discoveryPath), discoverySignature=try Data(contentsOf:URL(fileURLWithPath:discoveryPath.path+".sig"))
            let updates=UpdateDiscoveryFixture(discoveryBytes,discoverySignature)
            let offer=try await Updater.check(current:"1.0.0",fetch:updates.fetch)
            try check(offer?.manifest.Version == "99.0.1" && updates.apiCalls == 0,"Public signed discovery succeeds with REST API 403 and makes no API request")
            updates.apiCode=429
            let throttled=try await Updater.check(current:"1.0.0",fetch:updates.fetch)
            try check(throttled != nil && updates.apiCalls == 0,"REST API 429 does not block public signed discovery")
            try check(updates.urls.contains(Updater.asset("99.0.1",updates.prefix+".sig")) && !updates.urls.contains(Updater.latest(updates.prefix+".sig")),"Signature request pins the manifest version during concurrent releases")
            let equal=try await Updater.check(current:"99.0.1",fetch:updates.fetch), older=try await Updater.check(current:"100.0.0",fetch:updates.fetch)
            try check(equal == nil && older == nil,"Public signed discovery does not offer equal or older releases")
            updates.signature=Data(Data(repeating:0,count:64).base64EncodedString().utf8); rejected=false
            do { _=try await Updater.check(current:"1.0.0",fetch:updates.fetch) } catch { rejected=true }
            try check(rejected && updates.apiCalls == 0,"Invalid public signature fails closed without an API fallback")
            updates.signature=discoverySignature
            var badVersion=try JSONSerialization.jsonObject(with:discoveryBytes) as! [String:Any]; badVersion["Version"]="../other"
            updates.bytes=try JSONSerialization.data(withJSONObject:badVersion); rejected=false
            do { _=try await Updater.check(current:"1.0.0",fetch:updates.fetch) } catch { rejected=true }
            try check(rejected && updates.apiCalls == 0,"Untrusted version cannot change the signature download path")
            updates.bytes=Data(repeating:0,count:16385); rejected=false
            do { _=try await Updater.check(current:"1.0.0",fetch:updates.fetch) } catch { rejected=true }
            try check(rejected && updates.apiCalls == 0,"Oversized update manifest stops without a fallback")
            updates.bytes=discoveryBytes; updates.cancel=true; rejected=false
            do { _=try await Updater.check(current:"1.0.0",fetch:updates.fetch) } catch is CancellationError { rejected=true }
            try check(rejected && updates.apiCalls == 0,"Cancelled discovery does not start another source")
            updates.cancel=false; updates.latestCode=404; updates.apiCode=200
            let backup=try await Updater.check(current:"1.0.0",fetch:updates.fetch)
            try check(backup?.manifest.Version == "99.0.1" && updates.apiCalls == 1,"REST API route remains available when public latest download is unavailable")
            updates.forgeAPI=true; rejected=false
            do { _=try await Updater.check(current:"1.0.0",fetch:updates.fetch) } catch { rejected=true }
            try check(rejected,"Fallback API metadata cannot redirect trust to a different host")
            try check(shellQuote("a'b").contains("'\\''"),"Launch arguments preserve single quotes")
            try check(appleLiteral("a\"b\n").contains("\\\""),"Administrator dialog safely quotes paths")
            let model=AppState(preview:true); model.connected=true; model.disconnect()
            for _ in 0..<30 { if !model.busy { break }; try await Task.sleep(for:.milliseconds(100)) }
            try check(!model.connected && model.report == nil,"Disconnect clears stale transport results and semantic metric colors")
            for proceed in [false,true] {
                let decision=Task { await model.confirmLaunch(report,strict:true) }
                for _ in 0..<20 { if model.launchConfirmation != nil { break }; try await Task.sleep(for:.milliseconds(10)) }
                try check(model.launchConfirmation?.reasons.contains(where:{$0.contains("403")}) == true,"Launch sheet exposes failed webpage check (\(proceed))")
                model.resolveLaunch(proceed); let result=await decision.value
                try check(result == proceed && !report.strictReady && model.launchConfirmation == nil,"Launch sheet resolves decision once without changing failed results (\(proceed))")
            }
            let lockDirectory=AppPaths.root.appendingPathComponent("lock-test"); let owner=try SingleInstance.claim(lockDirectory); let duplicate=try SingleInstance.claim(lockDirectory)
            try check(owner != nil && duplicate == nil,"Second GUI instance cannot acquire an active session owner lock"); if let owner { close(owner) }
            let released=try SingleInstance.claim(lockDirectory); try check(released != nil,"Process lock is available after owner releases it"); if let released { close(released) }
            let server=Process(), portPipe=Pipe(); server.executableURL=URL(fileURLWithPath:"/usr/bin/python3"); server.standardOutput=portPipe; server.standardError=FileHandle.nullDevice
            server.arguments=["-u","-c","""
            from http.server import BaseHTTPRequestHandler,HTTPServer
            import time
            class Handler(BaseHTTPRequestHandler):
                def do_GET(self):
                    large='/large' in self.path
                    slow='/slow' in self.path
                    body=b'x'*100000 if large or slow else b'{}'
                    self.send_response(200 if large else 401)
                    self.send_header('Content-Length',str(len(body)))
                    self.end_headers()
                    try:
                        if slow:
                            for _ in range(100):
                                self.wfile.write(b'x'*1000); self.wfile.flush(); time.sleep(.1)
                        else: self.wfile.write(body)
                    except (BrokenPipeError,ConnectionResetError): pass
                def log_message(self,*args): pass
            server=HTTPServer(('127.0.0.1',0),Handler)
            print(server.server_address[1],flush=True)
            server.serve_forever()
            """]
            try server.run(); defer { server.terminate(); server.waitUntilExit() }; var portText=""
            while let byte=try portPipe.fileHandleForReading.read(upToCount:1), !byte.isEmpty { if byte[0] == 10 { break }; portText += String(decoding:byte,as:UTF8.self) }
            guard let proxyPort=Int(portText) else { throw ClientError.message("HTTP fixture failed") }
            let transport=try await Web.fetch("http://fixture.invalid/small",port:proxyPort,truncate:true); try check(transport.code == 401,"CFNetwork sends explicit HTTP proxy requests and preserves HTTP 401")
            let large=await Web.probe("http://fixture.invalid/large",port:proxyPort); try check(large.code == 200 && large.data.count == 65536,"Probe preserves HTTP status while truncating a large webpage")
            rejected=false; do { _=try await Web.fetch("http://fixture.invalid/large",port:proxyPort,limit:1024) } catch { rejected=true }; try check(rejected,"Subscription-style fetch rejects oversized bodies")
            let watch=Date(); let download=Task { try await Web.fetch("http://fixture.invalid/slow",port:proxyPort,limit:200000) }; try await Task.sleep(for:.milliseconds(300)); download.cancel(); rejected=false
            do { _=try await download.value } catch { rejected=true }; try check(rejected && Date().timeIntervalSince(watch) < 3,"Cancel interrupts an active streamed HTTP download")
            let result:[String:Any]=["Passed":checks.count,"Checks":checks,"Architecture":BuildInfo.arch,"OS":ProcessInfo.processInfo.operatingSystemVersionString]
            try JSONSerialization.data(withJSONObject:result,options:.prettyPrinted).write(to:URL(fileURLWithPath:CommandLine.arguments[1]))
        } catch { fputs("Native regression failed: \(error)\n",stderr); exit(1) }
    }
}

private final class UpdateDiscoveryFixture {
    var bytes:Data; var signature:Data
    var apiCode=403, latestCode=200, apiCalls=0
    var cancel=false, forgeAPI=false
    var urls:[String]=[]
    let prefix="update-macos-"+BuildInfo.arch+".json"
    init(_ bytes:Data,_ signature:Data) { self.bytes=bytes; self.signature=signature }
    func fetch(_ url:String,_ limit:Int) async throws -> WebResponse {
        urls.append(url)
        if cancel { throw CancellationError() }
        var code=200; var data=Data()
        if url == Updater.latest(prefix) { code=latestCode; data=bytes }
        else if url == "https://api.github.com/repos/"+Updater.repository+"/releases/latest" {
            apiCalls += 1; code=apiCode
            let names=[prefix,prefix+".sig","ClaudeLink-99.0.1-macos-"+BuildInfo.arch+".zip"]
            let assets=names.map { ["name":$0,"browser_download_url":forgeAPI ? "https://example.invalid/forged" : Updater.asset("99.0.1",$0)] }
            data=try JSONSerialization.data(withJSONObject:["draft":false,"prerelease":false,"tag_name":"v99.0.1","assets":assets])
        } else if url == Updater.asset("99.0.1",prefix) { data=bytes }
        else if url == Updater.asset("99.0.1",prefix+".sig") { data=signature }
        else { code=404 }
        return WebResponse(code:code,data:data,headers:[:])
    }
}
