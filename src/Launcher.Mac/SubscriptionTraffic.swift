import Foundation

struct SubscriptionTraffic:Codable,Hashable {
    var UploadBytes:Int64?; var DownloadBytes:Int64?; var TotalBytes:Int64?; var ExpiresUnixSeconds:Int64?; var WasEstimated:Bool?=nil
    var used:Double? { guard let up=UploadBytes,let down=DownloadBytes,up>=0,down>=0 else { return nil };return Double(up)+Double(down) }
    var hasQuota:Bool { used != nil && (TotalBytes ?? 0)>0 }
    var percent:Double { hasQuota ? min(100,max(0,used!/Double(TotalBytes!)*100)) : 0 }
    var expiry:Date? { guard let value=ExpiresUnixSeconds,value>0,value<=253402300799 else { return nil };return Date(timeIntervalSince1970:Double(value)) }
    var expired:Bool { expiry.map{$0<=Date()} ?? false }
    var tone:String { if expired || (hasQuota && used!>=Double(TotalBytes!)) { return "Error" }; if (hasQuota && percent>=80) || expiry.map({$0.timeIntervalSinceNow<=3*86400}) == true { return "Warning" }; return hasQuota ? "Accent" : "Pending" }
    var status:String { if expired { return "已过期" };if hasQuota && used!>=Double(TotalBytes!) { return "流量用尽" };if hasQuota && percent>=80 { return "余量较低" };if expiry.map({$0.timeIntervalSinceNow<=3*86400}) == true { return "即将到期" };return hasQuota ? "流量记录" : "信息未提供" }
    var usedText:String { hasQuota ? "已用 \(Self.bytes(used!)) / 共 \(Self.bytes(Double(TotalBytes!)))" : used.map{"已用 \(Self.bytes($0)) · 总量未提供"} ?? "服务商未提供流量信息" }
    var remainingText:String { hasQuota ? "剩余 "+Self.bytes(max(0,Double(TotalBytes!)-used!)) : "剩余流量未知" }
    var percentText:String { hasQuota ? (used!>Double(TotalBytes!) ? "已超出总量" : String(format:"%.1f%%",percent)) : "暂无比例" }
    var expiryText:String { guard let expiry else { return "到期时间未提供" };let format=DateFormatter();format.dateFormat="yyyy-MM-dd";return format.string(from:expiry)+" 到期" }
    var hint:String { WasEstimated == true ? "流量条按旧版汇总估算，更新订阅后获取精确数据。" : "流量与到期信息由服务商提供，不代表节点连通性。" }
    static func bytes(_ raw:Double) -> String { var value=raw;let units=["B","KB","MB","GB","TB","PB","EB"];var i=0;while value>=1024 && i<units.count-1 {value/=1024;i+=1};return String(format:"%.1f %@",value,units[i]) }
    static func legacy(_ text:String?) -> SubscriptionTraffic? {
        guard let text,text.count<4096,let regex=try? NSRegularExpression(pattern:#"^已用\s+([0-9]+[.,]?[0-9]*)\s*/\s*([0-9]+[.,]?[0-9]*)\s*GB$"#),let match=regex.firstMatch(in:text,range:NSRange(text.startIndex...,in:text)) else { return nil }
        let values=(1...2).compactMap { index -> Int64? in guard let range=Range(match.range(at:index),in:text),let value=Double(text[range].replacingOccurrences(of:",",with:".")),value.isFinite,value>=0 else { return nil };return Int64(exactly:(value*1073741824).rounded(.towardZero)) }
        guard values.count==2 else { return nil };return SubscriptionTraffic(UploadBytes:0,DownloadBytes:values[0],TotalBytes:values[1],WasEstimated:true)
    }
}

extension Subscription {
    var traffic:SubscriptionTraffic { Traffic ?? SubscriptionTraffic.legacy(Usage) ?? SubscriptionTraffic() }
    var updatedText:String {
        guard let UpdatedAt else { return "尚未更新" };let formatter=ISO8601DateFormatter();formatter.formatOptions=[.withInternetDateTime,.withFractionalSeconds]
        guard let date=formatter.date(from:UpdatedAt) ?? ISO8601DateFormatter().date(from:UpdatedAt) else { return "时间未验证" };let display=DateFormatter();display.dateFormat="MM-dd HH:mm";return display.string(from:date)
    }
}
