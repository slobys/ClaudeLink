# v1.1.1 验证记录

验证日期：2026-11-12（Asia/Shanghai）。本轮测试未更改 Windows 时区或显示语言。

环境：Windows 11 x64，系统报告 10.0.26300；项目内 .NET SDK 10.0.404；自包含 Microsoft.NETCore.App / Microsoft.WindowsDesktop.App 10.0.15；官方 sing-box 1.14.4。

## 已通过

| 层次 | 结果 | 证据 |
|---|---|---|
| 编译 | WPF 与核心项目成功构建，0 警告、0 错误 | scripts/build.ps1 |
| 解析与数据逻辑 | 31 项断言通过 | artifacts/test-configs/results.json |
| 官方内核配置检查 | 11 份节点配置 + 1 份 TUN 配置全部通过 | sing-box check |
| 本地集成 | 64 项断言通过 | artifacts/integration-results.json |
| 桌面与更新 | MSIX 桌面检测、实际 GUI 激活、URI 与 CLI 区分、签名/下载/损坏/取消检查通过 | artifacts/desktop-update-results.json、desktop-launch-results.json |
| 浏览器语言 | Chrome / Edge 各完成 en-US、zh-CN 的原生网页语言与 HTTP 请求头检查 | artifacts/browser-language-results.json |
| WPF 表单 | 5 项实际交互断言通过，含桌面/CLI 切换 | artifacts/ui-check/ui-results.json |
| 页面渲染 | 原有页面、CLI 设置、更新提示共 10 张图 | artifacts/ui-check |
| 安装与卸载 | 6 项检查通过，含等待旧进程、升级及自动重启 | artifacts/installer-results.json |

## 本地集成覆盖

- 旧偏好迁移保留 CLI 路径，默认桌面模式；拒绝将控制台 EXE 作为桌面客户端；中文、空格、方括号和 & 项目路径正确编码。
- 本机商店版 Claude 被识别为 GUI；官方 URI 激活产生可见客户端窗口，未启动终端包装。
- 正式版本比较、ECDSA 清单、仓库与来源约束、安装包 SHA-256、损坏下载清理、用户取消和安装器重启参数均验证。

- 自动选择优先 Chrome；Chrome 156.0.8078.81 和 Edge 157.0.4341.46 使用新专用配置，实际网页主语言、语言列表首项和 HTTP Accept-Language 符合选择。
- API 401 与网页 403 分别报告，后者继续阻止严格预检通过；失败提示保留 DNS / TLS 类别，用户取消正常传播。

- DPAPI 加密后不出现订阅测试令牌，当前 Windows 用户可正确读回。
- Windows PowerShell 模块进度/警告 CLIXML 能复现旧版 JSON 错误；修复后的结构化查询保留合法 JSON。
- 本机活动物理网卡 IPv6 状态只读查询成功，未修改绑定。
- 诊断输出隐藏测试节点凭据、服务器地址和 URL。
- 占用端口被拒绝；真实官方内核通过后台宿主启动。
- HTTP 混合入站经指定 HTTP 出站，到达本地测试服务。
- 真实 sing-box Shadowsocks 客户端/服务端完成加密往返。
- 下载真实本地 HTTP 订阅、解析并提交；HTTP 失败与无效内容不删除旧节点。
- 正常停止终止内核、释放端口并删除临时明文配置。
- 强制终止宿主后，Windows 作业对象终止内核。
- 主进程退出后，宿主监护停止内核。
- 后台互斥拒绝第二个内核会话，既有内核保持运行。
- 模拟 Claude CLI 验证进程代理、语言和中文/方括号/单引号/空格/& 项目目录。
- PowerShell 5.1 UTF-8 BOM JSON 与会话完成信号兼容。
- 过期恢复记录拦截新会话；恢复助手执行幂等恢复并解除门禁。

## 界面和安装覆盖

- 真实程序窗口及添加节点窗口能够打开；桌面自动化的模态文本输入定位不稳定，提交路径使用程序自身 WPF Click 事件回归验证。
- 分享链接导入、手动 Trojan 表单、无效格式的行内错误提示均通过。
- 安装包在独立临时目录安装，安装文件 SHA-256 与发布版一致。
- 当前用户卸载注册项存在；自包含 EXE 无相邻 .NET 文件仍能运行。
- 覆盖升级保留用户额外文件至旧安装备份。
- 更新安装器等待指定 PID 和启动时间对应的旧进程退出，再升级并重启至独立 GUI 验证；其他目录的已运行管理员助手不阻断隔离安装。
- 卸载删除产品文件和卸载注册项，保留未知文件；隔离测试目录清理完成。

## 本轮没有执行的验证

- 没有启动或切换用户节点、访问用户私人订阅服务；浏览器与协议回归使用独立测试配置和本地服务。
- 排查时仅对用户已运行的代理做过只读网络请求：当时 API 401、Claude 网页 403、出口美国。该观察不是完整在线回归，也没有复现旧截图的首次连接失败。
- 未连接用户 Claude 账户；使用的是本地模拟 CLI。
- TUN 配置通过官方内核检查，但没有让 TUN 接管本机实际路由，也没有测试管理员 UAC 交互。
- 没有对本机执行 IPv6 禁用或系统时区修改；因此这些实机恢复效果仍需实际授权环境确认。
- 未在 Windows 10 或 ARM64 实机运行；本包只发布 x64。

发布文件哈希见 SHA256.json。第三方内核来源、版本和独立可执行文件哈希见 vendor/core-manifest.json。
