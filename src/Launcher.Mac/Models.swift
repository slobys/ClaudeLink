import Foundation
import SwiftUI
import Darwin

struct ProxyNode: Codable, Identifiable, Hashable {
    var Id: String; var Name: String; var GroupId: String; var OutboundJson: String
    var id: String { Id }
    var outbound: [String: Any] { (try? JSONSerialization.jsonObject(with: Data(OutboundJson.utf8))) as? [String: Any] ?? [:] }
    var kind: String { (outbound["type"] as? String ?? "unknown").uppercased() }
    var display: String { "\(Name) · \(kind)" }
}
struct Subscription: Codable, Identifiable, Hashable {
    var Id = UUID().uuidString; var Name = ""; var Url = ""; var UpdatedAt: String?; var Usage: String?
    var id: String { Id }
}
struct Settings: Codable {
    var Nodes: [ProxyNode] = []; var Subscriptions: [Subscription] = []; var SelectedNodeId: String?
    var ProjectDirectory = FileManager.default.homeDirectoryForCurrentUser.path
    var ProxyPort = 17890; var TunEnabled = true; var StrictChecks = true; var DisableIPv6 = true; var MatchTimeZone = true
    var ProcessLanguage = "en-US"; var CheckBrowser = "auto"; var LaunchTarget = "desktop"; var ClaudePath = ""; var DesktopPath = ""; var AutoCheckUpdates = true
}
enum Level: String, Codable, CaseIterable {
    case info, success, warning, error, pending
    var title: String { switch self { case .info: return "信息"; case .success: return "通过"; case .warning: return "注意"; case .error: return "失败"; case .pending: return "待检测" } }
    var symbol: String { switch self { case .info: return "info.circle"; case .success: return "checkmark.circle.fill"; case .warning: return "exclamationmark.triangle.fill"; case .error: return "xmark.circle.fill"; case .pending: return "clock" } }
    var token: String { switch self { case .info: "Info"; case .success: "Success"; case .warning: "Warning"; case .error: "Error"; case .pending: "Pending" } }
    var color: Color { Palette.color(token) }
    var background: Color { Palette.color(token+"Bg") }
}
struct CheckRow: Identifiable { var id = UUID(); var label: String; var value: String; var detail: String; var level: Level }
struct LaunchConfirmation:Identifiable { var id=UUID(); var apiCode:Int; var apiReachable:Bool; var reasons:[String] }
struct LogEntry: Identifiable {
    var id = UUID(); var date = Date(); var message: String; var level: Level
    private static let clock:DateFormatter = { let value=DateFormatter(); value.locale=Locale(identifier:"en_US_POSIX"); value.dateFormat="HH:mm:ss"; return value }()
    var timeText:String { Self.clock.string(from:date) }
}
struct NetworkReport {
    var generalIP = ""; var cloudflareIP = ""; var claudeIP = ""; var country = ""; var city = ""; var zone = ""
    var apiCode = 0; var webCode = 0; var apiFailure = ""; var tun = false; var ipv6Disabled = false; var zoneMatches = false
    var rows: [CheckRow] = []
    var apiReachable: Bool { [200,400,401,404,405].contains(apiCode) }
    var validIP: Bool { var ipv4=in_addr(); var ipv6=in6_addr(); return generalIP.withCString { inet_pton(AF_INET,$0,&ipv4) == 1 || inet_pton(AF_INET6,$0,&ipv6) == 1 } }
    var basicReady: Bool { apiReachable && validIP }
    var sameIPv4: Bool { !generalIP.isEmpty && generalIP == cloudflareIP && generalIP == claudeIP && !generalIP.contains(":") }
    var strictReady: Bool { basicReady && (200..<400).contains(webCode) && sameIPv4 && country == "US" && tun && ipv6Disabled && zoneMatches }
    func reasons(strict: Bool) -> [String] {
        var result: [String] = []
        if !validIP { result.append("代理出口 IP 未验证") }
        if !apiReachable { result.append(apiCode > 0 ? "Anthropic API 返回 HTTP \(apiCode)" : "API 请求失败：\(apiFailure)") }
        if strict {
            if !(200..<400).contains(webCode) { result.append("Claude 登录页自动探测未通过：HTTP \(webCode)（需浏览器确认）") }
            if !sameIPv4 { result.append("三个 IPv4 出口不一致或未验证") }
            if country != "US" { result.append("严格模式参考地区未验证为 US") }
            if !tun { result.append("TUN 未启用") }; if !ipv6Disabled { result.append("活动物理网络 IPv6 未确认关闭") }; if !zoneMatches { result.append("系统时区未匹配") }
        }
        return result
    }
}
enum Palette {
    static let values: [String: String] = {
        guard let url = Bundle.main.url(forResource: "tokens", withExtension: "json"), let data = try? Data(contentsOf: url), let result = try? JSONDecoder().decode([String:String].self, from:data) else { return [:] }; return result
    }()
    static func color(_ key: String) -> Color {
        let hex = values[key] ?? "#18283D"; let value = UInt64(hex.dropFirst(), radix:16) ?? 0x18283D
        return Color(red:Double((value >> 16) & 255)/255,green:Double((value >> 8) & 255)/255,blue:Double(value & 255)/255)
    }
}
enum AppPaths {
    static var root: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/ClaudeLink")
    static var sessions: URL { root.appendingPathComponent("sessions") }
    static func prepare() throws { try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700]) }
    static func write(_ data: Data, to url: URL, mode: Int = 0o600) throws { try FileManager.default.createDirectory(at:url.deletingLastPathComponent(),withIntermediateDirectories:true,attributes:[.posixPermissions:0o700]); try data.write(to:url,options:.atomic); try FileManager.default.setAttributes([.posixPermissions:mode],ofItemAtPath:url.path) }
}
