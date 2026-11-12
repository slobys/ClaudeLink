import AppKit
import SwiftUI

struct NodeCatalog:View {
    @ObservedObject var model:AppState
    private var filtered:[ProxyNode] {
        model.settings.Nodes.filter { node in
            let region=NodeRegion.resolve(node,model.settings.Nodes), group=model.settings.Subscriptions.first(where:{$0.Id == node.GroupId})?.Name ?? "手动添加"
            let searchable=[node.Name,node.kind,group,region?.Name ?? "未识别地区",region?.Code ?? "",ProxyChain.path(node,model.settings.Nodes)].joined(separator:" ")
            return (model.nodeGroup == "all" || node.GroupId == model.nodeGroup) && (model.nodeRegion == "all" || (region?.Code ?? "unknown") == model.nodeRegion) && (model.nodeSearch.isEmpty || searchable.localizedCaseInsensitiveContains(model.nodeSearch))
        }
    }
    private var regions:[NodeRegion] { Array(Set(model.settings.Nodes.compactMap{NodeRegion.resolve($0,model.settings.Nodes)})).sorted{$0.Name < $1.Name} }
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            actions
            filters
            if filtered.isEmpty { emptyState }
            tiles
            footer
        }
    }
    private var actions:some View {
        HStack {
                Button("添加节点") { model.editingNode=nil; model.editor=""; model.editorName=""; model.showingNode=true }.buttonStyle(.borderedProminent)
                Button("创建链式代理") { model.editChain() }
                Spacer()
                Button("编辑") { if let node=model.selectedNode { if node.isChain { model.editChain(node) } else { model.editingNode=node; model.editor=node.OutboundJson; model.editorName=node.Name; model.showingNode=true } } }
                Button("删除",role:.destructive) { if let node=model.selectedNode { model.deleteNode(node) } }
        }.disabled(model.busy || model.connected)
    }
    private var filters:some View {
        HStack(spacing:12) {
                InputField("搜索节点",text:$model.nodeSearch,prompt:"搜索名称、地区、协议或分组")
                Picker("地区",selection:$model.nodeRegion) { Text("全部地区").tag("all"); ForEach(regions,id:\.Code) { Text($0.Name).tag($0.Code) }; Text("未识别地区").tag("unknown") }.labelsHidden().frame(width:145)
                Picker("分组",selection:$model.nodeGroup) { Text("全部分组").tag("all"); Text("手动添加").tag("manual"); ForEach(model.settings.Subscriptions) { Text($0.Name).tag($0.Id) } }.labelsHidden().frame(width:155)
        }
    }
    private var emptyState:some View {
                VStack(spacing:10) { Image(systemName:"magnifyingglass").font(.system(size:28)); Text(model.settings.Nodes.isEmpty ? "还没有节点，先添加节点或订阅。" : "没有匹配节点，请更换筛选条件。"); if !model.nodeSearch.isEmpty || model.nodeRegion != "all" || model.nodeGroup != "all" { Button("清空筛选") { model.nodeSearch=""; model.nodeRegion="all"; model.nodeGroup="all" } } }.frame(maxWidth:.infinity,minHeight:220).foregroundStyle(Palette.color("Muted"))
    }
    private var tiles:some View {
        LazyVGrid(columns:[GridItem(.adaptive(minimum:230,maximum:370),spacing:12)],alignment:.leading,spacing:12) {
                ForEach(filtered) { node in
                    Button { model.selectNode(node) } label: { NodeTile(node:node,model:model) }.buttonStyle(.plain).disabled(model.busy || model.connected)
                        .accessibilityLabel(node.Name+"，"+(NodeRegion.resolve(node,model.settings.Nodes)?.Name ?? "未识别地区")+"，"+node.kind)
                }
        }
    }
    private var footer:some View {
        HStack { Text("显示 \(filtered.count) / \(model.settings.Nodes.count) · \(model.settings.Nodes.filter{$0.isChain}.count) 条链路"); Spacer(); Text("点击卡片选择，切换前请先断开。") }.font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
    }
}
private struct NodeTile:View {
    var node:ProxyNode; @ObservedObject var model:AppState
    var current:Bool { model.selectedNode?.Id == node.Id }
    var invalid:Bool { ProxyChain.invalid(node,model.settings.Nodes) }
    var region:NodeRegion? { NodeRegion.resolve(node,model.settings.Nodes) }
    var body:some View {
        VStack(alignment:.leading,spacing:9) {
            HStack(spacing:9) {
                if let code=region?.Code.lowercased(), let image=NSImage(contentsOf:Bundle.main.resourceURL!.appendingPathComponent("Flags/"+code+".png")) { Image(nsImage:image).resizable().scaledToFit().frame(width:26,height:24).accessibilityHidden(true) } else { Image(systemName:"globe").font(.system(size:22)).foregroundStyle(Palette.color("Muted")) }
                Text(node.Name.replacingOccurrences(of:region?.emoji ?? "",with:"")).font(.system(size:15,weight:.semibold)).lineLimit(1).truncationMode(.tail).help(node.Name)
                Spacer(minLength:2); Image(systemName:current ? "checkmark.circle.fill" : "circle").foregroundStyle(Palette.color(current ? "Accent" : "Muted"))
            }
            Text(region?.Name ?? "未识别地区").font(.system(size:12)).foregroundStyle(Palette.color("Muted")).help("按节点名称识别；实际出口以连接检测为准。")
            Text(node.isChain ? ProxyChain.path(node,model.settings.Nodes) : model.settings.Subscriptions.first(where:{$0.Id == node.GroupId})?.Name ?? "手动添加").font(.system(size:12)).lineLimit(1).foregroundStyle(Palette.color("Muted"))
            Spacer(minLength:0)
            HStack { Text(node.isChain ? "链式 · 2 跳" : node.kind).font(.system(size:12,weight:.semibold)); Spacer(); Text(invalid ? "节点失效" : current ? "当前选择" : "待连接").font(.system(size:11,weight:.semibold)).foregroundStyle(Palette.color(invalid ? "Error" : current ? "Accent" : "Pending")).padding(.horizontal,8).padding(.vertical,5).background(Palette.color(invalid ? "ErrorBg" : current ? "AccentSubtle" : "PendingBg"),in:RoundedRectangle(cornerRadius:7)) }
        }.frame(maxWidth:.infinity,minHeight:112,maxHeight:112,alignment:.leading).padding(16).foregroundStyle(Palette.color("Ink")).background(Palette.color(current ? "AccentSubtle" : "Surface"),in:RoundedRectangle(cornerRadius:12)).overlay(RoundedRectangle(cornerRadius:12).stroke(Palette.color(current ? "Accent" : "Line"),lineWidth:1))
    }
}
struct ChainEditor:View {
    @ObservedObject var model:AppState
    @State private var name:String
    @State private var entry:String
    @State private var exit:String
    @State private var error=""
    init(model:AppState) {
        self.model=model;let old=model.editingChain
        _name=State(initialValue:old?.Name ?? "住宅出口链路")
        let entry=old?.ChainNodeIds.first ?? model.settings.Nodes.first(where:{$0.Id == model.settings.SelectedNodeId && !$0.isChain})?.Id ?? model.settings.Nodes.first(where:{!$0.isChain})?.Id ?? ""
        let exit=old?.ChainNodeIds.last ?? ""
        _entry=State(initialValue:model.settings.Nodes.contains(where:{$0.Id == entry}) ? entry : "")
        _exit=State(initialValue:model.settings.Nodes.contains(where:{$0.Id == exit}) ? exit : "")
        _error=State(initialValue:old.map{ProxyChain.invalid($0,model.settings.Nodes)} == true ? "当前链路已失效，请重新选择入口和出口。" : "")
    }
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            Text(model.editingChain == nil ? "创建链式代理" : "编辑链式代理").font(.system(size:24,weight:.semibold))
            Text("先连接入口，再通过住宅 IP 或其他节点访问目标网站。").foregroundStyle(Palette.color("Muted"))
            VStack(alignment:.leading,spacing:8) { Text("链路名称").fontWeight(.semibold); InputField("链路名称",text:$name) }
            VStack(alignment:.leading,spacing:8) { Text("入口节点").fontWeight(.semibold); Picker("入口节点",selection:$entry) { Text("选择入口").tag(""); ForEach(model.settings.Nodes.filter{!$0.isChain}) { Text($0.display).tag($0.Id) } }.labelsHidden(); Text("入口负责先连通出口节点。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }
            VStack(alignment:.leading,spacing:8) { Text("最终出口").fontWeight(.semibold); Picker("最终出口",selection:$exit) { Text("选择出口").tag(""); ForEach(model.settings.Nodes.filter(ProxyChain.supportsExit)) { Text($0.display).tag($0.Id) } }.labelsHidden(); Text("目标网站看到的是出口 IP；住宅代理通常使用 HTTP / SOCKS。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }
            Text("本机 → \(model.settings.Nodes.first(where:{$0.Id == entry})?.Name ?? "选择入口") → \(model.settings.Nodes.first(where:{$0.Id == exit})?.Name ?? "选择出口") → 目标网站").frame(maxWidth:.infinity,alignment:.leading).padding(14).background(Palette.color("AccentSubtle"),in:RoundedRectangle(cornerRadius:10))
            Text("HTTP 出口用于网页和 API 等 TCP 流量；UDP 需要节点额外支持。").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
            if !error.isEmpty { Text(error).foregroundStyle(Palette.color("Error")) }
            HStack { Spacer(); Button("取消") { model.showingChain=false }.keyboardShortcut(.cancelAction); Button("保存链式代理") { do { try model.saveChain(name,entry,exit) } catch { self.error=error.localizedDescription } }.buttonStyle(.borderedProminent).keyboardShortcut(.defaultAction) }
        }.padding(28).frame(width:600).foregroundStyle(Palette.color("Ink")).background(Palette.color("Surface"))
    }
}
