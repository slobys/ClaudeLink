import AppKit
import Foundation

struct RootStatus: Decodable { var state: String; var message: String; var ipv6Disabled: Bool; var zone: String; var pid: Int32 }
struct PendingSession: Codable { var stage: String; var mailbox: String; var nonce: String }
@MainActor final class Session {
    var process: Process?; var pending: PendingSession?; var tun = false
    var running: Bool { if let pending { return status(pending)?.state == "running" }; return process?.isRunning == true }
    var ipv6Disabled: Bool { pending.flatMap { status($0) }?.ipv6Disabled ?? false }
    private var pendingFile: URL { AppPaths.root.appendingPathComponent("pending-session.json") }
    private func status(_ record: PendingSession) -> RootStatus? { try? JSONDecoder().decode(RootStatus.self,from:Data(contentsOf:URL(fileURLWithPath:record.stage).appendingPathComponent("status.json"))) }
    func authorize(_ script: String) async throws {
        let result = try await Commands.run("/usr/bin/osascript",["-e","do shell script "+appleLiteral(script)+" with administrator privileges"],timeout:120)
        guard result.code == 0 else { throw ClientError.message("管理员授权未完成。请重试；拒绝授权时不会启动 TUN。") }
    }
    func recover() async throws {
        if pending == nil, let bytes = try? Data(contentsOf:pendingFile) { pending = try JSONDecoder().decode(PendingSession.self,from:bytes) }
        guard let pending else { return }
        guard pending.stage.hasPrefix("/Library/Application Support/ClaudeLink/Sessions/"), UUID(uuidString:URL(fileURLWithPath:pending.stage).lastPathComponent) != nil else { throw ClientError.message("恢复记录无效，请检查本地配置。") }
        try AppPaths.write(Data(pending.nonce.utf8),to:URL(fileURLWithPath:pending.mailbox).appendingPathComponent("stop"))
        for _ in 0..<40 { if status(pending)?.state != "running" { break }; try await Task.sleep(for:.milliseconds(100)) }
        // A stopped status proves restoration. Failed/missing status requires root-owned recovery.
        if status(pending)?.state != "stopped" {
            let host = pending.stage+"/ClaudeLinkHost"
            try await authorize("/usr/bin/env -i PATH=/usr/bin:/bin:/usr/sbin:/sbin "+shellQuote(host)+" --recover "+shellQuote(pending.stage))
            guard status(pending)?.state == "stopped" else { throw ClientError.message("临时环境尚未恢复，请重新尝试恢复。") }
        }
        try? FileManager.default.removeItem(at:pendingFile); self.pending=nil
    }
    func start(node: ProxyNode,settings: Settings) async throws {
        try await recover(); guard !running else { return }
        let port = settings.ProxyPort; guard (1025...65535).contains(port) else { throw ClientError.message("端口范围为 1025–65535。") }
        // Never send requests through another application's listener after a failed bind.
        let listener = try await Commands.run("/usr/sbin/lsof",["-nP","-iTCP:\(port)","-sTCP:LISTEN"])
        guard listener.output.isEmpty else { throw ClientError.message("本地端口已被占用，请在环境设置更换端口。") }
        tun = settings.TunEnabled || settings.LaunchTarget == "desktop"
        let id = UUID().uuidString, mailbox = AppPaths.sessions.appendingPathComponent(id)
        try FileManager.default.createDirectory(at:mailbox,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
        let config = try await Bridge.config(node,port:port,tun:tun)
        let configPath = mailbox.appendingPathComponent("config.json"); try AppPaths.write(config,to:configPath)
        let core = Bundle.main.resourceURL!.appendingPathComponent("sing-box")
        let check = try await Commands.run(core.path,["check","-c",configPath.path]); guard check.code == 0 else { throw ClientError.message("节点配置未通过内核校验，请检查节点格式。") }
        if !tun {
            guard !settings.DisableIPv6 && !settings.MatchTimeZone else { throw ClientError.message("临时系统环境设置需要启用 TUN 管理员会话。") }
            let p = Process(); p.executableURL=core; p.arguments=["run","-c",configPath.path]; p.standardOutput=FileHandle.nullDevice; p.standardError=FileHandle.nullDevice; try p.run(); process=p
        } else {
            let stage = "/Library/Application Support/ClaudeLink/Sessions/"+id
            let nonce = UUID().uuidString.replacingOccurrences(of:"-",with:"")+UUID().uuidString.replacingOccurrences(of:"-",with:"")
            let stamp = try await Commands.run("/bin/ps",["-p",String(getpid()),"-o","lstart="])
            let control: [String:Any] = ["parent":getpid(),"parentStamp":String(decoding:stamp.output,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines),"mailbox":mailbox.path,"nonce":nonce,"port":port,"disableIPv6":settings.DisableIPv6]
            let controlPath = mailbox.appendingPathComponent("control.json"); try AppPaths.write(JSONSerialization.data(withJSONObject:control),to:controlPath)
            let host = Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/ClaudeLinkHost")
            let record = PendingSession(stage:stage,mailbox:mailbox.path,nonce:nonce); pending=record; try AppPaths.write(JSONEncoder().encode(record),to:pendingFile)
            // Test every ancestor before copying anything as root; no symlink staging paths.
            let dirs = ["/Library/Application Support/ClaudeLink","/Library/Application Support/ClaudeLink/Sessions",stage]
            var script = "set -eu\numask 022\n"
            for directory in dirs { let q = shellQuote(directory); script += "if [ ! -e \(q) ]; then /bin/mkdir \(q); fi\n[ ! -L \(q) ] && [ \"$(/usr/bin/stat -f %u \(q))\" = 0 ] && [ \"$(/usr/bin/stat -f %Lp \(q))\" = 755 ]\n" }
            script += "/usr/bin/install -m 755 "+shellQuote(host.path)+" "+shellQuote(stage+"/ClaudeLinkHost")+"\n/usr/bin/install -m 755 "+shellQuote(core.path)+" "+shellQuote(stage+"/sing-box")+"\n"
            for (source,name) in [(configPath,"config.json"),(controlPath,"control.json")] { script += "/usr/bin/install -m 600 "+shellQuote(source.path)+" "+shellQuote(stage+"/"+name)+"\n" }
            script += "[ \"$(/usr/bin/shasum -a 256 "+shellQuote(stage+"/ClaudeLinkHost")+" | /usr/bin/awk '{print $1}')\" = "+shellQuote(BuildInfo.hostHash)+" ]\n[ \"$(/usr/bin/shasum -a 256 "+shellQuote(stage+"/sing-box")+" | /usr/bin/awk '{print $1}')\" = "+shellQuote(BuildInfo.coreHash)+" ]\n"
            script += "/usr/bin/nohup /usr/bin/env -i PATH=/usr/bin:/bin:/usr/sbin:/sbin "+shellQuote(stage+"/ClaudeLinkHost")+" --run "+shellQuote(stage)+" >/dev/null 2>&1 </dev/null &"
            do { try await authorize(script) } catch { if !FileManager.default.fileExists(atPath:stage) { pending=nil; try? FileManager.default.removeItem(at:pendingFile) }; throw error }
        }
        for _ in 0..<80 {
            if running { let test = try await Commands.run("/usr/bin/nc",["-z","-G","1","127.0.0.1",String(port)],timeout:3); if test.code == 0 { try? FileManager.default.removeItem(at:configPath); return } }
            if let pending, status(pending)?.state == "failed" { throw ClientError.message(status(pending)?.message ?? "内核启动失败。") }
            try await Task.sleep(for:.milliseconds(150))
        }
        throw ClientError.message("本地代理未能启动，请检查节点并重新连接。")
    }
    func matchZone(_ zone: String) async throws {
        guard let pending, TimeZone.knownTimeZoneIdentifiers.contains(zone) else { throw ClientError.message("出口时区未验证，本次不修改系统时区。") }
        try AppPaths.write(Data((pending.nonce+"\n"+zone).utf8),to:URL(fileURLWithPath:pending.mailbox).appendingPathComponent("zone"))
        for _ in 0..<60 { if status(pending)?.zone == zone { return }; try await Task.sleep(for:.milliseconds(100)) }; throw ClientError.message("系统时区未确认匹配，请查看运行记录。")
    }
    func stop() async throws {
        if let process { if process.isRunning { process.terminate(); for _ in 0..<40 { if !process.isRunning { break }; try await Task.sleep(for:.milliseconds(100)) }; if process.isRunning { throw ClientError.message("代理内核尚未退出，请稍后重试。") } }; self.process=nil }
        try await recover(); tun=false
    }
}
