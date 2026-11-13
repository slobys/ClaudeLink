import Foundation

struct WebResponse { var code: Int; var data: Data; var headers: [AnyHashable:Any]; var failure: String = "" }
enum Web {
    static let claudeLoginURL="https://claude.ai/login"
    static let requirementsURL="https://code.claude.com/docs/en/network-config"
    static func fetch(_ address: String, port: Int? = nil, limit: Int = 65536, timeout: Double = 15, truncate: Bool = false) async throws -> WebResponse {
        guard let url = URL(string:address), ["https","http"].contains(url.scheme), url.user == nil, url.password == nil else { throw ClientError.message("请输入有效的 HTTP 或 HTTPS 地址。") }
        let config = URLSessionConfiguration.ephemeral; config.timeoutIntervalForRequest=timeout; config.timeoutIntervalForResource=timeout
        if let port { config.connectionProxyDictionary=["HTTPEnable":1,"HTTPProxy":"127.0.0.1","HTTPPort":port,"HTTPSEnable":1,"HTTPSProxy":"127.0.0.1","HTTPSPort":port] } else { config.connectionProxyDictionary=[:] }
        let session = URLSession(configuration:config); defer { session.invalidateAndCancel() }
        return try await withTaskCancellationHandler {
        var request = URLRequest(url:url); request.setValue("ClaudeLink/"+BuildInfo.version,forHTTPHeaderField:"User-Agent")
        let (bytes,response) = try await session.bytes(for:request)
        guard let response = response as? HTTPURLResponse else { throw ClientError.message("服务器响应无效。") }
        if response.expectedContentLength > limit && !truncate { throw ClientError.message("服务器响应超过大小限制。") }
        var data = Data(); for try await byte in bytes { if data.count & 16383 == 0 { try Task.checkCancellation() }; if data.count >= limit { if truncate { break }; throw ClientError.message("服务器响应超过大小限制。") }; data.append(byte) }
        return WebResponse(code:response.statusCode,data:data,headers:response.allHeaderFields)
        } onCancel: { session.invalidateAndCancel() }
    }
    static func probe(_ address: String, port: Int, timeout:Double = 15) async -> WebResponse {
        do { return try await fetch(address,port:port,timeout:timeout,truncate:true) }
        catch {
            let reason:String
            if let e=error as? URLError {
                switch e.code {
                case .serverCertificateHasBadDate,.serverCertificateUntrusted,.serverCertificateHasUnknownRoot,.serverCertificateNotYetValid,.secureConnectionFailed: reason="TLS 握手或证书校验失败"
                case .cannotFindHost,.dnsLookupFailed: reason="DNS 解析失败"
                case .timedOut: reason="请求超时"
                case .cancelled: reason="请求已取消"
                default: reason="连接失败或网络不可用"
                }
            } else { reason="网络请求未完成" }
            return WebResponse(code:0,data:Data(),headers:[:],failure:reason)
        }
    }
    static func object(_ response: WebResponse) -> [String:Any] { (try? JSONSerialization.jsonObject(with:response.data)) as? [String:Any] ?? [:] }
    static func traceIP(_ response: WebResponse) -> String { String(decoding:response.data,as:UTF8.self).components(separatedBy:"\n").first(where:{$0.hasPrefix("ip=")}).map { String($0.dropFirst(3)) } ?? "" }
    static func check(port: Int,tun: Bool,ipv6: Bool) async -> NetworkReport {
        async let general = probe("https://api.ipify.org?format=json",port:port)
        async let cf = probe("https://www.cloudflare.com/cdn-cgi/trace",port:port)
        async let claude = probe("https://claude.ai/cdn-cgi/trace",port:port)
        async let api = probe("https://api.anthropic.com/v1/models",port:port)
        async let website = probe(claudeLoginURL,port:port)
        async let signIn = probe("https://claude.com/",port:port,timeout:5)
        async let oauth = probe("https://platform.claude.com/",port:port,timeout:5)
        let (g,c,a,p,w) = await (general,cf,claude,api,website)
        var report = NetworkReport(); report.generalIP = object(g)["ip"] as? String ?? ""; report.cloudflareIP=traceIP(c); report.claudeIP=traceIP(a); report.apiCode=p.code; report.webCode=w.code; report.tun=tun; report.ipv6Disabled=ipv6
        let (s,o)=await(signIn,oauth); report.signInCode=s.code; report.oauthCode=o.code; report.signInFailure=s.failure; report.oauthFailure=o.failure
        if report.validIP {
            let geo = await probe("https://ipwho.is/"+report.generalIP,port:port), value = object(geo)
            report.country=value["country_code"] as? String ?? ""; report.city=value["city"] as? String ?? ""; report.zone=(value["timezone"] as? [String:Any])?["id"] as? String ?? ""
        }
        report.zoneMatches = !report.zone.isEmpty && TimeZone.current.identifier == report.zone
        report.apiFailure = p.failure
        rows(&report); return report
    }
    static func rows(_ report: inout NetworkReport) {
        func ip(_ name: String,_ value: String,_ detail: String) -> CheckRow { CheckRow(label:name,value:value.isEmpty ? "未验证" : value,detail:detail,level:value.isEmpty ? .warning : .success) }
        report.rows = [
            ip("普通出口",report.generalIP,"通过本次 HTTP 代理独立检测"),
            ip("Cloudflare 出口",report.cloudflareIP,"Cloudflare trace"),
            ip("Claude 出口",report.claudeIP,"端点不提供 trace 时显示为未验证"),
            CheckRow(label:"三个 IPv4 出口",value:report.sameIPv4 ? "一致" : "不一致或未验证",detail:"自定义一致性检查，不是官方账户风控结论",level:report.sameIPv4 ? .success : .warning),
            CheckRow(label:"Anthropic API",value:report.apiCode > 0 ? "HTTP \(report.apiCode)" : "请求失败",detail:report.apiCode == 0 ? report.apiFailure : "HTTPS 可达与账户授权分别判断；401 未验证授权",level:report.apiReachable ? .success : .error),
            CheckRow(label:"Claude 登录页",value:report.webCode > 0 ? "HTTP \(report.webCode)"+(report.webCode == 403 ? " · 需浏览器确认" : "") : "未验证",detail:"https://claude.ai/login · 自动探测不验证账户；403 不代表浏览器无法打开",level:(200..<400).contains(report.webCode) ? .success : .warning),
            CheckRow(label:"登录跳转域名",value:report.signInCode > 0 ? "HTTP \(report.signInCode)" : "未验证",detail:"claude.com · "+(report.signInCode == 0 ? report.signInFailure : "官方登录会访问此域名；本次不验证账户登录"),level:(200..<400).contains(report.signInCode) ? .success : .warning),
            CheckRow(label:"OAuth 服务域名",value:report.oauthCode > 0 ? "HTTP \(report.oauthCode)" : "未验证",detail:"platform.claude.com · "+(report.oauthCode == 0 ? report.oauthFailure : "令牌交换使用此域名；本次未发送登录凭据或令牌"),level:(200..<400).contains(report.oauthCode) ? .success : .warning),
            CheckRow(label:"HTTPS / TLS 校验",value:[report.apiCode,report.webCode,report.signInCode,report.oauthCode].allSatisfy({$0>0}) ? "核心域名已完成握手" : "部分域名未验证",detail:"仅当前检测进程的证书校验；403 不等于证书失败或账户封禁",level:[report.apiCode,report.webCode,report.signInCode,report.oauthCode].allSatisfy({$0>0}) ? .success : .warning),
            CheckRow(label:"账户、套餐与额度",value:"未验证",detail:"请在官方客户端确认；HTTPS 可达或 401 不能证明账户授权",level:.pending),
            CheckRow(label:"出口地区",value:report.country.isEmpty ? "未验证" : report.country+" · "+report.city,detail:"第三方 IP 定位，仅供自定义预设参考；不判定使用资格",level:report.country.isEmpty ? .pending : .info),
            CheckRow(label:"系统时区",value:report.zoneMatches ? "已匹配" : "未匹配或未验证",detail:"自定义兼容预设 · "+report.zone,level:report.zoneMatches ? .success : .warning)
        ]
    }
}
