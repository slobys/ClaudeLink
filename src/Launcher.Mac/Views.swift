import AppKit
import SwiftUI

struct Card<Content:View>:View {
    @ViewBuilder var content:Content
    var body:some View { VStack(alignment:.leading,spacing:16) { content }.frame(maxWidth:.infinity,alignment:.leading).padding(22).background(Palette.color("Surface"),in:RoundedRectangle(cornerRadius:12)).overlay(RoundedRectangle(cornerRadius:12).stroke(Palette.color("Line"),lineWidth:1)) }
}
struct StatusBadge:View {
    var text:String; var level:Level
    var body:some View { Label(text,systemImage:level.symbol).font(.system(size:12,weight:.semibold)).foregroundStyle(level.color).padding(.horizontal,10).padding(.vertical,7).background(level.background,in:Capsule()) }
}
struct RootView:View {
    @ObservedObject var model:AppState
    let pages=[("工作台","square.grid.2x2"),("节点管理","point.3.connected.trianglepath.dotted"),("订阅管理","arrow.triangle.2.circlepath"),("环境设置","slider.horizontal.3"),("运行记录","list.bullet.rectangle")]
    var body:some View {
        HStack(spacing:0) {
            sidebar
            Rectangle().fill(Palette.color("Line")).frame(width:1)
            mainContent
        }
        .foregroundStyle(Palette.color("Ink"))
        .font(.system(size:14))
        .tint(Palette.color("Accent"))
        .sheet(isPresented:$model.showingNode) { nodeEditor }
        .sheet(isPresented:$model.showingSubscription) { subscriptionEditor }
        .alert("操作未完成",isPresented:Binding(get:{!model.error.isEmpty},set:{if !$0 { model.error="" }})) {
            Button("确定") { model.error="" }
        } message:{ Text(model.error) }
    }
    var sidebar:some View {
        VStack(alignment:.leading,spacing:6) {
            HStack(spacing:12) {
                Image(nsImage:NSImage(named:NSImage.applicationIconName) ?? NSImage()).resizable().frame(width:42,height:42)
                Text("Claude Link").font(.system(size:22,weight:.bold))
            }.padding(.bottom,32)
            ForEach(pages,id:\.0) { item in
                Button { model.page=item.0 } label: {
                    Label(item.0,systemImage:item.1)
                        .font(.system(size:15,weight:model.page == item.0 ? .semibold : .regular))
                        .frame(maxWidth:.infinity,alignment:.leading).padding(13)
                        .foregroundStyle(Palette.color(model.page == item.0 ? "Accent" : "Muted"))
                        .background(model.page == item.0 ? Palette.color("AccentSubtle") : Color.clear,in:RoundedRectangle(cornerRadius:8))
                }.buttonStyle(.plain)
            }
            Spacer()
            VStack(alignment:.leading,spacing:8) {
                Text("独立代理内核").fontWeight(.semibold)
                Text("sing-box 1.14.4 · "+BuildInfo.arch).font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
            }.padding(14).frame(maxWidth:.infinity,alignment:.leading).background(Palette.color("SurfaceAlt"),in:RoundedRectangle(cornerRadius:10))
            Text("v"+BuildInfo.version+"  ·  钥匙串加密存储").font(.system(size:11)).foregroundStyle(Palette.color("Muted")).padding(.top,12)
        }.padding(22).frame(width:224).background(Palette.color("Sidebar"))
    }
    var mainContent:some View {
        VStack(alignment:.leading,spacing:20) {
            HStack(alignment:.top) {
                VStack(alignment:.leading,spacing:8) {
                    Text(model.page).font(.system(size:28,weight:.bold))
                    Text(subtitle).foregroundStyle(Palette.color("Muted"))
                }
                Spacer()
                StatusBadge(text:model.connected ? "本地代理已启动" : "未连接",level:model.connected ? .success : .pending)
            }
            updateBanner
            if model.busy {
                HStack {
                    ProgressView().controlSize(.small)
                    Text(model.progress.isEmpty ? "正在处理…" : model.progress)
                    Spacer()
                    Button("取消检测") { model.operation?.cancel() }.disabled(model.operation == nil || !model.progress.contains("检测"))
                }.padding(14).background(Level.info.background,in:RoundedRectangle(cornerRadius:10))
            }
            if model.page == "运行记录" { logs }
            else {
                ScrollViewReader { reader in
                    ScrollView {
                        VStack(alignment:.leading,spacing:18) { pageContent }
                            .padding(.bottom,6).frame(maxWidth:.infinity,alignment:.leading)
                    }.scrollIndicators(.automatic).onChange(of:model.previewScrollCheck) { if !model.previewScrollCheck.isEmpty { reader.scrollTo("checks",anchor:model.previewScrollCheck == "top" ? .top : .bottom) } }
                }
            }
            HStack {
                Text(model.connected ? "会话保持连接；使用结束后点击断开。" : "就绪 · 添加节点或订阅即可开始").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
                Spacer()
                Button("恢复临时设置") { model.disconnect() }.disabled(model.busy)
            }
        }.padding(28).background(Palette.color("Background"))
    }
    @ViewBuilder var updateBanner:some View {
        if let offer=model.offer {
            HStack {
                Label("新版本 "+offer.manifest.Version+" 可用",systemImage:"arrow.down.circle").fontWeight(.semibold)
                Spacer()
                if model.updateBusy {
                    ProgressView().controlSize(.small)
                    Button("取消下载") { model.updateTask?.cancel() }.disabled(model.updateTask == nil)
                } else if model.downloaded != nil {
                    Button("安装并重启") { model.installUpdate() }.buttonStyle(.borderedProminent)
                } else {
                    Button("下载更新") { model.downloadUpdate() }.buttonStyle(.borderedProminent)
                }
            }.padding(14).background(Level.info.background,in:RoundedRectangle(cornerRadius:10))
        }
    }
    @ViewBuilder var pageContent:some View {
        switch model.page {
        case "工作台": dashboard
        case "节点管理": nodes
        case "订阅管理": subscriptions
        default: settings
        }
    }
    var subtitle:String { switch model.page { case "工作台":"选择节点，准备网络并打开 Claude。"; case "节点管理":"管理手动添加及订阅导入的节点。"; case "订阅管理":"更新订阅，失败时保留原有节点。"; case "环境设置":"选择本次会话的代理、客户端和临时环境。"; default:"按时间查看操作结果与恢复状态。" } }
    var dashboard:some View {
        Group {
            Card {
                Text("准备一次工作会话").font(.system(size:21,weight:.semibold)); Text("官方桌面客户端使用 TUN；CLI 可使用独立进程代理。").foregroundStyle(Palette.color("Muted"))
                VStack(alignment:.leading,spacing:9) { Text("使用节点").fontWeight(.semibold); HStack { Picker("使用节点",selection:Binding(get:{model.selectedNode?.Id ?? ""},set:{model.settings.SelectedNodeId=$0})) { if model.settings.Nodes.isEmpty { Text("请先添加节点").tag("") }; ForEach(model.settings.Nodes) { Text($0.display).tag($0.Id) } }.labelsHidden().frame(maxWidth:.infinity).disabled(model.connected || model.busy); Button("添加节点") { model.editingNode=nil; model.editor=""; model.editorName=""; model.showingNode=true }.disabled(model.connected || model.busy) } }
                VStack(alignment:.leading,spacing:9) { Text("项目文件夹").fontWeight(.semibold); HStack { TextField("项目文件夹",text:$model.settings.ProjectDirectory).textFieldStyle(.roundedBorder); Button("浏览…") { model.chooseDirectory() } } }
                Toggle("TUN 虚拟网卡",isOn:$model.settings.TunEnabled).disabled(model.settings.LaunchTarget == "desktop" || model.connected || model.busy); if model.settings.LaunchTarget == "desktop" { Text("桌面客户端需要 TUN，连接时按系统提示授权。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }
                HStack { Button(model.settings.LaunchTarget == "desktop" ? "准备并打开 Claude 桌面客户端" : "准备并启动 Claude Code CLI") { model.run(true) }.buttonStyle(.borderedProminent); Button(model.connected ? "重新检测" : "连接并检测") { model.run(false) }; Button("断开") { model.disconnect() }.disabled(!model.connected) }.disabled(model.busy)
            }
            HStack(spacing:12) { metric("本地代理",model.connected ? "127.0.0.1:\(model.settings.ProxyPort)" : "未连接",model.connected ? .success : .pending); metric("连接模式",model.connected ? (model.session.tun ? "TUN 已启用" : "进程代理") : "待连接",model.connected ? .info : .pending); metric("API 连通性",model.report.map { $0.apiReachable ? "HTTPS 可达" : "未通过" } ?? "待检测",model.report.map { $0.apiReachable ? .success : .error } ?? .pending) }
            Card {
                HStack { Text("启动前检查").font(.system(size:19,weight:.semibold)); Spacer(); Button("浏览器核对") { model.browser() }.disabled(!model.connected) }
                if let report=model.report { Text(report.basicReady ? "API 基础网络可达；请分别查看网页检查与账户状态。" : "部分检测未通过，请查看下方结果。 ").foregroundStyle(report.basicReady ? Level.info.color : Level.error.color); ForEach(report.rows) { row in HStack(alignment:.top,spacing:12) { Image(systemName:row.level.symbol).foregroundStyle(row.level.color).frame(width:20); Text(row.label).frame(width:128,alignment:.leading); VStack(alignment:.leading,spacing:5) { Text(row.value).fontWeight(.semibold).foregroundStyle(row.level.color).textSelection(.enabled); Text(row.detail).font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }; Spacer(minLength:8); StatusBadge(text:row.level.title,level:row.level) }.padding(12).frame(maxWidth:.infinity,alignment:.leading).background(row.level.background,in:RoundedRectangle(cornerRadius:8)) } }
                else { Text("连接节点后显示出口、API、网页和时区的独立检测结果。").foregroundStyle(Palette.color("Muted")).padding(.vertical,12) }
            }.id("checks")
        }
    }
    func metric(_ label:String,_ value:String,_ level:Level) -> some View { VStack(alignment:.leading,spacing:9) { Text(label).font(.system(size:12)).foregroundStyle(Palette.color("Muted")); Text(value).font(.system(size:16,weight:.semibold)).foregroundStyle(level.color) }.frame(maxWidth:.infinity,alignment:.leading).padding(18).background(Palette.color("Surface"),in:RoundedRectangle(cornerRadius:12)).overlay(RoundedRectangle(cornerRadius:12).stroke(Palette.color("Line"))) }
    var nodes:some View { Card {
        HStack { Text("节点列表 · \(model.settings.Nodes.count)").font(.system(size:19,weight:.semibold)); Spacer(); Button("添加节点") { model.editingNode=nil; model.editor=""; model.editorName=""; model.showingNode=true }.buttonStyle(.borderedProminent) }
        if model.settings.Nodes.isEmpty { empty("还没有节点","粘贴分享链接，或在订阅管理中添加订阅。","point.3.connected.trianglepath.dotted") }
        ForEach(model.settings.Nodes) { node in HStack { VStack(alignment:.leading,spacing:5) { Text(node.Name).fontWeight(.semibold); Text(node.kind+((node.GroupId.isEmpty || node.GroupId == "manual") ? " · 手动添加" : " · 来自订阅")).font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }; Spacer(); if model.selectedNode?.Id == node.Id { StatusBadge(text:"当前选择",level:.info) } else { Button("选择") { model.settings.SelectedNodeId=node.Id; model.saveQuietly() } }; Button("编辑") { model.editingNode=node; model.editor=node.OutboundJson; model.editorName=node.Name; model.showingNode=true }; Button("删除",role:.destructive) { model.deleteNode(node) } }.padding(.vertical,12); Divider() }
    }.disabled(model.busy || model.connected) }
    var subscriptions:some View { Card {
        HStack { Text("订阅列表").font(.system(size:19,weight:.semibold)); Spacer(); Button("添加订阅") { model.editingSubscription=nil; model.editorName=""; model.editorURL=""; model.showingSubscription=true }.buttonStyle(.borderedProminent) }
        if model.settings.Subscriptions.isEmpty { empty("还没有订阅","添加服务商提供的 HTTP 或 HTTPS 订阅地址。","arrow.triangle.2.circlepath") }
        ForEach(model.settings.Subscriptions) { item in HStack { VStack(alignment:.leading,spacing:6) { Text(item.Name).fontWeight(.semibold); Text(item.UpdatedAt.map { "最近更新："+$0 } ?? "尚未更新").font(.system(size:12)).foregroundStyle(Palette.color("Muted")) }; Spacer(); Button("更新") { model.updateSubscription(item) }; Button("编辑") { model.editingSubscription=item; model.editorName=item.Name; model.editorURL=item.Url; model.showingSubscription=true }; Button("删除",role:.destructive) { model.deleteSubscription(item) } }.padding(.vertical,12); Divider() }
    }.disabled(model.busy || model.connected) }
    var settings:some View { Group {
        Card { Text("连接与客户端").font(.system(size:19,weight:.semibold)); LabeledContent("本地混合端口") { TextField("端口",value:$model.settings.ProxyPort,format:.number.grouping(.never)).frame(width:120).textFieldStyle(.roundedBorder) }; Text("仅监听 127.0.0.1，不修改 macOS 系统代理。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")); Picker("启动方式",selection:$model.settings.LaunchTarget) { Text("官方 Claude 桌面客户端").tag("desktop"); Text("Claude Code CLI（终端）").tag("cli") }.onChange(of:model.settings.LaunchTarget) { if model.settings.LaunchTarget == "desktop" { model.settings.TunEnabled=true } }
            LabeledContent("Claude 桌面应用") { TextField("留空自动查找 Claude.app",text:$model.settings.DesktopPath).textFieldStyle(.roundedBorder); Button("浏览…") { model.chooseProgram(desktop:true) } }; LabeledContent("CLI 可执行文件") { TextField("留空自动查找 claude",text:$model.settings.ClaudePath).textFieldStyle(.roundedBorder); Button("浏览…") { model.chooseProgram(desktop:false) } }
            Picker("进程与核对浏览器语言",selection:$model.settings.ProcessLanguage) { ForEach(["en-US","zh-CN","ja-JP","en-GB"],id:\.self) { Text($0).tag($0) } }; Picker("核对浏览器",selection:$model.settings.CheckBrowser) { Text("自动（优先 Chrome）").tag("auto"); Text("Chrome").tag("chrome"); Text("Edge").tag("edge") }; Text("独立核对窗口应用所选语言；官方 Claude 桌面应用使用其自身语言设置。").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
        }.disabled(model.busy || model.connected)
        Card { Text("临时系统环境").font(.system(size:19,weight:.semibold)); Toggle("本次临时关闭活动物理网络的 IPv6",isOn:$model.settings.DisableIPv6); Toggle("出口验证后临时匹配 macOS 系统时区",isOn:$model.settings.MatchTimeZone); Text("需要 TUN 管理员会话。修改前记录原状态，断开或启动器退出时恢复；不支持自动恢复的手动 IPv6 配置会在修改前停止。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")); Toggle("启用严格预检",isOn:$model.settings.StrictChecks); Text("严格预检要求三个 IPv4 出口一致、参考地区为 US、TUN 启用、IPv6 关闭、系统时区匹配以及网页可达。地区结果仅供参考。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")); Button("保存设置") { model.save() }.buttonStyle(.borderedProminent) }.disabled(model.busy || model.connected)
        Card { Text("软件更新").font(.system(size:19,weight:.semibold)); Toggle("启动时自动检查更新",isOn:$model.settings.AutoCheckUpdates).onChange(of:model.settings.AutoCheckUpdates) { model.saveQuietly() }; HStack { Text("当前版本 "+BuildInfo.version+" · macOS "+BuildInfo.arch).foregroundStyle(Palette.color("Muted")); Spacer(); Button("检查更新") { Task { await model.checkUpdate(manual:true) } }.disabled(model.updateBusy); Link("GitHub 发布页",destination:URL(string:"https://github.com/slobys/ClaudeLink/releases/latest")!) } }
    } }
    var logs:some View { VStack(alignment:.leading,spacing:16) { HStack { Text("仅记录操作与结果，不记录订阅地址、密码或完整分享链接。").font(.system(size:12)).foregroundStyle(Palette.color("Muted")); Spacer(); Button("清空显示") { model.logs=[] } }; ScrollViewReader { proxy in ScrollView { LazyVStack(alignment:.leading,spacing:14) { if model.logs.isEmpty { Text("暂无运行记录。").foregroundStyle(Palette.color("Muted")) }; ForEach(model.logs) { entry in HStack(alignment:.top,spacing:14) { Text(entry.timeText).monospacedDigit().font(.system(size:13)).foregroundStyle(Palette.color("Muted")).frame(width:72,alignment:.leading); Label(entry.level.title,systemImage:entry.level.symbol).font(.system(size:13,weight:.semibold)).foregroundStyle(entry.level.color).frame(width:64,alignment:.leading); Text(entry.message).font(.system(size:16)).lineSpacing(6).foregroundStyle(entry.level == .info ? Palette.color("Ink") : entry.level.color).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading) }.id(entry.id) } }.frame(maxWidth:.infinity,alignment:.topLeading).padding(20) }.frame(maxHeight:.infinity,alignment:.top).background(Palette.color("Surface"),in:RoundedRectangle(cornerRadius:12)).overlay(RoundedRectangle(cornerRadius:12).stroke(Palette.color("Line"))).onChange(of:model.logs.count) { if let last=model.logs.last { proxy.scrollTo(last.id,anchor:.bottom) } } } }.frame(maxHeight:.infinity,alignment:.top) }
    func empty(_ title:String,_ detail:String,_ symbol:String) -> some View { VStack(spacing:12) { Image(systemName:symbol).font(.system(size:28)).foregroundStyle(Palette.color("Muted")); Text(title).fontWeight(.semibold); Text(detail).foregroundStyle(Palette.color("Muted")) }.padding(.vertical,54).frame(maxWidth:.infinity) }
    var nodeEditor:some View { VStack(alignment:.leading,spacing:16) { Text(model.editingNode == nil ? "添加节点" : "编辑节点").font(.system(size:23,weight:.bold)); if model.editingNode != nil { TextField("节点名称",text:$model.editorName) }; Text("粘贴节点分享链接、sing-box JSON 或兼容的订阅配置。").foregroundStyle(Palette.color("Muted")); TextEditor(text:$model.editor).font(.system(size:14,design:.monospaced)).frame(height:230).overlay(RoundedRectangle(cornerRadius:6).stroke(Palette.color("Line"))); HStack { Button("从剪贴板粘贴") { model.editor=NSPasteboard.general.string(forType:.string) ?? "" }; Spacer(); Button("取消") { model.showingNode=false }; Button("保存节点") { model.importNode() }.buttonStyle(.borderedProminent).disabled(model.busy || model.editor.isEmpty) } }.padding(26).frame(width:620) }
    var subscriptionEditor:some View { VStack(alignment:.leading,spacing:16) { Text(model.editingSubscription == nil ? "添加订阅" : "编辑订阅").font(.system(size:23,weight:.bold)); TextField("订阅名称",text:$model.editorName); TextField("https:// 订阅地址",text:$model.editorURL); Text("更新失败时保留原节点；订阅地址加密保存。").foregroundStyle(Palette.color("Muted")); HStack { Spacer(); Button("取消") { model.showingSubscription=false }; Button("保存并更新") { model.updateSubscription() }.buttonStyle(.borderedProminent).disabled(model.busy || model.editorName.isEmpty || model.editorURL.isEmpty) } }.textFieldStyle(.roundedBorder).padding(26).frame(width:600) }
}
