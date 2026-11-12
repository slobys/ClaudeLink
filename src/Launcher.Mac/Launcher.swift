import AppKit
import Foundation

@MainActor enum Launcher {
    static func desktop(_ settings: Settings) throws {
        let path = settings.DesktopPath.isEmpty ? NSWorkspace.shared.urlForApplication(withBundleIdentifier:"com.anthropic.claudefordesktop")?.path ?? "/Applications/Claude.app" : settings.DesktopPath
        guard FileManager.default.fileExists(atPath:path), path.hasSuffix(".app") else { throw ClientError.message("未找到官方 Claude 桌面应用，请安装后选择 Claude.app。") }
        var components = URLComponents(); components.scheme="claude"; components.host="code"; components.path="/new"; components.queryItems=[URLQueryItem(name:"folder",value:settings.ProjectDirectory)]
        NSWorkspace.shared.open([components.url!],withApplicationAt:URL(fileURLWithPath:path),configuration:NSWorkspace.OpenConfiguration())
    }
    static func cli(_ settings: Settings) throws {
        let path = settings.ClaudePath.isEmpty ? [FileManager.default.homeDirectoryForCurrentUser.path+"/.local/bin/claude","/opt/homebrew/bin/claude","/usr/local/bin/claude"].first(where:FileManager.default.isExecutableFile(atPath:)) ?? "" : settings.ClaudePath
        guard FileManager.default.isExecutableFile(atPath:path) else { throw ClientError.message("未找到 Claude Code CLI，请选择 claude 可执行文件。") }
        let proxy="http://127.0.0.1:\(settings.ProxyPort)", language=settings.ProcessLanguage.replacingOccurrences(of:"-",with:"_")+".UTF-8"
        let script = "#!/bin/sh\ncd "+shellQuote(settings.ProjectDirectory)+" || exit 1\nHTTP_PROXY="+shellQuote(proxy)+" HTTPS_PROXY="+shellQuote(proxy)+" ALL_PROXY="+shellQuote(proxy)+" LANG="+shellQuote(language)+" LC_ALL="+shellQuote(language)+" "+shellQuote(path)+"\n"
        let url = AppPaths.root.appendingPathComponent("launch-cli.command"); try AppPaths.write(Data(script.utf8),to:url,mode:0o700); NSWorkspace.shared.open(url)
    }
    static func browser(_ settings: Settings) async throws -> Bool {
        let chrome="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", edge="/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
        let executable = settings.CheckBrowser == "edge" ? edge : settings.CheckBrowser == "chrome" ? chrome : FileManager.default.isExecutableFile(atPath:chrome) ? chrome : edge
        guard FileManager.default.isExecutableFile(atPath:executable) else { throw ClientError.message("请安装所选的 Chrome 或 Edge 浏览器。") }
        let profile = AppPaths.root.appendingPathComponent("browser-"+UUID().uuidString)
        let prefs: [String:Any] = ["intl":["accept_languages":settings.ProcessLanguage+",en","selected_languages":settings.ProcessLanguage+",en"]]
        try AppPaths.write(JSONSerialization.data(withJSONObject:prefs),to:profile.appendingPathComponent("Default/Preferences"))
        let language = settings.ProcessLanguage
        let process=Process(); process.executableURL=URL(fileURLWithPath:executable); process.arguments=["--user-data-dir="+profile.path,"--lang="+language,"--accept-lang="+language+",en","--no-first-run","--no-default-browser-check","--remote-debugging-port=0","--remote-debugging-address=127.0.0.1","--disable-quic","--force-webrtc-ip-handling-policy=disable_non_proxied_udp","--proxy-server=http://127.0.0.1:\(settings.ProxyPort)","--new-window","about:blank"]; process.standardOutput=FileHandle.nullDevice; process.standardError=FileHandle.nullDevice; try process.run()
        return try await BrowserPrivacy.configure(profile)
    }
}
