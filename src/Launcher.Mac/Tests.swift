import AppKit
import CryptoKit
import Foundation

@main struct NativeTests {
    @MainActor static func main() async {
        var checks:[String]=[]
        func check(_ condition:Bool,_ text:String) throws { guard condition else { throw ClientError.message(text) }; checks.append(text); print("PASS "+text) }
        do {
            AppPaths.root=FileManager.default.temporaryDirectory.appendingPathComponent("ClaudeLink-tests-"+UUID().uuidString)
            let parsed=try await Bridge.parse("socks5://user:secret@127.0.0.1:1080#Local")
            try check(parsed.Nodes.count == 1,"Shared bridge imports SOCKS credentials")
            let node=parsed.Nodes[0]
            for tun in [false,true] {
                let config=try await Bridge.config(node,port:17991,tun:tun,route:false)
                let value=try JSONSerialization.jsonObject(with:config) as! [String:Any]
                let inbounds=value["inbounds"] as! [[String:Any]]
                try check(!tun || inbounds.first(where:{$0["type"] as? String == "tun"})?["interface_name"] == nil,"Darwin TUN lets OS allocate utun (\(tun))")
                let path=AppPaths.root.appendingPathComponent("config-\(tun).json"); try AppPaths.write(config,to:path)
                let result=try await Commands.run(Bundle.main.resourceURL!.appendingPathComponent("sing-box").path,["check","-c",path.path]); try check(result.code == 0,"Official Darwin core validates generated configuration (\(tun))")
            }
            var settings=Settings(); settings.Nodes=[node]; settings.Subscriptions=[Subscription(Id:UUID().uuidString,Name:"Private",Url:"https://example.invalid/token-private")]
            try SecureSettings.save(settings); let loaded=try SecureSettings.load(); try check(loaded.Nodes == settings.Nodes && loaded.Subscriptions == settings.Subscriptions,"Keychain AES-GCM settings roundtrip")
            let encrypted=try Data(contentsOf:AppPaths.root.appendingPathComponent("settings.dat")); try check(!String(decoding:encrypted,as:UTF8.self).contains("token-private"),"Encrypted config hides subscription tokens")
            var corrupted=encrypted; corrupted[corrupted.count-1] ^= 1; try AppPaths.write(corrupted,to:AppPaths.root.appendingPathComponent("settings.dat"))
            var rejected=false; do { _=try SecureSettings.load() } catch { rejected=true }; try check(rejected,"Corrupted ciphertext is rejected without replacing data")
            var report=NetworkReport(); report.generalIP="198.51.100.24"; report.cloudflareIP=report.generalIP; report.claudeIP=report.generalIP; report.apiCode=401; report.webCode=403; report.country="US"; report.tun=true; report.ipv6Disabled=true; report.zoneMatches=true
            try check(report.basicReady && !report.strictReady,"HTTP 401 means transport reachable; HTTP 403 fails strict webpage check")
            Web.rows(&report); try check(report.rows.contains(where:{$0.label == "Claude 网页" && $0.level == .warning}),"Webpage warning uses semantic warning state")
            let hostile=Data("{}".utf8), signature=Data(Data(repeating:0,count:64).base64EncodedString().utf8); rejected=false; do { _=try Updater.verified(hostile,signature) } catch { rejected=true }; try check(rejected,"Update rejects forged signature")
            try check(shellQuote("a'b").contains("'\\''"),"Launch arguments preserve single quotes")
            try check(appleLiteral("a\"b\n").contains("\\\""),"Administrator dialog safely quotes paths")
            let result:[String:Any]=["Passed":checks.count,"Checks":checks,"Architecture":BuildInfo.arch,"OS":ProcessInfo.processInfo.operatingSystemVersionString]
            try JSONSerialization.data(withJSONObject:result,options:.prettyPrinted).write(to:URL(fileURLWithPath:CommandLine.arguments[1]))
        } catch { fputs("Native regression failed: \(error)\n",stderr); exit(1) }
    }
}
