# 实施计划

1. 保留原始 v1.3 压缩包，建立独立 C# 工程，不执行旧脚本的网络更改。
2. 参考 v2rayN 的订阅分组、节点导入、独立内核管理交互，自行实现精简客户端。
3. 支持 VLESS（含 Reality）、VMess、Trojan、Shadowsocks、Hysteria2、TUIC、HTTP、SOCKS 节点。
4. 订阅更新先完整解析再替换，失败不清空旧节点；报告未支持的节点与参数。
5. 两种连接方式：进程专用 HTTP 代理；TUN 虚拟网卡（全机路由，按需管理员权限）。
6. 保留 v1.3 严格预检选项；界面区分未验证、失败、通过和用户跳过。
7. 时区与活动物理网卡 IPv6 修改均由用户在界面选择，写恢复日志后执行，退出时读回确认。
8. 会话互斥；内核宿主监护主进程；Claude 会话运行时关闭主窗口进入托盘。
9. 分层验证：解析/配置回归测试、官方内核 check、本地代理协议集成、真实 WPF 界面、安装与卸载。
10. 未提供真实节点的联网效果不标记为已验证；TUN 实机路由测试与静态检查分开记录。

## 初始关键文件

- 原始压缩包中的 README_使用说明.md、ClaudeCode-Network.ps1、IPv6Guard.ps1、TimeZoneGuard.ps1、启动入口和 Hook。
- v2rayN/ServiceLib/Handler/SubscriptionHandler.cs、Handler/Fmt、CoreConfigHandler.cs（按需读取）。
- sing-box 官方 TUN、DNS、Route、各协议 outbound 配置文档。

## 第三方选择

- sing-box 1.14.4：独立官方可执行内核，不修改其实现；打包许可证与精确版本来源。
- YamlDotNet：小型 MIT YAML 解析库，用于常见 Clash 订阅导入；不执行订阅内脚本或外部提供者。
- v2rayN：功能和交互参考，不直接复制源码。来源 https://github.com/2dust/v2rayN 。
