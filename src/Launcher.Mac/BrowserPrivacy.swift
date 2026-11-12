import Foundation

enum BrowserPrivacy {
    static let checkURL="https://ip.net.coffee/claude/"
    static func script(origin:String="https://ip.net.coffee") throws -> String {
        let path=Bundle.main.resourceURL!.appendingPathComponent("browser-privacy.js")
        let quoted=String(decoding:try JSONSerialization.data(withJSONObject:origin,options:.fragmentsAllowed),as:UTF8.self)
        return try String(contentsOf:path,encoding:.utf8).replacingOccurrences(of:"__CLAUDELINK_ORIGIN__",with:quoted)
    }
    static func configure(_ profile:URL) async throws -> Bool {
        let deadline=Date().addingTimeInterval(35)
        var port:Int?
        while port == nil && Date() < deadline {
            if let text=try? String(contentsOf:profile.appendingPathComponent("DevToolsActivePort"),encoding:.utf8) {
                let lines=text.components(separatedBy:"\n")
                if lines.count > 1, lines[1].hasPrefix("/devtools/browser/"), let value=Int(lines[0]), (1...65535).contains(value) { port=value }
            }
            if port == nil { try await Task.sleep(for:.milliseconds(100)) }
        }
        guard let port else { throw ClientError.message("无法初始化专用核对窗口。") }
        var address:URL?
        while address == nil && Date() < deadline {
            let response=try await Web.fetch("http://127.0.0.1:\(port)/json/list",limit:262144,timeout:3)
            let pages=(try? JSONSerialization.jsonObject(with:response.data)) as? [[String:Any]] ?? []
            if let page=pages.first(where:{$0["type"] as? String == "page" && $0["url"] as? String == "about:blank"}), let raw=page["webSocketDebuggerUrl"] as? String { address=URL(string:raw) }
            if address == nil { try await Task.sleep(for:.milliseconds(100)) }
        }
        guard let address else { throw ClientError.message("未找到本次专用核对标签。") }
        let config=URLSessionConfiguration.ephemeral; config.connectionProxyDictionary=[:]
        let session=URLSession(configuration:config), socket=session.webSocketTask(with:address)
        socket.resume()
        let timeout=Task { try? await Task.sleep(for:.seconds(35)); if !Task.isCancelled { socket.cancel(with:.goingAway,reason:nil) } }
        var retained=false
        defer { timeout.cancel(); if !retained { socket.cancel(with:.normalClosure,reason:nil); session.invalidateAndCancel() } }
        var id=0
        func send(_ method:String,_ parameters:[String:Any]) async throws -> [String:Any] {
            id += 1; let requestID=id
            let bytes=try JSONSerialization.data(withJSONObject:["id":requestID,"method":method,"params":parameters])
            try await socket.send(.data(bytes))
            while true {
                let message=try await socket.receive(); let data:Data
                switch message { case .data(let bytes): data=bytes; case .string(let text): data=Data(text.utf8); @unknown default: throw ClientError.message("核对窗口响应无效。") }
                guard data.count <= 1048576 else { throw ClientError.message("核对窗口响应过大。") }
                let response=try JSONSerialization.jsonObject(with:data) as? [String:Any] ?? [:]
                if response["id"] as? Int != requestID { continue }
                if response["error"] != nil { if method == "Runtime.evaluate" { return [:] }; throw ClientError.message("无法设置核对页的隐私显示。") }
                return response["result"] as? [String:Any] ?? [:]
            }
        }
        _=try await send("Page.enable",[:])
        _=try await send("Page.addScriptToEvaluateOnNewDocument",["source":try script()])
        _=try await send("Page.navigate",["url":checkURL])
        while Date() < deadline {
            let response=try await send("Runtime.evaluate",["expression":"document.documentElement.dataset.claudeLinkIpHidden === 'true' && document.getElementById('ipMaskToggle')?.checked === true","returnByValue":true])
            if let result=response["result"] as? [String:Any], result["value"] as? Bool == true {
                retained=true
                Task { defer { socket.cancel(with:.normalClosure,reason:nil); session.invalidateAndCancel() }; do { while true { _=try await socket.receive() } } catch { } }
                return true
            }
            try await Task.sleep(for:.milliseconds(200))
        }
        return false
    }
}
