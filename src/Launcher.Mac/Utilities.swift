import Foundation
import Security
import CryptoKit
import Darwin

enum ClientError: LocalizedError { case message(String); var errorDescription: String? { if case .message(let text) = self { return text }; return nil } }
func shellQuote(_ value: String) -> String { "'" + value.replacingOccurrences(of:"'",with:"'\\''") + "'" }
func appleLiteral(_ value: String) -> String { "\"" + value.replacingOccurrences(of:"\\",with:"\\\\").replacingOccurrences(of:"\"",with:"\\\"").replacingOccurrences(of:"\n",with:"\\n") + "\"" }
func sha256(_ data: Data) -> String { SHA256.hash(data:data).map { String(format:"%02x",$0) }.joined() }
struct CommandResult { let code: Int32; let output: Data; let error: Data }
enum Commands {
    static func run(_ executable: String, _ args: [String], input: Data? = nil, timeout: TimeInterval = 30) async throws -> CommandResult {
        try await Task.detached(priority:.userInitiated) { try sync(executable,args,input:input,timeout:timeout) }.value
    }
    static func sync(_ executable: String, _ args: [String], input: Data? = nil, timeout: TimeInterval = 30) throws -> CommandResult {
        let process = Process(); process.executableURL = URL(fileURLWithPath:executable); process.arguments = args
        let output = Pipe(), error = Pipe(), standardInput = Pipe(); process.standardOutput = output; process.standardError = error
        process.standardInput = standardInput; let done = DispatchSemaphore(value:0); process.terminationHandler = { _ in done.signal() }
        try process.run()
        let group = DispatchGroup(); let buffers = CommandBuffers()
        group.enter(); DispatchQueue.global().async { buffers.setOutput(output.fileHandleForReading.readDataToEndOfFile()); group.leave() }
        group.enter(); DispatchQueue.global().async { buffers.setError(error.fileHandleForReading.readDataToEndOfFile()); group.leave() }
        if let input { standardInput.fileHandleForWriting.write(input) }; try? standardInput.fileHandleForWriting.close()
        if done.wait(timeout:.now()+timeout) == .timedOut { process.terminate(); if done.wait(timeout:.now()+3) == .timedOut { kill(process.processIdentifier,SIGKILL) }; throw ClientError.message("后台操作超时，请重试。") }
        group.wait(); let data = buffers.get()
        if data.0.count > 32*1024*1024 || data.1.count > 2*1024*1024 { throw ClientError.message("后台响应超过允许大小。") }
        return CommandResult(code:process.terminationStatus,output:data.0,error:data.1)
    }
}
final class CommandBuffers: @unchecked Sendable {
    private let lock = NSLock(); private var output = Data(), error = Data()
    func setOutput(_ data: Data) { lock.lock(); output=data; lock.unlock() }
    func setError(_ data: Data) { lock.lock(); error=data; lock.unlock() }
    func get() -> (Data,Data) { lock.lock(); defer { lock.unlock() }; return (output,error) }
}
enum SecureSettings {
    private static func key() throws -> SymmetricKey {
        let query: [String:Any] = [kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:"com.slobys.claudelink.settings",kSecAttrAccount as String:"config-key",kSecReturnData as String:true,kSecMatchLimit as String:kSecMatchLimitOne]
        var item: CFTypeRef?; let status = SecItemCopyMatching(query as CFDictionary,&item)
        if status == errSecSuccess, let bytes = item as? Data { return SymmetricKey(data:bytes) }
        if status != errSecItemNotFound { throw ClientError.message("无法读取登录钥匙串中的配置密钥（\(status)）。") }
        var bytes = Data(count:32); let random = bytes.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault,32,$0.baseAddress!) }; guard random == errSecSuccess else { throw ClientError.message("无法创建安全配置密钥。") }
        let add: [String:Any] = [kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:"com.slobys.claudelink.settings",kSecAttrAccount as String:"config-key",kSecValueData as String:bytes,kSecAttrAccessible as String:kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly]
        guard SecItemAdd(add as CFDictionary,nil) == errSecSuccess else { throw ClientError.message("配置密钥写入钥匙串失败。") }; return SymmetricKey(data:bytes)
    }
    static func save(_ settings: Settings) throws { try AppPaths.prepare(); let data = try JSONEncoder().encode(settings); let sealed = try AES.GCM.seal(data,using:key()); try AppPaths.write(sealed.combined!,to:AppPaths.root.appendingPathComponent("settings.dat")) }
    static func load() throws -> Settings {
        try AppPaths.prepare(); let path=AppPaths.root.appendingPathComponent("settings.dat"); if !FileManager.default.fileExists(atPath:path.path) { return Settings() }
        let clear=try AES.GCM.open(AES.GCM.SealedBox(combined:Data(contentsOf:path)),using:key())
        guard let saved=try JSONSerialization.jsonObject(with:clear) as? [String:Any], var defaults=try JSONSerialization.jsonObject(with:JSONEncoder().encode(Settings())) as? [String:Any] else { throw ClientError.message("配置内容无效。") }
        for (name,value) in saved { defaults[name]=value }
        return try JSONDecoder().decode(Settings.self,from:JSONSerialization.data(withJSONObject:defaults))
    }
}
enum Bridge {
    struct Parsed: Decodable { var Nodes: [ProxyNode]; var Warnings: [String] }
    static var executable: String { Bundle.main.resourceURL!.appendingPathComponent("bridge/ClaudeLink.Bridge").path }
    static func call(_ object: [String:Any]) async throws -> Data {
        let result = try await Commands.run(executable,[],input:JSONSerialization.data(withJSONObject:object),timeout:25)
        if result.code != 0 { let message = ((try? JSONSerialization.jsonObject(with:result.output)) as? [String:Any])?["Error"] as? String ?? "节点解析失败，请检查格式。"; throw ClientError.message(message) }; return result.output
    }
    static func parse(_ text: String) async throws -> Parsed { try JSONDecoder().decode(Parsed.self,from:await call(["operation":"parse","text":text])) }
    static func config(_ node: ProxyNode, port: Int, tun: Bool, route: Bool = true) async throws -> Data {
        let encoded = try JSONSerialization.jsonObject(with:JSONEncoder().encode(node)); let response = try await call(["operation":"config","node":encoded,"port":port,"tun":tun,"routeEnabled":route,"platform":"macos"])
        let value = (try JSONSerialization.jsonObject(with:response) as! [String:Any])["Config"]!; return try JSONSerialization.data(withJSONObject:value,options:[.prettyPrinted,.sortedKeys])
    }
}
