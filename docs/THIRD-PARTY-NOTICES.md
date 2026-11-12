# 第三方来源

本程序是独立开发的桌面启动器，不是 Anthropic、v2rayN 或 SagerNet 官方产品。

- sing-box 1.14.4：官方 Windows amd64 原始二进制，未修改。GPL-3.0-or-later，另含上游名称使用条款。许可证见 sing-box-LICENSE.txt 与 GPL-3.0.txt。精确发布、SHA-256 和源码地址见 core-manifest.json。
  - 发布：https://github.com/SagerNet/sing-box/releases/tag/v1.14.4
  - 对应源码：https://github.com/SagerNet/sing-box/tree/v1.14.4
  - 原始源码快照随 Source 包提供；依赖记录见其中 go.mod / go.sum。
  - 内核 Windows Wintun 实现由 sing-tun 集成；驱动许可及依赖声明保留在上游源码中。
- YamlDotNet 18.1.0：MIT，许可证见 YamlDotNet-LICENSE.txt；https://github.com/aaubry/YamlDotNet 。
- Microsoft .NET 10：自包含运行时来自微软官方发布资产；MIT 及第三方声明见 https://github.com/dotnet/runtime 和 https://github.com/dotnet/wpf 。
- v2rayN：仅作为节点、订阅和内核管理的功能与交互参考，未复制代码。阅读了 ServiceLib/Handler/SubscriptionHandler.cs 与 Handler/Fmt 目录。https://github.com/2dust/v2rayN 。
- 用户提供的 v1.3 PowerShell 压缩包：网络与恢复流程的需求依据，原包保留不变。

原创代码以 GPL-3.0-only 发布至 https://github.com/slobys/ClaudeLink ，对应 Source 包提供工程、许可证及内核源码快照。用户原始附件仅保留于本地 reference，不进入公开仓库和发布包。
# Node region artwork and name hints

- `design/flags/*.png`: unmodified Twemoji v15.1.0 country/region artwork, © Twitter, Inc. and other contributors, licensed under CC BY 4.0. Source: https://github.com/jdecked/twemoji/tree/v15.1.0/assets/72x72 . License: `vendor/Twemoji-LICENSE.txt`.
- `design/regions.json`: name-hint mapping adapted from NaiLink `apps/web/src/utils/node-region.ts`, MIT licensed. Source: https://github.com/slobys/NaiLink . License and copyright notice: `vendor/NaiLink-LICENSE.txt`. These are name hints, not geolocation results.
