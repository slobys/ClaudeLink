import AppKit
import SwiftUI

@MainActor final class AppState: ObservableObject {
    @Published var settings=Settings(); @Published var page="工作台"; @Published var logs:[LogEntry]=[]; @Published var report:NetworkReport?
    @Published var busy=false; @Published var connected=false; @Published var progress=""; @Published var error=""; @Published var offer:UpdateOffer?; @Published var downloaded:URL?; @Published var updateBusy=false; @Published var previewScrollCheck=""
    @Published var editor=""; @Published var editorName=""; @Published var editorURL=""; @Published var editingNode:ProxyNode?; @Published var editingSubscription:Subscription?; @Published var showingNode=false; @Published var showingSubscription=false
    @Published var launchConfirmation:LaunchConfirmation?; private var launchDecision:CheckedContinuation<Bool,Never>?
    let session=Session(); var operation:Task<Void,Never>?; var monitor:Task<Void,Never>?; var updateTask:Task<Void,Never>?; var storageReadFailed=false; var previewMode=false
    var selectedNode:ProxyNode? { settings.Nodes.first { $0.Id == settings.SelectedNodeId } ?? settings.Nodes.first }
    init(preview:Bool = false) {
        if preview { fillPreview(); return }
        if CommandLine.arguments.contains("--ui-smoke") || CommandLine.arguments.contains("--shutdown-smoke") { AppPaths.root=FileManager.default.temporaryDirectory.appendingPathComponent("ClaudeLink-ui-"+UUID().uuidString); fillPreview(); return }
        do { if try !SingleInstance.acquire() { exit(0) } }
        catch { let alert=NSAlert(); alert.messageText="无法启动 Claude Link"; alert.informativeText="请检查配置目录权限后重试。"; alert.runModal(); exit(1) }
        do { settings=try SecureSettings.load(); log("启动器已就绪。节点与订阅使用登录钥匙串密钥加密保存。") } catch { storageReadFailed=true; self.error="读取配置失败，原文件已保留。请解锁登录钥匙串后重新打开。" }
        Task { do { try await session.recover() } catch { fail("临时环境恢复未完成，请点击“恢复临时设置”。") }; if settings.AutoCheckUpdates { await checkUpdate(manual:false) } }
        monitor=Task { while !Task.isCancelled { try? await Task.sleep(for:.seconds(2)); if connected && !busy && !session.running { connected=false; report=nil; log("代理会话已退出，请重新连接。",.error); do { try await session.recover() } catch { fail("临时环境尚未恢复，请点击恢复临时设置。") } } } }
    }
    func log(_ text:String,_ level:Level = .info) { let clean=text.replacingOccurrences(of:#"https?://\S+"#,with:"[地址已隐藏]",options:.regularExpression); logs.append(LogEntry(message:clean,level:level)); if logs.count > 500 { logs.removeFirst(logs.count-500) } }
    func fail(_ text:String) { error=text; log(text,.error) }
    func save() { guard !storageReadFailed else { fail("原配置未成功读取，已阻止覆盖。请解锁钥匙串后重新打开。 "); return }; do { try SecureSettings.save(settings); log("环境设置已保存。",.success) } catch { fail("配置保存失败，请检查登录钥匙串与存储空间。") } }
    func chooseDirectory() { let panel=NSOpenPanel(); panel.canChooseFiles=false; panel.canChooseDirectories=true; if panel.runModal() == .OK, let url=panel.url { settings.ProjectDirectory=url.path } }
    func chooseProgram(desktop:Bool) { let panel=NSOpenPanel(); panel.canChooseDirectories=false; panel.canChooseFiles=true; panel.treatsFilePackagesAsDirectories=false; if panel.runModal() == .OK, let url=panel.url { if desktop { settings.DesktopPath=url.path } else { settings.ClaudePath=url.path } } }
    func run(_ launch:Bool) {
        guard !busy else { return }; busy=true
        operation=Task {
            defer { busy=false; progress=""; operation=nil }
            do {
                guard let node=selectedNode else { throw ClientError.message("请先添加节点或订阅。") }
                guard FileManager.default.fileExists(atPath:settings.ProjectDirectory) else { throw ClientError.message("项目文件夹不存在，请重新选择。") }
                if !session.running { progress="正在准备本地代理；TUN 模式请允许系统管理员授权…"; try await session.start(node:node,settings:settings); connected=true; log(session.tun ? "内置 TUN 与本地代理已启动。" : "本地进程代理已启动。",.success) }
                progress="正在检测出口与 API 连通性…"; var result=await Web.check(port:settings.ProxyPort,tun:session.tun,ipv6:session.ipv6Disabled); try Task.checkCancellation()
                if settings.MatchTimeZone && !result.zone.isEmpty { progress="正在匹配系统时区…"; try await session.matchZone(result.zone); result.zoneMatches=true; Web.rows(&result); log("系统时区已临时匹配，会话结束后恢复。",.success) }
                report=result; log(result.basicReady ? "API 基础网络可达；账户授权由官方客户端确认。" : "基础网络检测未通过，请查看结果并检查节点。",result.basicReady ? .success : .error)
                if result.webCode == 403 { log("Claude 登录页自动探测返回 HTTP 403；需浏览器确认。",.warning) }
                if launch {
                    let reasons=result.reasons(strict:settings.StrictChecks)
                    if !reasons.isEmpty { let approved=await confirmLaunch(result,strict:settings.StrictChecks); if !approved { return } }
                    if settings.LaunchTarget == "desktop" { try Launcher.desktop(settings); log("已请求官方 Claude 桌面客户端打开 Code 项目。",.success) } else { try Launcher.cli(settings); log("已在独立终端启动 Claude Code CLI；使用后请断开代理。",.success) }
                }
                saveQuietly()
                if settings.AutoCheckUpdates && offer == nil { Task { await checkUpdate(manual:false) } }
            } catch is CancellationError { log("检测已取消。",.warning) }
            catch { fail(error.localizedDescription); if !session.running { do { try await session.stop() } catch { log("恢复未完成，请重试恢复临时设置。",.error) }; connected=false } }
        }
    }
    func saveQuietly() { guard !storageReadFailed else { fail("原配置未成功读取，已阻止覆盖。请解锁钥匙串后重新打开。 "); return }; do { try SecureSettings.save(settings) } catch { fail("配置保存失败，原配置已保留。") } }
    func confirmLaunch(_ result:NetworkReport,strict:Bool) async -> Bool {
        await withCheckedContinuation { continuation in launchDecision=continuation; launchConfirmation=LaunchConfirmation(apiCode:result.apiCode,apiReachable:result.apiReachable,reasons:result.reasons(strict:strict)) }
    }
    func resolveLaunch(_ proceed:Bool) { let decision=launchDecision; launchDecision=nil; launchConfirmation=nil; decision?.resume(returning:proceed) }
    func disconnect() { resolveLaunch(false); operation?.cancel(); busy=true; Task { defer { busy=false; progress="" }; do { try await session.stop(); connected=false; report=nil; log("会话已结束，临时设置已恢复。",.success) } catch { fail(error.localizedDescription) } } }
    func browser() { Task { do { guard connected else { throw ClientError.message("请先连接节点。") }; let hidden=try await Launcher.browser(settings); log(hidden ? "已打开专用核对页，页面 IP 已隐藏；所选语言 "+settings.ProcessLanguage+"。" : "核对窗口已打开，请手动确认网页的隐藏 IP 开关。",hidden ? .success : .warning) } catch { fail(error.localizedDescription) } } }
    func importNode() { busy=true; Task { defer { busy=false }; do {
        let parsed=try await Bridge.parse(editor); guard !parsed.Nodes.isEmpty else { throw ClientError.message("未识别到支持的节点。") }
        if let old=editingNode { guard parsed.Nodes.count == 1 else { throw ClientError.message("编辑节点时只可提交一个节点。") }; var node=parsed.Nodes[0]; node.Id=old.Id; node.GroupId=old.GroupId; if !editorName.isEmpty { node.Name=editorName }; if let index=settings.Nodes.firstIndex(where:{$0.Id==old.Id}) { settings.Nodes[index]=node } }
        else { settings.Nodes.append(contentsOf:parsed.Nodes); if settings.SelectedNodeId == nil { settings.SelectedNodeId=parsed.Nodes.first?.Id } }
        saveQuietly(); showingNode=false; log("节点已保存，共 \(parsed.Nodes.count) 个。",.success); for warning in parsed.Warnings { log(warning,.warning) }
    } catch { fail("节点未保存，请检查分享链接或配置格式。") } } }
    func deleteNode(_ node:ProxyNode) { settings.Nodes.removeAll { $0.Id == node.Id }; if settings.SelectedNodeId == node.Id { settings.SelectedNodeId=settings.Nodes.first?.Id }; saveQuietly(); log("节点已删除。") }
    func updateSubscription(_ existing:Subscription? = nil) {
        busy=true; Task { defer { busy=false }; do {
            var item=existing ?? editingSubscription ?? Subscription(); if existing == nil { item.Name=editorName; item.Url=editorURL }; guard !item.Name.isEmpty else { throw ClientError.message("请输入订阅名称。") }
            let response=try await Web.fetch(item.Url,port:connected ? settings.ProxyPort : nil,limit:8*1024*1024,timeout:30); guard response.code == 200 else { throw ClientError.message("订阅下载失败。") }
            let parsed=try await Bridge.parse(String(decoding:response.data,as:UTF8.self)); guard !parsed.Nodes.isEmpty else { throw ClientError.message("订阅中没有可支持的节点。") }
            let merged=try await Bridge.merge(settings,item,parsed.Nodes); let nodes=parsed.Nodes; settings.Nodes=merged.Nodes; settings.SelectedNodeId=merged.SelectedNodeId; item.UpdatedAt=ISO8601DateFormatter().string(from:Date())
            if let i=settings.Subscriptions.firstIndex(where:{$0.Id==item.Id}) { settings.Subscriptions[i]=item } else { settings.Subscriptions.append(item) }; if selectedNode == nil { settings.SelectedNodeId=nodes.first?.Id }
            saveQuietly(); showingSubscription=false; log("订阅已更新，共 \(nodes.count) 个节点。",.success)
        } catch { fail("订阅更新失败，原节点已保留。请检查地址与网络。") } }
    }
    func deleteSubscription(_ item:Subscription) { settings.Nodes.removeAll { $0.GroupId==item.Id }; settings.Subscriptions.removeAll { $0.Id==item.Id }; saveQuietly(); log("订阅及所属节点已删除。") }
    func checkUpdate(manual:Bool) async { guard !updateBusy else { return }; updateBusy=true; defer { updateBusy=false }; do { offer=try await Updater.check(); if manual { log(offer == nil ? "当前已是最新版本。" : "发现新版本，可下载更新。",.success) } } catch { if manual { fail(error.localizedDescription) } else { log("自动更新检查暂未完成，可稍后在设置中重试。",.warning) } } }
    func downloadUpdate() { guard let offer else { return }; updateBusy=true; updateTask=Task { defer { updateBusy=false; updateTask=nil }; do { downloaded=try await Updater.download(offer); log("更新包已通过发布签名与 SHA-256 校验。",.success) } catch is CancellationError { log("更新下载已取消。",.warning) } catch { if Task.isCancelled { log("更新下载已取消。",.warning) } else { fail(error.localizedDescription) } } } }
    func installUpdate() { guard let downloaded else { return }; Task { do { try await Updater.install(downloaded,session:session) } catch { fail(error.localizedDescription); NSWorkspace.shared.activateFileViewerSelecting([downloaded]) } } }
    func fillPreview() {
        previewMode=true
        settings.Nodes=[ProxyNode(Id:"example",Name:"界面示例节点",GroupId:"",OutboundJson:"{\"type\":\"socks\",\"server\":\"127.0.0.1\",\"server_port\":1080}")]; settings.SelectedNodeId="example"
        var sample=NetworkReport(); sample.generalIP="198.51.100.24"; sample.cloudflareIP=sample.generalIP; sample.claudeIP=sample.generalIP; sample.apiCode=401; sample.webCode=403; sample.country="US"; sample.city="界面示例"; sample.zone="America/Los_Angeles"; Web.rows(&sample); sample.rows.append(CheckRow(label:"DNS 示例",value:"请求失败",detail:"仅用于核对错误样式，不代表真实网络",level:.error)); report=sample
        log("界面示例：启动器已就绪。"); log("界面示例：本地代理已启动。",.success); log("界面示例：网页检查未通过。",.warning); log("界面示例：请求失败，请检查节点。",.error)
    }
}
