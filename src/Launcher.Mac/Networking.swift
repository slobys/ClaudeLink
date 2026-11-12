import Foundation

struct WebResponse { var code: Int; var data: Data; var headers: [AnyHashable:Any] }
enum Web {
    static func fetch(_ address: String, port: Int? = nil, limit: Int = 65536, timeout: Double = 15, truncate: Bool = false) async throws -> WebResponse {
        guard let url = URL(string:address), ["https","http"].contains(url.scheme), url.user == nil, url.password == nil else { throw ClientError.message("请输入有效的 HTTP 或 HTTPS 地址。") }
        let config = URLSessionConfiguration.ephemeral; config.timeoutIntervalForRequest=timeout; config.timeoutIntervalForResource=timeout
        if let port { config.connectionProxyDictionary=["HTTPEnable":1,"HTTPProxy":"127.0.0.1","HTTPPort":port,"HTTPSEnable":1,"HTTPSProxy":"127.0.0.1","HTTPSPort":port] } else { config.connectionProxyDictionary=[:] }
        let session = URLSession(configuration:config); defer { session.invalidateAndCancel() }
        var request = URLRequest(url:url); request.setValue("ClaudeLink/"+BuildInfo.version,forHTTPHeaderField:"User-Agent")
        let (bytes,response) = try await session.bytes(for:request)
        guard let response = response as? HTTPURLResponse else { throw ClientError.message("服务器响应无效。") }
        if response.expectedContentLength > limit && !truncate { throw ClientError.message("服务器响应超过大小限制。") }
        var data = Data(); for try await byte in bytes { if data.count >= limit { if truncate { break }; throw ClientError.message("服务器响应超过大小限制。") }; data.append(byte) }
        return WebResponse(code:response.statusCode,data:data,headers:response.allHeaderFields)
    }
    static func probe(_ address: String, port: Int) async -> WebResponse { (try? await fetch(address,port:port,truncate:true)) ?? WebResponse(code:0,data:Data(),headers:[:]) }
    static func object(_ response: WebResponse) -> [String:Any] { (try? JSONSerialization.jsonObject(with:response.data)) as? [String:Any] ?? [:] }
    static func traceIP(_ response: WebResponse) -> String { String(decoding:response.data,as:UTF8.self).components(separatedBy:"\n").first(where:{$0.hasPrefix("ip=")}).map { String($0.dropFirst(3)) } ?? "" }
    static func check(port: Int,tun: Bool,ipv6: Bool) async -> NetworkReport {
        async let general = probe("https://api.ipify.org?format=json",port:port)
        async let cf = probe("https://www.cloudflare.com/cdn-cgi/trace",port:port)
        async let claude = probe("https://claude.ai/cdn-cgi/trace",port:port)
        async let api = probe("https://api.anthropic.com/v1/models",port:port)
        async let website = probe("https://claude.ai",port:port)
        let (g,c,a,p,w) = await (general,cf,claude,api,website)
        var report = NetworkReport(); report.generalIP = object(g)["ip"] as? String ?? ""; report.cloudflareIP=traceIP(c); report.claudeIP=traceIP(a); report.apiCode=p.code; report.webCode=w.code; report.tun=tun; report.ipv6Disabled=ipv6
        if report.validIP {
            let geo = await probe("https://ipwho.is/"+report.generalIP,port:port), value = object(geo)
            report.country=value["country_code"] as? String ?? ""; report.city=value["city"] as? String ?? ""; report.zone=(value["timezone"] as? [String:Any])?["id"] as? String ?? ""
        }
        report.zoneMatches = !report.zone.isEmpty && TimeZone.current.identifier == report.zone
        report.apiFailure = p.code == 0 ? "网络请求未完成" : ""
        rows(&report); return report
    }
    static func rows(_ report: inout NetworkReport) {
        func ip(_ name: String,_ value: String,_ detail: String) -> CheckRow { CheckRow(label:name,value:value.isEmpty ? "未验证" : value,detail:detail,level:value.isEmpty ? .warning : .success) }
        report.rows = [ip("普通出口",report.generalIP,"通过本次 HTTP 代理独立检测"),ip("Cloudflare 出口",report.cloudflareIP,"Cloudflare trace"),ip("Claude 出口",report.claudeIP,"端点不提供 trace 时显示为未验证"),CheckRow(label:"三个 IPv4 出口",value:report.sameIPv4 ? "一致" : "不一致或未验证",detail:"严格预检项目",level:report.sameIPv4 ? .success : .warning),CheckRow(label:"Anthropic API",value:report.apiCode > 0 ? "HTTP \(report.apiCode)" : "请求失败",detail:"HTTPS 可达与账户授权分别判断；401 未验证授权",level:report.apiReachable ? .success : .error),CheckRow(label:"Claude 网页",value:report.webCode > 0 ? "HTTP \(report.webCode)" : "未验证",detail:"网页检查与 API 连通性分别显示",level:(200..<400).contains(report.webCode) ? .success : .warning),CheckRow(label:"出口地区",value:report.country.isEmpty ? "未验证" : report.country+" · "+report.city,detail:"第三方 IP 地理位置，仅供参考",level:report.country.isEmpty ? .pending : .info),CheckRow(label:"系统时区",value:report.zoneMatches ? "已匹配" : "未匹配或未验证",detail:report.zone,level:report.zoneMatches ? .success : .warning)]
    }
}
