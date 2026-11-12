import SwiftUI

struct LaunchConfirmationView:View {
    var data:LaunchConfirmation; var decide:(Bool)->Void
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack(alignment:.top,spacing:14) {
                Image(systemName:"exclamationmark.triangle").font(.system(size:22,weight:.medium)).foregroundStyle(Level.warning.color).frame(width:42,height:42).background(Level.warning.background,in:RoundedRectangle(cornerRadius:10))
                VStack(alignment:.leading,spacing:7) { Text("确认启动 Claude").font(.system(size:21,weight:.semibold)); Text("部分网络检查需要核对，请确认后再打开应用。").font(.system(size:14)).foregroundStyle(Palette.color("Muted")) }
            }
            VStack(alignment:.leading,spacing:6) {
                Text(data.apiReachable ? "API HTTPS 可达 · HTTP \(data.apiCode)" : "API 连通性尚未通过").fontWeight(.semibold).foregroundStyle(data.apiReachable ? Level.success.color : Level.error.color)
                Text("此项仅表示网络连通性，尚未验证账户授权。").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
            }.padding(14).frame(maxWidth:.infinity,alignment:.leading).background(data.apiReachable ? Level.success.background : Level.error.background,in:RoundedRectangle(cornerRadius:8))
            Text("以下项目未通过或尚未验证：")
            if data.reasons.count > 3 { ScrollView { issues }.frame(height:280) } else { issues }
            Text("继续启动不会改变检测结论；这些检查不判断账号封禁风险。").font(.system(size:12)).foregroundStyle(Palette.color("Muted"))
            Divider()
            HStack { Spacer(); Button("返回检查") { decide(false) }.keyboardShortcut(.defaultAction); Button("仍要启动") { decide(true) }.buttonStyle(.borderedProminent) }
        }.font(.system(size:14)).foregroundStyle(Palette.color("Ink")).padding(26).frame(width:620).background(Palette.color("Surface")).tint(Palette.color("Accent")).onExitCommand { decide(false) }
    }
    var issues:some View {
        VStack(alignment:.leading,spacing:7) {
            ForEach(Array(data.reasons.enumerated()),id:\.offset) { _,reason in
                HStack(alignment:.top,spacing:10) { Image(systemName:"exclamationmark.triangle").frame(width:16); Text(reason).lineSpacing(4).frame(maxWidth:.infinity,alignment:.leading) }
                    .foregroundStyle(Level.warning.color).padding(12).frame(maxWidth:.infinity,alignment:.leading).background(Level.warning.background,in:RoundedRectangle(cornerRadius:8))
            }
        }
    }
}
