import AppKit
import SwiftUI

@MainActor final class AppDelegate:NSObject,NSApplicationDelegate {
    static weak var model:AppState?
    private var readyToExit=false
    func applicationShouldTerminate(_ sender:NSApplication) -> NSApplication.TerminateReply {
        if readyToExit { return .terminateNow }
        guard let model=Self.model else { return .terminateNow }
        model.resolveLaunch(false)
        Task { do { try await model.session.stop(); readyToExit=true; sender.terminate(nil) } catch { model.fail("临时环境恢复未完成，请先恢复后退出。") } }; return .terminateCancel
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication) -> Bool { true }
    func applicationWillTerminate(_ notification:Notification) {
        if let index=CommandLine.arguments.firstIndex(of:"--shutdown-smoke"), CommandLine.arguments.count > index+1 { try? AppPaths.write(Data("{\"Passed\":1,\"RestoredBeforeQuit\":true}".utf8),to:URL(fileURLWithPath:CommandLine.arguments[index+1])) }
    }
}
@main struct ClaudeLinkApp:App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject var model=AppState()
    var body:some Scene { WindowGroup("Claude Link") { RootView(model:model).frame(minWidth:1040,minHeight:700).preferredColorScheme(.light).onAppear { AppDelegate.model=model; if let position=CommandLine.arguments.firstIndex(of:"--ui-smoke"), CommandLine.arguments.count > position+1 { let folder=CommandLine.arguments[position+1]; Task { try? await Task.sleep(for:.seconds(2)); await capture(folder) } }; if CommandLine.arguments.contains("--shutdown-smoke") { Task { try? await Task.sleep(for:.seconds(1)); NSApplication.shared.terminate(nil) } } } }.defaultSize(width:1280,height:850).commands { CommandGroup(replacing:.newItem) {}; CommandGroup(after:.appInfo) { Button("检查更新…") { Task { await model.checkUpdate(manual:true) } } } } }
    @MainActor func capture(_ folder:String) async {
        do {
            let url=URL(fileURLWithPath:folder); try FileManager.default.createDirectory(at:url,withIntermediateDirectories:true)
            guard let window=NSApplication.shared.windows.first(where:{$0.contentView != nil}), let view=window.contentView else { throw ClientError.message("Missing window") }
            NSApplication.shared.activate(ignoringOtherApps:true); window.makeKeyAndOrderFront(nil); window.setContentSize(NSSize(width:1280,height:850)); try await Task.sleep(for:.milliseconds(500))
            for page in ["工作台","节点管理","订阅管理","环境设置","运行记录"] {
                model.page=page; try await Task.sleep(for:.milliseconds(600)); view.layoutSubtreeIfNeeded(); view.displayIfNeeded()
                guard let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) else { throw ClientError.message("Missing bitmap") }; view.cacheDisplay(in:view.bounds,to:bitmap)
                try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent(page+".png"))
            }
            model.page="工作台"; try await Task.sleep(for:.milliseconds(500)); model.previewScrollCheck="top"; try await Task.sleep(for:.milliseconds(500)); view.layoutSubtreeIfNeeded()
            if let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) { view.cacheDisplay(in:view.bounds,to:bitmap); try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent("checks.png")) }
            model.previewScrollCheck="bottom"; try await Task.sleep(for:.milliseconds(500)); view.layoutSubtreeIfNeeded()
            if let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) { view.cacheDisplay(in:view.bounds,to:bitmap); try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent("checks-bottom.png")) }
            model.page="运行记录"; window.setContentSize(NSSize(width:1040,height:700)); try await Task.sleep(for:.milliseconds(500)); view.layoutSubtreeIfNeeded()
            if let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) { view.cacheDisplay(in:view.bounds,to:bitmap); try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent("minimum.png")) }
            window.setContentSize(NSSize(width:1280,height:850)); model.page="环境设置"; model.settings.LaunchTarget="cli"; try await Task.sleep(for:.milliseconds(500))
            try saveView(view,to:url.appendingPathComponent("settings-cli.png")); model.settings.LaunchTarget="desktop"
            model.page="节点管理"; model.editor="socks5://127.0.0.1:1080#界面示例"; model.showingNode=true; try await Task.sleep(for:.milliseconds(700))
            guard let nodeSheet=window.sheets.first?.contentView else { throw ClientError.message("Missing node sheet") }; try saveView(nodeSheet,to:url.appendingPathComponent("node-editor.png"))
            model.editingNode=model.settings.Nodes.first; model.editorName=""; try await Task.sleep(for:.milliseconds(500)); if let emptySheet=window.sheets.first?.contentView { try saveView(emptySheet,to:url.appendingPathComponent("node-editor-empty.png")) }
            model.showingNode=false; try await Task.sleep(for:.milliseconds(500)); model.page="订阅管理"; model.editorName="界面示例订阅"; model.editorURL="https://example.invalid/sub"; model.showingSubscription=true; try await Task.sleep(for:.milliseconds(700))
            guard let subSheet=window.sheets.first?.contentView else { throw ClientError.message("Missing subscription sheet") }; try saveView(subSheet,to:url.appendingPathComponent("subscription-editor.png"))
            model.editorName=""; model.editorURL=""; try await Task.sleep(for:.milliseconds(500)); if let emptySheet=window.sheets.first?.contentView { try saveView(emptySheet,to:url.appendingPathComponent("subscription-editor-empty.png")) }; model.showingSubscription=false
            try await Task.sleep(for:.milliseconds(500)); model.page="工作台"; model.offer=UpdateOffer(manifest:ReleaseManifest(Schema:1,Repository:Updater.repository,Version:"9.9.9",AssetName:"example.zip",Size:1,Sha256:String(repeating:"0",count:64)),url:"https://example.invalid/update",notes:"界面示例")
            try await Task.sleep(for:.milliseconds(500)); try saveView(view,to:url.appendingPathComponent("update-notice.png"))
            model.offer=nil; model.launchConfirmation=LaunchConfirmation(apiCode:401,apiReachable:true,reasons:["Claude 登录页自动探测返回 HTTP 403（需浏览器确认）"]); try await Task.sleep(for:.milliseconds(700))
            guard let confirmSheet=window.sheets.first?.contentView else { throw ClientError.message("Missing launch confirmation sheet") }; try saveView(confirmSheet,to:url.appendingPathComponent("launch-confirmation.png"))
            var many=model.launchConfirmation!; many.reasons=(1...18).map { "检查示例 \($0)：此项目尚未验证，请返回结果页面核对网络、地区与环境设置。" }; model.launchConfirmation=many; try await Task.sleep(for:.milliseconds(700))
            if let longSheet=window.sheets.first?.contentView { try saveView(longSheet,to:url.appendingPathComponent("launch-confirmation-long.png")) }; model.resolveLaunch(false)
            try Data("{\"CapturedViews\":16,\"NativeViews\":true,\"ScreenCapture\":false}".utf8).write(to:url.appendingPathComponent("ui-results.json")); exit(0)
        } catch { fputs("Native rendering failed\n",stderr); exit(1) }
    }
    @MainActor func saveView(_ view:NSView,to url:URL) throws {
        view.layoutSubtreeIfNeeded(); view.displayIfNeeded()
        guard let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) else { throw ClientError.message("Missing native bitmap") }; view.cacheDisplay(in:view.bounds,to:bitmap)
        try bitmap.representation(using:.png,properties:[:])!.write(to:url)
    }
}
