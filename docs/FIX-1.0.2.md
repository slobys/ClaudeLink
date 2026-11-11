# v1.0.2：Chrome 核对语言与检测结果修复

## 语言问题

旧版固定打开 Edge，主要通过启动参数设置语言。排查发现旧专用配置的 intl.accept_languages / intl.selected_languages 仍为 zh-CN,en-US,en,en-GB；只传 --lang 不能可靠地把已有语言列表切到 en-US。普通 Chrome 窗口也不会受到专用窗口的参数影响。

新版默认优先使用 Chrome，可在环境设置明确选择 Chrome 或 Edge。每次核对使用新的独立配置，同时写入 selected_languages、accept_languages、app_locale，并传入 --lang 和 --accept-lang；保持当前会话的 HTTP 代理。语言核对页直接读取浏览器原生语言与 Intl 信息，没有覆写 navigator 属性。

本机 Chrome 156.0.8078.81、Edge 157.0.4341.46 均用真实窗口和本地 HTTP 服务完成 en-US / zh-CN 验证：主语言、语言列表首项、Intl locale 和 HTTP Accept-Language 符合选择。Chrome 英文结果为 navigator.language=en-US，navigator.languages=[en-US,en]，Accept-Language=en-US,en;q=0.9。详细结果见 artifacts/browser-language-results.json。

测试使用一次性配置和仅在测试启动时启用的本机 DevTools 连接；正常核对窗口不启用远程调试。该 Edge 版本的 headless dump-dom 曾返回空输出，因此最终回归采用与产品相同的原生窗口模式，读取实际网页结果，并关闭测试浏览器。

## 网络状态与弹窗

- 区分本地代理已启动、API HTTPS 可达和严格预检结果。
- API 返回 401 代表收到未认证响应，不代表账户授权成功；Claude 网页返回 403 单独显示，继续影响严格预检，不将其误算为 API 失败。
- 显示实际 HTTP 状态或 DNS、TLS、代理隧道、连接、超时等失败类别。
- 启动确认弹窗列出具体未通过项目，弹出前结束检测进度显示。
- 每次检测写入当前会话 network-report.json，包含检测完成时间和明细。

排查期间通过用户当时已运行的本地代理观察到 API 401、Claude 网页 403，以及美国出口。这是当时的只读结果；旧截图中的首次节点失败原因没有足够日志确定，不能直接归因于语言或 DNS。未修改节点、订阅、DNS 路由方案，也未重新接管本机 TUN。

## 使用

退出旧版启动器并运行 1.0.2 安装包覆盖安装，节点、订阅和偏好保留。进入环境设置：专用核对浏览器选择 Chrome（或自动），语言选择 en-US，保存。连接后点击工作台「打开专用核对浏览器」，在新窗口的「语言核对」标签确认结果。

普通 Chrome 的现有标签页保持原语言；需要在启动器新打开的窗口检查。此设置不修改 Windows 显示语言或普通 Chrome 配置，也不保证第三方检测结论或账户可用性。

## 来源与实现

- Chromium 语言偏好键：[pref_names.h](https://chromium.googlesource.com/chromium/src/+/HEAD/components/language/core/browser/pref_names.h)。
- 修改文件：Launcher.Core/Models.cs；Launcher.App 的 CheckBrowser.cs、ClaudeTerminal.cs、NetworkServices.cs、MainWindow.xaml/.cs、IntegrationSmoke.cs；版本、打包、测试清理脚本与文档。没有增加依赖。
- 验证范围和统计见 VALIDATION.md；自动化原始结果随源码包提供。
