import Foundation
import Darwin

enum LatencyState:String { case pending,running,success,timeout,failed,cancelled,invalid }
struct NodeLatencyResult {
    var fingerprint:String; var state:LatencyState; var milliseconds:Int?=nil; var measuredAt=Date()
    var hasDelay:Bool { state == .success && (milliseconds ?? 0)>0 }
    var text:String { if hasDelay { return "\(milliseconds!) ms" }; switch state { case .running:return "测速中"; case .timeout:return "超时"; case .failed:return "失败"; case .cancelled:return "已取消"; case .invalid:return "节点失效"; default:return "未测速" } }
    var tone:String { if state == .failed || state == .invalid { return "Error" }; if state == .timeout { return "Warning" }; if hasDelay { return milliseconds!<=300 ? "Success" : milliseconds!<=1000 ? "Warning" : "Error" }; return "Pending" }
}
enum NodeLatency {
    static let target="https://www.gstatic.com/generate_204"
    static let timeout=8000
    static func fingerprint(_ node:ProxyNode,_ nodes:[ProxyNode]) -> String {
        // Native cache key only; the bridge independently validates the actual outbounds.
        let hops=node.isChain ? node.ChainNodeIds.compactMap { id in nodes.first{$0.Id == id} } : [node]
        return node.ChainNodeIds.joined(separator:"|")+hops.map{$0.Id+":"+$0.OutboundJson}.joined(separator:"|")
    }
    static func sort(_ nodes:[ProxyNode],_ direction:Int,_ result:(ProxyNode)->NodeLatencyResult?) -> [ProxyNode] {
        guard direction != 0 else { return nodes }
        return nodes.enumerated().sorted { a,b in
            let x=result(a.element), y=result(b.element)
            if x?.hasDelay != y?.hasDelay { return x?.hasDelay == true }
            if x?.hasDelay == true, let xm=x?.milliseconds, let ym=y?.milliseconds, xm != ym { return direction == 2 ? xm>ym : xm<ym }
            return a.offset<b.offset
        }.map(\.element)
    }
    static func freePort() throws -> Int {
        let fd=socket(AF_INET,SOCK_STREAM,0); guard fd>=0 else { throw ClientError.message("无法分配测速端口。") }; defer { close(fd) }
        var addr=sockaddr_in(); addr.sin_len=UInt8(MemoryLayout<sockaddr_in>.size); addr.sin_family=sa_family_t(AF_INET); addr.sin_addr.s_addr=inet_addr("127.0.0.1"); addr.sin_port=0
        let bound=withUnsafePointer(to:&addr) { pointer in pointer.withMemoryRebound(to:sockaddr.self,capacity:1) { bind(fd,$0,socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        var length=socklen_t(MemoryLayout<sockaddr_in>.size)
        let read=withUnsafeMutablePointer(to:&addr) { pointer in pointer.withMemoryRebound(to:sockaddr.self,capacity:1) { getsockname(fd,$0,&length) } }
        guard bound==0 && read==0 else { throw ClientError.message("无法分配测速端口。") }; return Int(UInt16(bigEndian:addr.sin_port))
    }
    static func measure(_ client:URLSession,_ base:URL,_ secret:String,_ tag:String,target:String=NodeLatency.target,timeout:Int=NodeLatency.timeout) async throws -> (LatencyState,Int?) {
        var parts=URLComponents(url:base.appendingPathComponent("proxies/\(tag)/delay"),resolvingAgainstBaseURL:false)!
        parts.queryItems=[URLQueryItem(name:"timeout",value:String(timeout)),URLQueryItem(name:"url",value:target)]
        var request=URLRequest(url:parts.url!); request.setValue("Bearer "+secret,forHTTPHeaderField:"Authorization"); request.timeoutInterval=Double(timeout)/1000+2
        do {
            let (data,response)=try await client.data(for:request); try Task.checkCancellation()
            let code=(response as? HTTPURLResponse)?.statusCode
            if code==504 { return (.timeout,nil) }
            guard code==200, let json=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any], let ms=json["delay"] as? Int, ms>0 && ms<=timeout else { return (.failed,nil) }
            return (.success,ms)
        } catch { if Task.isCancelled { throw CancellationError() }; return ((error as? URLError)?.code == .timedOut ? .timeout : .failed,nil) }
    }
}

@MainActor final class NodeLatencyProbe {
    private var process:Process?; private var folder:URL?
    func stop() async {
        guard let root=folder else { return }; try? Data().write(to:root.appendingPathComponent("cancel"))
        if let process { for _ in 0..<60 { if !process.isRunning { break }; await Task.detached { try? await Task.sleep(for:.milliseconds(100)) }.value } }
        // The guardian observes the parent and cancel file; never kill it ahead of its core cleanup.
        if process == nil || process?.isRunning == false { try? FileManager.default.removeItem(at:root); folder=nil; process=nil }
    }
    func run(_ nodes:[ProxyNode],_ available:[ProxyNode],changed:@escaping @MainActor (String,LatencyState,Int?)->Void) async throws {
        let root=FileManager.default.temporaryDirectory.resolvingSymlinksInPath().appendingPathComponent("ClaudeLink-Latency-"+UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:false,attributes:[.posixPermissions:0o700]); folder=root
        do {
            let port=try NodeLatency.freePort(), secret=UUID().uuidString.replacingOccurrences(of:"-",with:"")+UUID().uuidString.replacingOccurrences(of:"-",with:"")
            let encoded=try JSONSerialization.jsonObject(with:JSONEncoder().encode(nodes)), all=try JSONSerialization.jsonObject(with:JSONEncoder().encode(available))
            let data=try await Bridge.call(["operation":"latency-config","selected":encoded,"nodes":all,"port":port,"secret":secret])
            guard let plan=try JSONSerialization.jsonObject(with:data) as? [String:Any], let config=plan["Config"], let tags=plan["Tags"] as? [String:String] else { throw ClientError.message("测速配置无效。") }
            for id in plan["InvalidNodeIds"] as? [String] ?? [] { changed(id,.invalid,nil) }
            guard !tags.isEmpty else { await stop(); return }
            try JSONSerialization.data(withJSONObject:config).write(to:root.appendingPathComponent("config.json"),options:.atomic)
            let stamp=try await Commands.run("/bin/ps",["-p",String(getpid()),"-o","lstart="])
            let control:[String:Any]=["parent":getpid(),"stamp":String(decoding:stamp.output,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines)]
            try JSONSerialization.data(withJSONObject:control).write(to:root.appendingPathComponent("control.json"))
            try Task.checkCancellation()
            let child=Process(); child.executableURL=Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/ClaudeLinkHost"); child.arguments=["--latency",root.path]; child.standardOutput=FileHandle.nullDevice; child.standardError=FileHandle.nullDevice
            try child.run(); process=child
            let configuration=URLSessionConfiguration.ephemeral; configuration.connectionProxyDictionary=[:]; configuration.urlCache=nil
            let client=URLSession(configuration:configuration); defer { client.invalidateAndCancel() }
            let base=URL(string:"http://127.0.0.1:\(port)")!; var ready=false
            for _ in 0..<60 {
                try Task.checkCancellation(); if !child.isRunning { break }
                var request=URLRequest(url:base.appendingPathComponent("version")); request.setValue("Bearer "+secret,forHTTPHeaderField:"Authorization"); request.timeoutInterval=0.5
                if let (_,response)=try? await client.data(for:request), (response as? HTTPURLResponse)?.statusCode == 200 { ready=true; break }
                try await Task.sleep(for:.milliseconds(100))
            }
            guard ready else { throw ClientError.message("测速内核未能启动，请稍后重试。") }
            try? FileManager.default.removeItem(at:root.appendingPathComponent("config.json"))
            // Four workers share one isolated core. Each API request targets an explicit outbound tag.
            let entries=Array(tags); var next=0
            try await withThrowingTaskGroup(of:(String,LatencyState,Int?).self) { group in
                @MainActor func enqueue(_ item:(key:String,value:String)) {
                    changed(item.key,.running,nil)
                    group.addTask { let value=try await NodeLatency.measure(client,base,secret,item.value); return (item.key,value.0,value.1) }
                }
                while next<min(4,entries.count) { enqueue(entries[next]); next+=1 }
                while let result=try await group.next() { changed(result.0,result.1,result.2); try Task.checkCancellation(); if next<entries.count { enqueue(entries[next]); next+=1 } }
            }
            await stop()
        } catch { await stop(); throw error }
    }
}

extension AppState {
    func latency(_ node:ProxyNode) -> NodeLatencyResult? {
        guard let result=nodeLatencies[node.Id], result.fingerprint == NodeLatency.fingerprint(node,settings.Nodes) else { return nil }; return result
    }
    func testNodes(_ nodes:[ProxyNode]) {
        if nodeTesting { nodeTestTask?.cancel(); return }
        guard !busy && !connected && !nodes.isEmpty else { return }
        nodeTesting=true; var completed=0; nodeTestProgress="测速 0 / \(nodes.count)"
        for node in nodes { nodeLatencies[node.Id]=NodeLatencyResult(fingerprint:NodeLatency.fingerprint(node,settings.Nodes),state:.pending) }
        let available=settings.Nodes
        nodeTestTask=Task {
            defer {
                for node in nodes { if let result=nodeLatencies[node.Id], result.state == .pending || result.state == .running { nodeLatencies[node.Id]=NodeLatencyResult(fingerprint:NodeLatency.fingerprint(node,available),state:Task.isCancelled ? .cancelled : .failed) } }
                nodeTestProgress="\(Task.isCancelled ? "已取消" : "已完成") · 完成 \(completed) / \(nodes.count)"; nodeTesting=false; nodeTestTask=nil
            }
            do { try await nodeProbe.run(nodes,available) { id,state,ms in
                guard let node=available.first(where:{$0.Id == id}) else { return }
                self.nodeLatencies[id]=NodeLatencyResult(fingerprint:NodeLatency.fingerprint(node,available),state:state,milliseconds:ms)
                if state != .running { completed+=1 }; self.nodeTestProgress="测速 \(completed) / \(nodes.count)"
            }; log("节点 HTTP 延迟测试完成。",.success) }
            catch is CancellationError { log("测速已取消，已完成的结果已保留。",.info) }
            catch { fail("测速未完成，请检查网络或稍后重试。") }
        }
    }
    func stopNodeTest() async { nodeTestTask?.cancel(); await nodeTestTask?.value; await nodeProbe.stop() }
    func fillLatencyPreview() {
        let states:[LatencyState]=[.success,.success,.success,.timeout,.failed,.running,.cancelled,.pending]
        for (i,node) in settings.Nodes.enumerated() { let state=states[i%states.count]; nodeLatencies[node.Id]=NodeLatencyResult(fingerprint:NodeLatency.fingerprint(node,settings.Nodes),state:state,milliseconds:state == .success ? [86,430,1280][i%3] : nil) }
        nodeTestProgress="界面示例 · 非实测数据"
    }
}
