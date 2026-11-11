# 1.0.1 启动失败修复

## 现象和定位

用户启用 TUN、临时关闭 IPv6 后，程序提示「会话启动或运行失败」。原会话记录 CorePid=0，且没有环境修改日志。

使用用户会话中的配置，仅运行官方内核 check：返回 0，静态配置有效。没有启动代理或更改实际路由。

在本机重放旧版的只读网卡查询：退出代码 0，标准输出 4 字符，为合法 JSON；标准错误 1363 字符，含模块加载进度 CLIXML。标准输出单独能解析，旧版合并标准输出和标准错误后无法解析。

旧版 ProcessTools.RunAsync 将两个流拼接；WindowsEnvironment.ApplyIPv6Async 把拼接结果作为 JSON，抛出 JsonException。后台捕获后又隐藏了具体异常，形成用户看到的笼统提示。

## 修改

- ProcessTools：提供分别返回 stdout / stderr 的运行接口；结构化 PowerShell 查询只读取 stdout，同时关闭无须展示的进度流。
- WindowsEnvironment：提取只读 IPv6 网卡查询；先验证 JSON 与 GUID，再写恢复记录、执行改动。
- EngineHost / SessionDiagnostics：记录启动阶段、异常类别、Windows 错误码及有限的内核错误输出；隐藏节点密码、UUID、服务器地址和 URL。
- CoreJob：明确 Windows API 失败位置，关联失败时释放作业句柄。
- IntegrationSmoke：加入 CLIXML 复现、输出隔离、本机只读网卡查询和诊断脱敏回归。
- 版本和安装包更新为 1.0.1；不迁移、不清空现有 settings.dat。

## 验证边界

本次定位与回归没有禁用本机 IPv6，没有调整时区，也没有让 TUN 接管本机路由。真实节点的联通性与本次 JSON 解析故障是不同验证项。
