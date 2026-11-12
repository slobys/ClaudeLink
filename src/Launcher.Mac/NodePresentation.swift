import Foundation

struct NodeRegion:Decodable,Hashable {
    var Code:String; var Name:String; var Names:String; var Short:String
    static let all:[NodeRegion] = {
        guard let url=Bundle.main.url(forResource:"regions",withExtension:"json"), let data=try? Data(contentsOf:url) else { return [] }
        return (try? JSONDecoder().decode([NodeRegion].self,from:data)) ?? []
    }()
    var emoji:String { String(String.UnicodeScalarView(Code.unicodeScalars.compactMap{UnicodeScalar(0x1f1e6+$0.value-65)})) }
    static func fromName(_ name:String) -> NodeRegion? {
        all.first { name.contains($0.emoji) || name.range(of:$0.Names,options:[.regularExpression,.caseInsensitive]) != nil || name.range(of:"(?<![A-Za-z])(?:"+$0.Short+")(?=[^A-Za-z]|$)",options:[.regularExpression,.caseInsensitive]) != nil }
    }
    static func resolve(_ node:ProxyNode,_ nodes:[ProxyNode]) -> NodeRegion? {
        if node.isChain { return nodes.first(where:{$0.Id == node.ChainNodeIds.last}).flatMap{fromName($0.Name)} }
        return fromName(node.Name)
    }
}
enum ProxyChain {
    static func supportsExit(_ node:ProxyNode) -> Bool { !node.isChain && ["HTTP","SOCKS","VLESS","VMESS","TROJAN","SHADOWSOCKS","ANYTLS"].contains(node.kind) }
    static func create(_ name:String,_ entry:String,_ exit:String,_ nodes:[ProxyNode],id:String?=nil) throws -> ProxyNode {
        let name=name.trimmingCharacters(in:.whitespacesAndNewlines)
        guard !name.isEmpty && name.count <= 120 else { throw ClientError.message("请输入 1—120 字的链路名称。") }
        guard entry != exit else { throw ClientError.message("入口和出口不能是同一个节点。") }
        guard let first=nodes.first(where:{$0.Id == entry}), let last=nodes.first(where:{$0.Id == exit}), !first.isChain, supportsExit(last), id != entry, id != exit else { throw ClientError.message("入口或出口已失效，请重新选择支持 TCP 的节点。") }
        return ProxyNode(Id:id ?? UUID().uuidString,Name:name,GroupId:"manual",OutboundJson:"{}",ChainNodeIds:[entry,exit])
    }
    static func path(_ node:ProxyNode,_ nodes:[ProxyNode]) -> String { node.ChainNodeIds.map { id in nodes.first(where:{$0.Id == id})?.Name ?? "节点已失效" }.joined(separator:" → ") }
    static func invalid(_ node:ProxyNode,_ nodes:[ProxyNode]) -> Bool {
        guard node.isChain else { return false }
        guard node.ChainNodeIds.count == 2 else { return true }
        return (try? create(node.Name,node.ChainNodeIds[0],node.ChainNodeIds[1],nodes,id:node.Id)) == nil
    }
}
