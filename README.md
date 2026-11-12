# Claude Link / Claude Network Launcher 1.2.0

Windows x64 / macOS 15+ 中文网络启动器：节点与订阅管理、内置代理 / TUN、官方 Claude 桌面客户端 Code 页面启动，并保留 CLI 模式。Mac 同时提供 Apple 芯片 arm64 与 Intel x64 版本。

[下载最新安装包](https://github.com/slobys/ClaudeLink/releases/latest) · [使用指南](docs/使用指南.md) · [验证记录](docs/VALIDATION.md)

## 安装和使用

- 安装版：artifacts/ClaudeLink-Setup-1.2.0.exe，按当前用户安装，不要求预装 .NET。
- 便携版：artifacts/ClaudeLink-Portable-1.2.0-win-x64.zip，解压运行。
- 独立程序：artifacts/publish/ClaudeNetworkLauncher.exe，单个 EXE 包含运行环境和代理内核。
- 使用指南：docs/使用指南.md；验证记录：docs/VALIDATION.md。
- Mac：下载对应芯片的 DMG，将 Claude Link 拖入“应用程序”；不确定芯片可在苹果菜单 → 关于本机查看。首次打开请参阅 [Mac 安装指南](docs/macOS.md)。

首次安装：从 Releases 下载 Setup EXE，退出旧版后覆盖安装即可保留节点和订阅。添加节点或订阅后，选择项目文件夹，点击「准备并打开 Claude 桌面客户端」。桌面模式自动选择必需的 TUN，按系统提示允许管理员授权；Claude 会确认项目文件夹。环境设置可切换为 CLI 命令行。

1.1.1 起，启动时检查正式版本；发现更新会显示按钮，下载校验后点击「安装并重启」。1.0.x 请先手动安装本版一次。更新保留配置，安装前恢复临时网络环境，不关闭 Claude 桌面应用或项目。

## 功能

- 节点添加/编辑/删除；分享链接与配置粘贴导入。Windows 另提供文件选择与手动表单。
- 订阅添加/编辑/删除、更新订阅；失败时保留原节点。Windows 另提供批量更新。
- 常见 VLESS / Reality、VMess、Trojan、Shadowsocks、Hysteria2、TUIC、SOCKS5、HTTP/HTTPS、AnyTLS 配置。
- 进程代理和 TUN 模式，后者按需 UAC 授权。
- 桌面模式自动发现普通安装版 / MSIX 商店版，使用官方 claude://code/new 链接；CLI 路径单独保存，避免混淆。
- GitHub Releases 在线更新、下载进度与取消、ECDSA 发布签名和 SHA-256 校验、安装后自动重启。
- 出口、API、地区与时区检测，保留 v1.3 严格预检选项，未知结果标记为未验证。
- 专用核对浏览器优先 Chrome，可在环境设置选择 Chrome / Edge；独立配置应用所选语言，并提供实际语言核对页。
- 可选临时 IPv6 和系统时区调整，先记恢复记录再修改，结束会话后尝试恢复。
- 进程监护、Windows 作业对象、后台会话互斥、旧记录恢复、系统托盘。
- 当前用户 DPAPI 加密保存节点、订阅与偏好，日志不记录密码或订阅令牌。
- 1.2.0 统一浅色界面、蓝色操作按钮；绿色通过、橙色注意、红色失败、灰色待检测，结果同时保留图标和文字。运行记录使用 16px 字号，从顶部按时间显示，并按级别着色。
- Mac 原生 SwiftUI 客户端：钥匙串密钥加密配置、系统授权 TUN、节点与订阅、专用 Chrome / Edge、桌面与 CLI 启动、临时环境恢复及签名更新。

支持边界与真实网络验证范围见使用指南和验证记录。需要自行安装官方 Claude 桌面应用或 CLI；不提供账户或节点服务。桌面应用的语言和账户权限由官方客户端管理，专用 Chrome 核对窗口仍默认优先 Chrome。

## 技术和结构

C# / WPF 与 macOS SwiftUI、.NET 10、官方 sing-box 1.14.4，固定上游压缩包 SHA-256；Mac 对原生可执行文件作 ad-hoc 代码签名。仅增加小型 YamlDotNet 解析依赖。v2rayN 为交互与功能参考，没有复制其源码。

- src/Launcher.Core：节点、订阅、内核配置等纯逻辑。
- src/Launcher.App：WPF、后台会话、Windows 环境、加密存储。
- src/Launcher.Mac：SwiftUI、macOS 后台监护与环境恢复、钥匙串和更新。
- src/Launcher.Bridge：Mac 复用的 .NET 节点解析与配置生成桥接，不安装大型 UI 框架。
- design/tokens.json：两端共用的颜色，scripts/sync-theme.ps1 生成 WPF 资源。
- tests/Launcher.Tests：无需额外测试框架的回归测试。
- installer：当前用户安装/卸载程序。
- scripts：获取内核、构建、测试、打包。
- docs：指南、设计、来源、验证记录。
- vendor：内核、源码快照、许可证。
- reference：仅本地保留的原始 v1.3 ZIP，不进入公共仓库；artifacts：发布产物和测试证据。

## 构建

使用 Windows x64 + PowerShell 7：

1. scripts/bootstrap.ps1：获取项目内固定 .NET SDK 和官方内核，校验哈希。
2. scripts/build.ps1 -SkipBootstrap：生成自包含 EXE、安装包和便携包。
3. scripts/test.ps1：运行纯逻辑、官方内核、本地协议、WPF、安装/卸载检查。
4. scripts/package-source.ps1：生成源码包。

工具在 .tools/，不改系统 PATH。App.ico 随源码提供；仅重新生成图标时需要已有的 Pillow。

macOS 构建：在对应架构的 macOS 15+ / Xcode Command Line Tools 环境运行 `bash scripts/build-macos.sh arm64` 或 `bash scripts/build-macos.sh x64`。脚本获取校验过的 SDK / 内核，执行原生测试，生成 .app、DMG 与在线更新 ZIP。GitHub Actions 为两种架构分别构建。

Source 提供可构建工程、第三方精确版本源码快照和验证结果。GPL-3.0-only；第三方各自的许可与名称使用条件见 docs/THIRD-PARTY-NOTICES.md 与 vendor/。本项目不是 Anthropic、SagerNet 或 v2rayN 官方产品。

## 发布更新

构建和测试后，运行 scripts/package-source.ps1，再运行 scripts/sign-release.ps1 生成 update.json 和 update.json.sig。发布 v版本号 的正式 GitHub Release，上传版本化 Setup、Portable、Source、两个签名清单附件和 SHA256.json。客户端只信任 slobys/ClaudeLink 的正式版本和内置公钥对应的签名。

包含 Mac 发布时，将两种架构的 ZIP / DMG 放入 artifacts，运行 `scripts/sign-release.ps1 -IncludeMac`；另外上传 update-macos-arm64.json / .sig 和 update-macos-x64.json / .sig。Windows 旧版继续使用原来的 update.json，不更改更新格式。

签名私钥由本机 Windows 用户 DPAPI 保护，保存于 %LOCALAPPDATA%/ClaudeLinkRelease/signing-key.dat，不进入源码或安装包。首次初始化用 scripts/sign-release.ps1 -InitializeKey；公钥一旦随正式版发布，后续版本必须保留对应私钥。CI 只构建和测试，不携带私钥。
