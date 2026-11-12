import Foundation
import Darwin

// An unprivileged guardian, separate from the normal privileged TUN/session path.
func latencyHostMain(_ path:String) throws {
    guard getuid() != 0 else { throw HostFailure.invalid("测速不可使用管理员身份。") }
    let root=URL(fileURLWithPath:path).standardizedFileURL
    let temporary=FileManager.default.temporaryDirectory.resolvingSymlinksInPath().standardizedFileURL
    let name=root.lastPathComponent
    var st=stat()
    guard root.deletingLastPathComponent() == temporary, name.hasPrefix("ClaudeLink-Latency-"),
          UUID(uuidString:String(name.dropFirst("ClaudeLink-Latency-".count))) != nil,
          lstat(root.path,&st)==0, (st.st_mode & S_IFMT)==S_IFDIR, st.st_uid==getuid(), st.st_mode & 0o777 == 0o700 else { throw HostFailure.invalid("测速目录无效。") }
    let data=try Data(contentsOf:root.appendingPathComponent("control.json"))
    guard let control=try JSONSerialization.jsonObject(with:data) as? [String:Any], let parent=control["parent"] as? Int32,
          let stamp=control["stamp"] as? String, parent>1,
          try command("/bin/ps",["-p",String(parent),"-o","lstart="]) == stamp else { throw HostFailure.invalid("测速进程已退出。") }
    let executable=URL(fileURLWithPath:CommandLine.arguments[0]).resolvingSymlinksInPath()
    let core=executable.deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("Resources/sing-box")
    guard digest(try Data(contentsOf:core)) == BuildInfo.coreHash else { throw HostFailure.invalid("测速内核校验失败。") }
    let process=Process(); process.executableURL=core; process.arguments=["run","-c",root.appendingPathComponent("config.json").path]
    process.standardOutput=FileHandle.nullDevice; process.standardError=FileHandle.nullDevice
    process.environment=["PATH":"/usr/bin:/bin:/usr/sbin:/sbin","HOME":NSHomeDirectory(),"TMPDIR":temporary.path]
    try process.run()
    defer {
        if process.isRunning { process.terminate(); for _ in 0..<30 { if !process.isRunning { break }; Thread.sleep(forTimeInterval:0.1) }; if process.isRunning { kill(process.processIdentifier,SIGKILL) }; process.waitUntilExit() }
        try? FileManager.default.removeItem(at:root.appendingPathComponent("config.json"))
    }
    let deadline=Date().addingTimeInterval(20*60)
    while process.isRunning && Date()<deadline && !FileManager.default.fileExists(atPath:root.appendingPathComponent("cancel").path) {
        guard (try? command("/bin/ps",["-p",String(parent),"-o","lstart="])) == stamp else { break }
        Thread.sleep(forTimeInterval:0.25)
    }
}
