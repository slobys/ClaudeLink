# 启动前检查说明

Claude Link 的作用是自动准备 Claude Code 的网络环境。检测结果用于排查连接问题，不能预测封号，也不能保证账户一定可用。

## 自动检查什么

| 项目 | 能说明什么 | 不能说明什么 |
| --- | --- | --- |
| 本地代理、出口 IP | 本次代理是否能发出请求 | 节点长期稳定性、账户资格 |
| Anthropic API | 到 `api.anthropic.com` 的 HTTPS 连通性 | 401 不代表账户授权通过 |
| Claude 登录页 | 自动请求 `https://claude.ai/login` 的结果 | 自动请求 403 不代表 Chrome 打不开，更不能据此判定封号 |
| 登录跳转域名 | `claude.com` 的可访问情况 | 不验证完整登录流程 |
| OAuth 服务域名 | `platform.claude.com` 的可访问情况 | 不发送凭据或令牌，不验证令牌交换 |
| HTTPS / TLS | 当前检测进程是否正常完成这些域名的证书校验 | 官方客户端可能使用不同的证书存储或代理设置 |
| 订阅流量、到期时间 | 服务商在订阅响应头中提供的数据 | 不代表节点延迟或 Claude 使用额度 |

登录/OAuth 是补充诊断，单次请求最多 5 秒并发执行。域名根页面的返回结果不能验证其全部接口；失败时请结合官方客户端提示排查。

## 仍需在官方客户端确认

- 账户登录、套餐是否支持所用功能、使用额度，以及组织权限。
- 实际所在地区与主体是否符合服务提供范围；出口 IP 的第三方定位不能决定使用资格。
- 企业代理的自定义 CA 证书。启动器不会关闭 TLS 证书验证；CLI 与桌面应用的证书配置可能不同。
- 企业 SSO 还会使用 `sso.claude.com`。桌面应用、连接器、插件、更新和浏览器桥接可能需要额外域名，完整清单见官方网络文档。
- 无法使用时先查看 [Anthropic 服务状态](https://status.claude.com/)，再核对客户端的具体错误。

## 自定义严格预检

为了保留原启动器及用户已有设置，仍提供三出口 IPv4 一致、US 参考地区、TUN、IPv6 临时关闭、时区匹配的兼容预设。

**这些是本项目的自定义条件，不是 Anthropic 公布的防封要求。** 没有公开依据证明英文、关闭 IPv6、匹配时区或美国出口可以避免封号。应用保留原设置，用户可在环境设置中调整；涉及系统的临时修改会记录并在会话结束时恢复。

## 官方资料

核对日期：2026-11-13。官方要求可能更新，请以原文为准。

- [Claude Code 网络与代理配置](https://code.claude.com/docs/en/network-config)
- [安装与系统要求](https://code.claude.com/docs/en/setup)
- [安装、证书与登录排错](https://support.claude.com/en/articles/14552646-troubleshoot-claude-code-installation-and-authentication)
- [Anthropic 支持的地区](https://www.anthropic.com/legal/supported-regions)
