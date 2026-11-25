import Foundation
import Darwin

enum TemporaryData {
    static var applicationRoot: URL {
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/ClaudeLink")
    }
    static var systemTemp: URL { FileManager.default.temporaryDirectory.resolvingSymlinksInPath().standardizedFileURL }

    static func remove(_ directory: URL, under parent: URL, prefix: String) {
        let root = parent.standardizedFileURL
        let target = directory.standardizedFileURL
        guard target.deletingLastPathComponent().path == root.path,
              target.lastPathComponent.hasPrefix(prefix),
              UUID(uuidString: String(target.lastPathComponent.dropFirst(prefix.count))) != nil,
              target.resolvingSymlinksInPath().path == target.path else { return }
        var entry = stat()
        guard lstat(target.path, &entry) == 0, entry.st_uid == getuid(), (entry.st_mode & S_IFMT) == S_IFDIR else { return }
        try? FileManager.default.removeItem(at: target)
    }

    private static func runningCommands() -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/ps")
        process.arguments = ["-axww", "-o", "command="]
        let output = Pipe()
        process.standardOutput = output; process.standardError = FileHandle.nullDevice
        do { try process.run() } catch { return nil }
        let bytes = output.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        return process.terminationStatus == 0 ? String(decoding: bytes, as: UTF8.self) : nil
    }

    static func sweepBrowsers(_ root: URL) {
        guard let commands = runningCommands(), let candidates = try? FileManager.default.contentsOfDirectory(
            at: root, includingPropertiesForKeys: [.contentModificationDateKey, .creationDateKey]) else { return }
        let cutoff = Date().addingTimeInterval(-3600)
        for candidate in candidates where candidate.lastPathComponent.hasPrefix("browser-") {
            guard let times = try? candidate.resourceValues(forKeys: [.contentModificationDateKey, .creationDateKey]),
                  let modified = times.contentModificationDate, let created = times.creationDate,
                  modified < cutoff, created < cutoff, !commands.contains(candidate.path) else { continue }
            remove(candidate, under: root, prefix: "browser-")
        }
    }

    static func watchBrowser(_ id: String, root: URL = applicationRoot) async {
        guard getuid() != 0, let uuid = UUID(uuidString: id) else { return }
        let profile = root.appendingPathComponent("browser-" + uuid.uuidString)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.connectionProxyDictionary = [:]
        configuration.timeoutIntervalForRequest = 2
        configuration.timeoutIntervalForResource = 2
        let client = URLSession(configuration: configuration)
        defer { client.invalidateAndCancel() }
        try? await Task.sleep(for: .seconds(2))
        var inactive = 0
        var empty = 0
        var hasShownPage = false
        while inactive < 2 {
            guard let commands = runningCommands() else { return }
            if commands.contains("--user-data-dir=" + profile.path) {
                inactive = 0
                if let count = await pageCount(profile: profile, client: client) {
                    if count > 0 { hasShownPage = true; empty = 0 }
                    else if hasShownPage { empty += 1 }
                    if empty == 2, let endpoint = debugEndpoint(profile) {
                        // On macOS closing the last window may leave Chromium alive in the Dock.
                        let socket = client.webSocketTask(with: endpoint.socket)
                        socket.resume()
                        let timeout = Task { try? await Task.sleep(for: .seconds(2)); if !Task.isCancelled { socket.cancel() } }
                        try? await socket.send(.string(#"{"id":1,"method":"Browser.close"}"#))
                        timeout.cancel()
                        socket.cancel(with: .normalClosure, reason: nil)
                    }
                } else { empty = 0 }
            } else {
                inactive += 1
            }
            try? await Task.sleep(for: .seconds(2))
        }
        remove(profile, under: root, prefix: "browser-")
    }

    private static func debugEndpoint(_ profile: URL) -> (port: Int, socket: URL)? {
        guard let text = try? String(contentsOf: profile.appendingPathComponent("DevToolsActivePort"), encoding: .utf8) else { return nil }
        let lines = text.split(whereSeparator: \.isNewline)
        guard lines.count == 2, let port = Int(lines[0]), (1...65535).contains(port),
              lines[1].hasPrefix("/devtools/browser/"), UUID(uuidString: String(lines[1].dropFirst("/devtools/browser/".count))) != nil,
              let socket = URL(string: "ws://127.0.0.1:\(port)" + lines[1]) else { return nil }
        return (port, socket)
    }

    private static func pageCount(profile: URL, client: URLSession) async -> Int? {
        guard let endpoint = debugEndpoint(profile) else { return nil }
        let base = "http://127.0.0.1:\(endpoint.port)"
        guard let (version, _) = try? await client.data(from: URL(string: base + "/json/version")!),
              let descriptor = (try? JSONSerialization.jsonObject(with: version)) as? [String: Any],
              let socket = descriptor["webSocketDebuggerUrl"] as? String, URL(string: socket) == endpoint.socket,
              let (list, response) = try? await client.data(from: URL(string: base + "/json/list")!),
              (response as? HTTPURLResponse)?.statusCode == 200,
              let targets = (try? JSONSerialization.jsonObject(with: list)) as? [[String: Any]] else { return nil }
        return targets.filter { $0["type"] as? String == "page" }.count
    }
}
