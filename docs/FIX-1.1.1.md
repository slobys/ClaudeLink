# 1.1.1 客户端启动、更新与发布

验证日期：2026-11-12（Asia/Shanghai）。

## 桌面客户端与 CLI

原路径 %USERPROFILE%/.local/bin/claude.exe 属于 Claude Code CLI，原启动服务使用 PowerShell 启动终端，因此不会自动变成图形客户端。新版增加独立的启动方式和桌面路径，默认官方 Claude 桌面应用；原 CLI 路径保留用于可选 CLI 模式。

桌面应用支持普通安装目录和 MSIX 商店版自动发现；MSIX 每次升级重新查询当前包位置。自定义桌面路径验证 EXE 的 GUI 子系统，拒绝控制台程序。通过官方 claude://code/new?folder=... 打开 Code 页面，项目路径按 URL 编码，文件夹确认由 Claude 处理。已在本机检测并打开已安装的 MSIX Claude，存在可见客户端窗口；没有发送提示词或验证账号资格。

桌面模式明确使用 TUN，覆盖已运行的客户端及其后台 Code 进程；不依赖 CLI 环境变量继承来宣称桌面代理生效。网络保持到用户点击断开或托盘退出，不会把应用激活进程退出误判为工作结束。CLI 仍沿用终端正常结束后的自动恢复。

参考：[Claude 官方桌面深链接文档](https://support.claude.com/en/articles/14729294-open-claude-desktop-with-a-link)。

## 黄色边框

Windows.Graphics.Capture 会在捕获对象四周显示黄色提示框。排查发现此前界面测试遗留了工具捕获会话，释放后用户确认四周黄框消失。没有关闭 Windows 的隐私提示或修改录屏策略。

启动器本身没有屏幕捕获功能；本轮界面验证使用 WPF RenderTargetBitmap 渲染自己的窗口，不建立桌面捕获会话。

参考：[Microsoft 屏幕捕获说明](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)。

## 在线更新

启动时默认检查 GitHub 正式 Release，新版本显示按钮。可手动检查、关闭自动检查、取消下载；下载完成后由用户点击安装并重启。离线不阻止原功能。

节点连接成功后会通过本地代理重新检查一次，启动时的检查若仍在进行则排队重试，避免网络准备前的失败让更新按钮一直缺失。

清单限定产品、版本、附件名称和 GitHub 项目，使用内置 P-256 公钥验证 ECDSA/SHA-256 签名。安装包检查大小和 SHA-256，签名不符、文件损坏、跨仓库来源和旧版本均拒绝。下载不直接执行；安装前再次校验、保存偏好、停止网络并恢复环境，安装器按 PID 与启动时间等待旧进程退出后升级并重启。CLI 工作尚未结束时不能安装更新。

发布密钥在源码和安装包之外由当前 Windows 用户 DPAPI 保护。构建和签名脚本不输出私钥。安装器用有限进程查询识别真正的安装目录，修复其他目录的管理员助手误拦截安装的问题。

## 文件与验证

- 主要新增：Services/ClaudeDesktop.cs、Services/ReleaseUpdater.cs、MainWindow.Updates.cs、FeatureSmoke.cs、ReleaseProbe.cs、scripts/sign-release.ps1、scripts/ReleaseSigning.cs、GitHub Actions 工作流。
- 主要修改：Models.cs、MainWindow.xaml/.cs、App.xaml.cs、UiSmoke.cs、IntegrationSmoke.cs、安装器、版本与打包脚本、指南与许可证。
- 31 项核心断言、12 份官方内核配置、64 项集成断言、5 项 WPF 交互断言、10 张窗口渲染、6 项安装升级检查通过。
- 未重新接管用户实际 TUN、修改 IPv6 或时区，也未执行用户真实 Claude 任务；本轮更新下载/损坏/取消通过本地 HTTP 处理器模拟验证。在线正式 Release 验证结果发布后另行记录。
- GitHub Windows VM 的一张网卡没有 IPv6 绑定，Get-NetAdapterBinding 返回 CmdletizationQuery_NotFound。新版仅把该错误识别为无绑定；权限或其他读取错误仍失败。同时支持纯虚拟网卡空列表，独立数据覆盖纯虚拟、混合、缺失绑定和权限失败；本机新旧查询的活动物理网卡选择一致。
- 公开仓库与发布包排除节点、订阅、数据目录、私钥和原始附件。临时测试路径从公开集成报告中省略。
