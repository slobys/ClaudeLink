import AppKit
import Darwin
import Foundation

enum SingleInstance {
    private static var held:Int32 = -1
    static func claim(_ directory:URL) throws -> Int32? {
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
        let descriptor=open(directory.appendingPathComponent("instance.lock").path,O_CREAT|O_RDWR|O_NOFOLLOW|O_CLOEXEC,0o600)
        guard descriptor >= 0 else { throw ClientError.message("无法创建启动器锁，请检查配置目录权限。") }
        guard flock(descriptor,LOCK_EX|LOCK_NB) == 0 else { close(descriptor); return nil }
        return descriptor
    }
    @MainActor static func acquire() throws -> Bool {
        guard let descriptor=try claim(AppPaths.root) else {
            NSWorkspace.shared.runningApplications.first { $0.bundleIdentifier == Bundle.main.bundleIdentifier && $0.processIdentifier != getpid() }?.activate(options:[])
            return false
        }
        held=descriptor; return true
    }
}
