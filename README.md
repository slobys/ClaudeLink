<p align="center">
  <img src="design/brand.svg" width="104" height="104" alt="Claude Link Logo">
</p>

<h1 align="center">Claude Link</h1>

<p align="center">为 Claude 桌面应用和 Claude Code 准备网络，添加节点、连接、启动，都在一个窗口完成。</p>

<p align="center">
  <a href="https://github.com/slobys/ClaudeLink/releases/latest">下载最新版</a> ·
  <a href="docs/使用指南.md">使用指南</a> ·
  <a href="docs/macOS.md">Mac 安装指南</a>
</p>

## 能做什么

Claude Link 把常用操作放进图形界面：

- 添加节点和订阅，内置代理内核，无需另开代理客户端
- 准备网络后打开官方 Claude 桌面应用的 Code 页面，也支持命令行模式
- 检查网络出口和连接结果，用颜色区分通过、注意和失败
- 优先使用 Chrome 打开核对页，默认遮挡页面上的 IP
- 检查新版本，下载校验后安装并重启

## 下载安装

当前版本为 **v1.2.2**，按电脑类型选择安装包：

| 你的电脑 | 下载 |
|---|---|
| Windows 64 位 | [EXE 安装包](https://github.com/slobys/ClaudeLink/releases/download/v1.2.2/ClaudeLink-Setup-1.2.2.exe) |
| Mac，Apple 芯片（M 系列） | [Apple 芯片 DMG](https://github.com/slobys/ClaudeLink/releases/download/v1.2.2/ClaudeLink-1.2.2-macos-arm64.dmg) |
| Mac，Intel 芯片 | [Intel DMG](https://github.com/slobys/ClaudeLink/releases/download/v1.2.2/ClaudeLink-1.2.2-macos-x64.dmg) |

Windows 下载后运行 EXE；Mac 打开 DMG，把应用拖入“应用程序”。不需要预装 .NET。便携版和其他版本见 [发布页](https://github.com/slobys/ClaudeLink/releases)。

Mac 需要 macOS 15 或更新版本，当前安装包尚未通过 Apple 公证；首次打开请按 [Mac 安装指南](docs/macOS.md) 操作。

## 开始使用

首次使用按以下步骤操作：

1. 安装 [官方 Claude 桌面应用](https://claude.com/download)，准备自己的节点或订阅。
2. 打开 Claude Link，在“节点管理”或“订阅管理”中添加。
3. 在工作台选好节点和项目文件夹，点击 **准备并打开 Claude 桌面客户端**。
4. 按系统提示允许本次管理员授权，再到 Claude 的 Code 页面确认项目。

桌面模式使用 TUN 虚拟网卡，会影响整台电脑的网络。用完点击 **断开**，启动器会尝试恢复本次临时网络和时区设置。命令行模式可在“环境设置”切换，更多操作见 [使用指南](docs/使用指南.md)。

## 更新与配置

发现新版时，点击 **立即更新**，下载完成后点击 **安装并重启**。节点、订阅和偏好保存在本机并加密，覆盖升级会保留这些配置。

**v1.2.0 / v1.2.1 如果无法检查更新，请先从托盘退出旧版，再手动覆盖安装 v1.2.2 一次。** 新版已修复 GitHub 接口限流导致的检查失败。

## 开发与许可

构建和发布方法见 [开发与发布](docs/开发与发布.md)，测试范围见 [验证记录](docs/VALIDATION.md)。本项目采用 [GPL-3.0-only](LICENSE)，第三方组件见 [许可说明](docs/THIRD-PARTY-NOTICES.md)。

Claude Link 是独立开源项目，不是 Anthropic 官方产品，不提供账户或节点服务。
