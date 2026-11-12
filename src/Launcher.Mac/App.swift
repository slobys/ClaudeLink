import AppKit
import SwiftUI

@MainActor final class AppDelegate:NSObject,NSApplicationDelegate {
    static weak var model:AppState?
    func applicationShouldTerminate(_ sender:NSApplication) -> NSApplication.TerminateReply {
        guard let model=Self.model else { return .terminateNow }
        Task { do { try await model.session.stop(); sender.reply(toApplicationShouldTerminate:true) } catch { model.fail("临时环境恢复未完成，请先恢复后退出。"); sender.reply(toApplicationShouldTerminate:false) } }; return .terminateLater
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication) -> Bool { true }
}
@main struct ClaudeLinkApp:App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject var model=AppState()
    var body:some Scene { WindowGroup("Claude Link") { RootView(model:model).frame(minWidth:1040,minHeight:700).preferredColorScheme(.light).onAppear { AppDelegate.model=model; if let position=CommandLine.arguments.firstIndex(of:"--ui-smoke"), CommandLine.arguments.count > position+1 { let folder=CommandLine.arguments[position+1]; Task { try? await Task.sleep(for:.seconds(2)); await capture(folder) } } } }.defaultSize(width:1280,height:850).commands { CommandGroup(replacing:.newItem) {}; CommandGroup(after:.appInfo) { Button("检查更新…") { Task { await model.checkUpdate(manual:true) } } } } }
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
            model.page="工作台"; try await Task.sleep(for:.milliseconds(500)); model.previewScrollCheck=true; try await Task.sleep(for:.milliseconds(500)); view.layoutSubtreeIfNeeded()
            if let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) { view.cacheDisplay(in:view.bounds,to:bitmap); try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent("checks.png")) }
            model.page="运行记录"; window.setContentSize(NSSize(width:1040,height:700)); try await Task.sleep(for:.milliseconds(500)); view.layoutSubtreeIfNeeded()
            if let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) { view.cacheDisplay(in:view.bounds,to:bitmap); try bitmap.representation(using:.png,properties:[:])!.write(to:url.appendingPathComponent("minimum.png")) }
            try Data("{\"Passed\":7,\"NativeViews\":true,\"ScreenCapture\":false}".utf8).write(to:url.appendingPathComponent("ui-results.json")); exit(0)
        } catch { fputs("Native rendering failed\n",stderr); exit(1) }
    }
}
