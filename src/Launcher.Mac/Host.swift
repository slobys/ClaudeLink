import Foundation
import CryptoKit
import Darwin

// This helper only runs following an interactive macOS administrator authorization.
// All root-writable state is kept in a root-owned directory, never in the user's directory.
struct HostControl: Codable {
    var parent: Int32; var parentStamp: String; var mailbox: String; var nonce: String; var port: Int; var disableIPv6: Bool
}
struct IPv6Original: Codable { var service: String; var mode: String }
struct HostJournal: Codable { var ipv6: [IPv6Original] = []; var originalZone: String?; var appliedZone: String? }
struct HostStatus: Codable { var state = "starting"; var message = ""; var ipv6Disabled = false; var zone = ""; var pid = getpid() }
enum HostFailure: Error { case invalid(String) }
func digest(_ bytes: Data) -> String { SHA256.hash(data:bytes).map { String(format:"%02x",$0) }.joined() }
func command(_ path: String,_ arguments: [String]) throws -> String {
    let process = Process(); process.executableURL = URL(fileURLWithPath:path); process.arguments = arguments
    process.environment = ["PATH":"/usr/bin:/bin:/usr/sbin:/sbin","LANG":"en_US.UTF-8"]
    let pipe = Pipe(); process.standardOutput=pipe; process.standardError=pipe; try process.run()
    let data = pipe.fileHandleForReading.readDataToEndOfFile(); process.waitUntilExit()
    guard process.terminationStatus == 0 else { throw HostFailure.invalid("系统环境命令未完成。") }
    return String(decoding:data,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines)
}
func atomic<T: Encodable>(_ value: T,_ url: URL,_ mode: Int = 0o600) throws {
    try JSONEncoder().encode(value).write(to:url,options:.atomic); try FileManager.default.setAttributes([.posixPermissions:mode],ofItemAtPath:url.path)
}
func privateRootDirectory(_ path: String) throws {
    var st = stat(); guard lstat(path,&st) == 0, st.st_uid == 0, (st.st_mode & S_IFMT) == S_IFDIR, (st.st_mode & 0o022) == 0 else { throw HostFailure.invalid("后台会话目录权限不正确。") }
}
func readMailbox(_ path: String) -> String? {
    let fd = open(path,O_RDONLY|O_NOFOLLOW|O_NONBLOCK); guard fd >= 0 else { return nil }; defer { close(fd) }
    var st = stat(); guard fstat(fd,&st) == 0, (st.st_mode & S_IFMT) == S_IFREG, st.st_size < 1024 else { return nil }
    var bytes = [UInt8](repeating:0,count:1024); let size = read(fd,&bytes,1024); guard size > 0 else { return nil }; return String(decoding:bytes.prefix(size),as:UTF8.self)
}
func activeServices() throws -> [String] {
    let order = try command("/usr/sbin/networksetup",["-listnetworkserviceorder"]).components(separatedBy:"\n")
    var services: [String] = []; var service = ""
    for line in order {
        if line.range(of:#"^\(\d+\) "#,options:.regularExpression) != nil { service = line.replacingOccurrences(of:#"^\(\d+\) "#,with:"",options:.regularExpression) }
        if let range = line.range(of:#"Device: (en\d+)"#,options:.regularExpression) {
            let device = String(line[range]).replacingOccurrences(of:"Device: ",with:"")
            if (try? command("/sbin/ifconfig",[device]).contains("status: active")) == true, !service.isEmpty { services.append(service) }
        }
    }
    return services
}
func ipv6Mode(_ service: String) throws -> String {
    let info = try command("/usr/sbin/networksetup",["-getinfo",service])
    guard let line = info.components(separatedBy:"\n").first(where:{$0.hasPrefix("IPv6: ")}) else { throw HostFailure.invalid("无法读取 IPv6 原状态。") }
    let mode = String(line.dropFirst(6)).lowercased()
    guard ["automatic","off","link-local only"].contains(mode) else { throw HostFailure.invalid("此网络服务使用手动 IPv6，请先关闭临时 IPv6 选项。") }; return mode
}
func zone() throws -> String { try command("/usr/sbin/systemsetup",["-gettimezone"]).replacingOccurrences(of:"Time Zone: ",with:"") }
func restore(_ stage: URL,_ journal: inout HostJournal) throws {
    var failures = false
    for item in journal.ipv6.reversed() {
        do { if try ipv6Mode(item.service) == "off" { let flag = item.mode == "automatic" ? "-setv6automatic" : item.mode == "off" ? "-setv6off" : "-setv6linklocal"; _ = try command("/usr/sbin/networksetup",[flag,item.service]) } }
        catch { failures = true }
    }
    if let original = journal.originalZone, let applied = journal.appliedZone {
        do { if try zone() == applied { _ = try command("/usr/sbin/systemsetup",["-settimezone",original]) } } catch { failures = true }
    }
    if failures { throw HostFailure.invalid("部分临时环境尚未恢复，请重新打开启动器恢复。") }
    journal = HostJournal(); try atomic(journal,stage.appendingPathComponent("journal.json"))
}
func hostMain() throws {
    guard getuid() == 0, CommandLine.arguments.count >= 3 else { throw HostFailure.invalid("需要管理员授权。") }
    let action = CommandLine.arguments[1], path = CommandLine.arguments[2]
    let base = "/Library/Application Support/ClaudeLink/Sessions"
    guard path.hasPrefix(base+"/"), UUID(uuidString:String(path.dropFirst(base.count+1))) != nil else { throw HostFailure.invalid("会话路径无效。") }
    for directory in ["/Library/Application Support/ClaudeLink",base,path] { try privateRootDirectory(directory) }
    let stage = URL(fileURLWithPath:path), journalPath = stage.appendingPathComponent("journal.json"), statusPath = stage.appendingPathComponent("status.json")
    var journal = (try? JSONDecoder().decode(HostJournal.self,from:Data(contentsOf:journalPath))) ?? HostJournal()
    if action == "--recover" { try restore(stage,&journal); try atomic(HostStatus(state:"stopped",message:"临时环境已恢复。"),statusPath,0o644); return }
    guard action == "--run" else { throw HostFailure.invalid("操作无效。") }
    let lock = open("/var/run/com.slobys.claudelink.lock",O_CREAT|O_RDWR|O_NOFOLLOW,0o600)
    guard lock >= 0, flock(lock,LOCK_EX|LOCK_NB) == 0 else { throw HostFailure.invalid("已有 TUN 会话正在运行。") }; defer { close(lock) }
    let core = stage.appendingPathComponent("sing-box")
    guard digest(try Data(contentsOf:core)) == BuildInfo.coreHash else { throw HostFailure.invalid("代理内核校验失败。") }
    let control = try JSONDecoder().decode(HostControl.self,from:Data(contentsOf:stage.appendingPathComponent("control.json")))
    guard control.port > 1024 && control.port < 65536, control.nonce.count == 64, control.parent > 1 else { throw HostFailure.invalid("会话参数无效。") }
    let process = Process(); var status = HostStatus()
    var cleanupError: Error?
    do {
        try atomic(journal,journalPath)
        if control.disableIPv6 {
            let services = try activeServices(); guard !services.isEmpty else { throw HostFailure.invalid("未找到活动物理网络服务。") }
            journal.ipv6 = try services.map { IPv6Original(service:$0,mode:try ipv6Mode($0)) }; try atomic(journal,journalPath)
            for service in services { _ = try command("/usr/sbin/networksetup",["-setv6off",service]) }
            status.ipv6Disabled = try services.allSatisfy { try ipv6Mode($0) == "off" }
        }
        let log = stage.appendingPathComponent("core.log"); FileManager.default.createFile(atPath:log.path,contents:nil,attributes:[.posixPermissions:0o600]); let handle = try FileHandle(forWritingTo:log)
        process.executableURL=core; process.arguments=["run","-c",stage.appendingPathComponent("config.json").path]; process.standardOutput=handle; process.standardError=handle
        process.environment=["PATH":"/usr/bin:/bin:/usr/sbin:/sbin","HOME":"/var/root","LANG":"en_US.UTF-8"]; try process.run()
        status.state="running"; try atomic(status,statusPath,0o644)
        while process.isRunning {
            if readMailbox(control.mailbox+"/stop") == control.nonce { break }
            if (try? command("/bin/ps",["-p",String(control.parent),"-o","lstart="])) != control.parentStamp { break }
            if let request = readMailbox(control.mailbox+"/zone"), request.hasPrefix(control.nonce+"\n") {
                let requested = String(request.dropFirst(65))
                if requested != status.zone, TimeZone.knownTimeZoneIdentifiers.contains(requested) {
                    if journal.originalZone == nil { journal.originalZone = try zone() }
                    journal.appliedZone = requested; try atomic(journal,journalPath)
                    _ = try command("/usr/sbin/systemsetup",["-settimezone",requested]); status.zone = try zone(); try atomic(status,statusPath,0o644)
                }
            }
            Thread.sleep(forTimeInterval:0.25)
        }
        if process.isRunning { process.terminate(); for _ in 0..<40 { if !process.isRunning { break }; Thread.sleep(forTimeInterval:0.1) }; if process.isRunning { kill(process.processIdentifier,SIGKILL) }; process.waitUntilExit() }
        try? handle.close()
        if readMailbox(control.mailbox+"/stop") != control.nonce, (try? command("/bin/ps",["-p",String(control.parent),"-o","lstart="])) == control.parentStamp { cleanupError = HostFailure.invalid("代理内核已退出，请重新连接并检查节点。") }
    } catch { cleanupError = error; if process.isRunning { process.terminate(); process.waitUntilExit() } }
    do { try restore(stage,&journal) } catch { cleanupError = error }
    status.state = cleanupError == nil ? "stopped" : "failed"; status.message = cleanupError == nil ? "会话已结束，临时环境已恢复。" : "会话失败；若临时环境未恢复，请重新打开启动器恢复。"
    try atomic(status,statusPath,0o644)
    if journal.ipv6.isEmpty && journal.originalZone == nil { for name in ["config.json","control.json","core.log"] { try? FileManager.default.removeItem(at:stage.appendingPathComponent(name)) } }
}
@main struct HostMain {
    static func main() { do { try hostMain() } catch { fputs("Claude Link 后台操作失败，请检查授权、节点及恢复记录。\n",stderr); exit(1) } }
}
