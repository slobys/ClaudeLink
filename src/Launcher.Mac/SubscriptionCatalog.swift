import SwiftUI

struct SubscriptionCatalog:View {
    @ObservedObject var model:AppState
    @State private var selected:String?
    private var chosen:Subscription? { model.settings.Subscriptions.first{$0.Id==selected} }
    private var locked:Bool { model.busy || model.connected || model.nodeTesting }
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {
                Button("添加订阅") {model.editingSubscription=nil;model.editorName="";model.editorURL="";model.showingSubscription=true}.buttonStyle(.borderedProminent)
                Button("更新选中") {if let chosen {model.updateSubscription(chosen)}}.disabled(chosen==nil)
                Button("编辑") {if let chosen {model.editingSubscription=chosen;model.editorName=chosen.Name;model.editorURL=chosen.Url;model.showingSubscription=true}}.disabled(chosen==nil)
                Button("删除订阅",role:.destructive) {if let chosen {model.deleteSubscription(chosen)}}.disabled(chosen==nil)
                Spacer()
            }.disabled(locked)
            if model.settings.Subscriptions.isEmpty {
                VStack(spacing:10) {Image(systemName:"tray").font(.system(size:28));Text("还没有订阅");Text("添加服务商提供的地址，流量与到期信息会在这里显示。").font(.system(size:12))}.frame(maxWidth:.infinity,minHeight:220).foregroundStyle(Palette.color("Muted"))
            }
            ForEach(model.settings.Subscriptions) { item in
                Button {selected=item.Id} label: {SubscriptionTile(item:item,count:model.settings.Nodes.filter{$0.GroupId==item.Id}.count,selected:selected==item.Id)}
                    .buttonStyle(.plain).disabled(locked).accessibilityLabel(Text(item.Name+"，"+item.traffic.usedText+"，"+item.traffic.remainingText+"，"+item.traffic.expiryText)).accessibilityValue(Text(selected==item.Id ? "已选" : "未选择"))
            }
            Text("\(model.settings.Subscriptions.count) 个订阅 · 流量由服务商提供，未提供时保留未知。地址与凭据在本机加密保存。").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
        }.onAppear {if chosen==nil {selected=model.settings.Subscriptions.first?.Id}}
        .onChange(of:model.settings.Subscriptions.map(\.Id)) { _,ids in if let selected,!ids.contains(selected) {self.selected=ids.first} }
    }
}
private struct SubscriptionTile:View {
    let item:Subscription;let count:Int;let selected:Bool
    private var traffic:SubscriptionTraffic {item.traffic}
    var body:some View {
        VStack(alignment:.leading,spacing:10) {
            HStack(alignment:.top) {
                VStack(alignment:.leading,spacing:5) {Text(item.Name).font(.system(size:17,weight:.semibold)).lineLimit(1).help(item.Name);Text("\(count) 个节点").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))}
                Spacer()
                Text(traffic.status).font(.system(size:11,weight:.semibold)).padding(.horizontal,8).padding(.vertical,4).foregroundStyle(Palette.color(traffic.tone)).background(Palette.color(traffic.tone=="Accent" ? "AccentSubtle" : traffic.tone+"Bg"),in:RoundedRectangle(cornerRadius:6))
                if selected {Image(systemName:"checkmark").foregroundStyle(Palette.color("Accent")).accessibilityHidden(true)}
            }
            HStack {Text(traffic.usedText).font(.system(size:14));Spacer();Text(traffic.percentText).font(.system(size:13,weight:.semibold))}.help(traffic.hint)
            ProgressView(value:traffic.percent,total:100).tint(Palette.color(traffic.tone)).accessibilityLabel("订阅流量使用比例").help(traffic.hint)
            HStack(alignment:.bottom) {
                VStack(alignment:.leading,spacing:5) {Text(traffic.remainingText).font(.system(size:13,weight:.semibold));Text(traffic.expiryText).font(.system(size:12)).foregroundStyle(Palette.color("Muted"))}
                Spacer()
                VStack(alignment:.trailing,spacing:5) {Text("最近更新").font(.system(size:11));Text(item.updatedText).font(.system(size:12))}.foregroundStyle(Palette.color("Muted"))
            }
        }.padding(18).frame(maxWidth:.infinity,alignment:.leading).foregroundStyle(Palette.color("Ink")).background(Palette.color(selected ? "AccentSubtle" : "Surface"),in:RoundedRectangle(cornerRadius:12)).overlay(RoundedRectangle(cornerRadius:12).stroke(Palette.color(selected ? "Accent" : "Line"),lineWidth:1))
    }
}
