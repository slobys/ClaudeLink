---
name: Claude Link
description: Windows WPF 与 macOS SwiftUI 的浅色原生网络工作台
colors:
  Background: '#F4F7FB'
  Surface: '#FFFFFF'
  SurfaceAlt: '#F8FAFC'
  Sidebar: '#FFFFFF'
  Ink: '#18283D'
  Muted: '#5C6D82'
  Line: '#D8E1EC'
  Accent: '#2563EB'
  AccentSubtle: '#EAF1FF'
  Success: '#067647'
  SuccessBg: '#ECFDF3'
  Warning: '#92400E'
  WarningBg: '#FFFAEB'
  Error: '#B42318'
  ErrorBg: '#FEF3F2'
  Info: '#175CD3'
  InfoBg: '#EFF8FF'
  Pending: '#475467'
  PendingBg: '#F2F4F7'
  BrandCharcoal: '#202C2D'
  BrandCoral: '#E49B7D'
  BrandCream: '#F4EBDF'
typography:
  headline-windows:
    fontFamily: Segoe UI, Microsoft YaHei UI
    fontSize: 26px
    fontWeight: 600
  headline-macos:
    fontFamily: system
    fontSize: 28px
    fontWeight: 700
  session-title:
    fontSize: 21px
    fontWeight: 600
  subscription-title:
    fontSize: 17px
    fontWeight: 600
  section-title-windows:
    fontSize: 17px
    fontWeight: 600
  section-title-macos:
    fontSize: 19px
    fontWeight: 600
  body-windows:
    fontFamily: Segoe UI, Microsoft YaHei UI
    fontSize: 14px
    fontWeight: 400
  body-macos:
    fontFamily: system
    fontSize: 14px
    fontWeight: 400
  log-windows:
    fontFamily: Segoe UI, Microsoft YaHei UI
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.625
  log-macos:
    fontFamily: system
    fontSize: 16px
    fontWeight: 400
  navigation:
    fontSize: 15px
  support-windows:
    fontSize: 13px
  support-macos:
    fontSize: 12px
  status-label:
    fontSize: 12px
    fontWeight: 600
rounded:
  choice: 6px
  control: 8px
  card: 12px
  badge-windows: 14px
  dialog-tone: 10px
spacing:
  tight: 6px
  small: 8px
  control-gap: 10px
  related: 12px
  group: 16px
  section: 20px
  card-inset: 22px
  window-windows: 30px
  window-macos: 28px
components:
  button-primary:
    backgroundColor: '{colors.Accent}'
    textColor: '{colors.Surface}'
    rounded: '{rounded.control}'
    padding: 10px 16px
  button-secondary:
    backgroundColor: '{colors.SurfaceAlt}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.control}'
    padding: 10px 16px
  input-windows:
    backgroundColor: '{colors.Surface}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.control}'
    padding: 9px 12px
  navigation-selected:
    backgroundColor: '{colors.AccentSubtle}'
    textColor: '{colors.Accent}'
    rounded: '{rounded.control}'
  card:
    backgroundColor: '{colors.Surface}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.card}'
    padding: '{spacing.card-inset}'
  node-card:
    backgroundColor: '{colors.Surface}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.card}'
    padding: 10px
  node-card-selected:
    backgroundColor: '{colors.AccentSubtle}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.card}'
    padding: 10px
  subscription-card:
    backgroundColor: '{colors.Surface}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.card}'
    padding: 16px 18px
  subscription-card-selected:
    backgroundColor: '{colors.AccentSubtle}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.card}'
    padding: 16px 18px
  chain-route:
    backgroundColor: '{colors.AccentSubtle}'
    textColor: '{colors.Ink}'
    rounded: '{rounded.dialog-tone}'
    padding: 14px
  result-success:
    backgroundColor: '{colors.SuccessBg}'
    textColor: '{colors.Success}'
    rounded: '{rounded.control}'
  result-warning:
    backgroundColor: '{colors.WarningBg}'
    textColor: '{colors.Warning}'
    rounded: '{rounded.control}'
  result-error:
    backgroundColor: '{colors.ErrorBg}'
    textColor: '{colors.Error}'
    rounded: '{rounded.control}'
  result-info:
    backgroundColor: '{colors.InfoBg}'
    textColor: '{colors.Info}'
    rounded: '{rounded.control}'
  result-pending:
    backgroundColor: '{colors.PendingBg}'
    textColor: '{colors.Pending}'
    rounded: '{rounded.control}'
  brand-mark:
    width: 42px
    height: 42px
  dialog:
    backgroundColor: '{colors.Surface}'
    textColor: '{colors.Ink}'
  dialog-tone:
    rounded: '{rounded.dialog-tone}'
    width: 42px
    height: 42px
---

# Design System: Claude Link

## Overview

**Creative North Star: "可读的原生网络工作台"**

界面以白色侧栏和白色工作面承载操作，浅冷灰蓝色背景区分区域，石板色文字建立阅读层级，蓝色标出当前导航与主要行动。密度服务于节点配置、连接检查和恢复操作，沿用桌面系统控件的交互习惯。

两端共享颜色含义、卡片材料与中文状态语言，分别采用 WPF 与 SwiftUI 实现。系统字体、窗口外壳、选择器、菜单、焦点反馈和授权窗口遵循所在操作系统；统一的是视觉语义，而非强行复制控件像素。

应用标志保留用户指定的深炭、珊瑚与米白三色，作为独立身份资产；操作蓝继续承担界面行动。启动确认与应用自有提示沿用白色工作面、石板文字和语义色，将检测事实与继续启动的决定分别呈现。

**Key Characteristics:**

- 白色工作面、轻边界与蓝色操作重心。
- 结果通过颜色、文字和图形共同表达，未知状态保留灰色。
- 运行记录正文保持 16 个逻辑单位，按时间从上到下排列。
- 共享令牌与平台原生控件共同组成系统。
- 暖色标志保持独立，主题对话框延续工作台的阅读与操作层级。

此文档记录已经构建的原生桌面系统。界面颜色以 `design/tokens.json` 为运行时来源，Brand 前缀颜色取自 `design/brand.svg`，不加入操作色板；本文件前置令牌是用于设计工具的提取快照。WPF 从 `Theme/Colors.xaml` 使用生成资源，Mac 从应用包中的 `tokens.json` 读取相同键。表头中的 `px` 是设计格式的可移植写法：WPF 对应 DIP，SwiftUI 对应 point，不是截图中的物理像素。没有额外的网页运行层。

证据范围：已核对 `src/Launcher.App/App.xaml`、`Theme/Colors.xaml`、`MainWindow.xaml`、`NodeDialog.xaml` 及状态／日志绑定，和 `src/Launcher.Mac/Views.swift`、`Models.swift`、`App.swift`。渲染抽样包括 `.impeccable/review/checks.png`、`logs.png`、`macos-arm64/工作台.png`、`macos-arm64/subscription-editor-empty.png` 与 `macos-x64/checks.png`；完整原生审查图位于同一 review 目录。未使用 HTML 检测器；原生截图审查不构成键盘全流程或用户 Mac 真实路由验证。

1.2.1 扩展另核对 `design/brand.svg`、`BrandAssets.cs`、`AppDialog.xaml`／`.xaml.cs`、`LaunchConfirmationView.swift` 及图标导出脚本；对应证据为 `.impeccable/review/1.2.1/windows/` 中的 `brand-scales.png`、`launch-confirmation.png`、`launch-confirmation-long.png`、`notice-error.png`、`dashboard.png`、`settings-presets.png`，和 `macos-arm64/` 中的启动确认普通／长内容截图与 `环境设置.png`。这些图用于核对外观与内容边界，不证明真实账号、Mac 键盘全流程或 TUN 已获授权并验证。

1.3.0 局部扩展核对 `MainWindow.xaml`、`MainWindow.Nodes.cs`、`ChainDialog.xaml`／`.xaml.cs`、`NodeCatalog.swift` 和 `NodePresentation.swift`。同一应用提交 `974d2ac` 的 Windows 原生自绘证据为 `.impeccable/review/1.3.0/windows/` 下的 `nodes.png`、`nodes-small.png`、`nodes-empty.png`、`chain-editor.png`、`chain-editor-error.png` 与 `dashboard.png`；Mac 对应图为 `macos-x64/` 下的 `节点管理.png`、`nodes-small.png`、`nodes-empty.png`、`chain-editor.png`、`chain-editor-error.png` 与 `工作台.png`，`macos-arm64/` 同样保留完整证据。所有节点均为示例，原生内容由应用自身绘制，未启用屏幕捕获；Windows 与两种 Mac 架构的原生 CI 已通过。隔离夹具另验证双跳 HTTP 及入口失效不旁路，不能据此推断真实用户 TUN、出口住宅属性、账户授权或键盘全流程已验收。

1.3.2 节点密度调整按用户明确要求将节点卡片缩小并在用户窗口宽度及更宽时显示四列。Windows 验证来源为代码提交 `cd79fc64a6393c545ed03fec5305bc275dbd0bb4`，原生 CI `43211751714` 已通过；从该提交到 `d339fed`，Windows、Core、安装器、设计资源和脚本的运行输入无变化。本轮 Windows 原生自绘证据位于 `.impeccable/review/1.3.2/windows/`：`nodes.png`、`nodes-user-width.png` 为四列，`nodes-small.png` 为三列及纵向滚动，`nodes-empty.png` 保留无匹配引导。示例包含超长名称和链路路径，未使用屏幕捕获，也未发布用户私人参考截图。Windows 独立 finish review 的结论为 ship，无 material 项。

Mac 验证来源为补齐路径／来源完整 help 的代码提交 `d339fed856cf8443d2f05f54ee3a4571caccd350`；两种架构的原生 CI `43212943064` 均已通过，各含 51 项原生、14 项后台、1 项退出和 20 项 UI 检查。`.impeccable/review/1.3.2/macos-x64/` 下的 `节点管理.png`、`nodes-small.png`、`nodes-empty.png` 已由该最终 CI 的 Intel 原生图覆盖，并逐张复查，分别记录四列、三列和空结果。两端证据分别来自上述提交；1.3.0 的 Mac 图不作为本轮密度证据。这些原生示例不证明用户 Mac 实机、真实 TUN、网络出口或账户授权已验收。

1.3.3 按用户要求进一步降低节点卡片高度：四行信息合为三行，内部高度由 92 改为 70，内边距由 12 改为 10，其余列数预算、间距、字级、颜色与标志保持不变。源码核对提交为 `289d15bc62cc785e6f3262dd8cb65166e7122f0c`，范围为 `MainWindow.xaml`、`MainWindow.Nodes.cs` 与 `NodeCatalog.swift`。本轮 Windows 原生 CI `43221634447` 已通过，原生自绘证据位于 `.impeccable/review/1.3.3/windows/`：`nodes.png`、`nodes-user-width.png`、`nodes-small.png` 与 `nodes-empty.png`，分别核对宽窗／用户宽度四列、窄窗重排和无匹配引导；`ui-results.json` 记录 14 项 UI 检查通过。Windows 独立 finish review 的结论为 ship，无 material 项。

同一应用提交的 macOS Apple Silicon 与 Intel 原生 CI `43221634653` 均已通过，各含 51 项原生、14 项后台、1 项退出与 20 项 UI 检查。本轮 Mac 视觉核对仅限 `.impeccable/review/1.3.3/macos-arm64/` 下的 `节点管理.png`、`nodes-small.png`、`nodes-empty.png`，分别记录四列、窄窗重排和空结果；Apple Silicon 独立 finish review 的结论为 ship，无 material 项。Intel 本轮仅记录原生 CI 通过，不扩大截图审查范围，也不将 1.3.2 的历史 Mac 证据视为本轮验证。两端图中均为示例数据，未启用屏幕捕获；这些原生示例不证明用户 Mac 实机、真实 TUN、出口、账户授权或实际 hover 操作已验收。


1.4.0 的局部扩展在同一三行卡片中加入 HTTP 延迟，未改变全局颜色、字体、标志或密度。核对源码为 `MainWindow.Nodes.cs`、`MainWindow.Latency.cs`、`MainWindow.xaml` 的节点页、`NodeCatalog.swift` 与 `Latency.swift`。Windows 原生自绘证据位于 `.impeccable/review/latency-windows/`：`nodes-latency.png`、`nodes-latency-running.png`、`nodes-user-width.png`、`nodes-small.png`、`nodes-empty.png`，分别覆盖延迟状态、取消与禁用、用户宽度四列、窄窗重排及空结果；独立 finish review 为 ship，无 material 项。Mac 最终来源为补充动态可访问值的代码提交 `33d32a0`；两架构原生 CI `43233056492` 均成功，各含 57 项原生、7 项延迟、14 项后台、1 项退出检查及 22 张实际 PNG。`.impeccable/review/latency-macos/` 中的 `节点管理.png`、`nodes-latency.png`、`nodes-latency-running.png`、`nodes-small.png`、`nodes-empty.png` 已由该 CI 的最终原生自绘图覆盖并逐一确认有效。独立 Mac finish review 原唯一 material 项为自定义可访问名称遗漏选择与延迟信息；最终 verdict 将动态 `accessibilityValue` 修复评为 resolved，remaining 为 clear，disposition 为 ship。该 verdict 仅覆盖原修复项，不扩大为重新整页无问题认证或用户 Mac 真实 VoiceOver 验收。所有示例均不证明用户真实节点、账户、Mac 实机路由或授权已验证。

1.4.1 是对上述节点流程的局部性能修复：将短超时、有界并发和结果批量回显落实到既有原生界面，颜色、字级、卡片密度与动态可访问值保持不变。Windows 本机完整验证含 57 项核心、16 份官方配置、94 项集成、17 项 UI 与 7 项安装检查；CI `43238417845` 成功。`.impeccable/review/1.4.1/windows/` 中的延迟、运行中、用户宽度、窄窗和空结果图用于核对布局、结果和取消入口；卡片对象复用与选择保持由增量交互断言验证，独立 finish review 为 ship，无 material 项。Mac 两架构 CI `43238417795` 均成功，其中 Apple 芯片证据含 58 项原生、10 项延迟、14 项后台、1 项退出检查及 22 张原生视图；`.impeccable/review/1.4.1/macos/` 的对应五张图用于核对同一界面流程，独立 Mac finish review 完整结论为 ship，无 material 项。24 条唯一模拟线路（一半正常、一半等待超时）的 Windows 网络参数隔离复测为 25.038 秒→3.962 秒，约 6.32 倍；Apple 芯片的同类隔离参数对比为约 5.64 倍。两者分开记录，不把 Windows 比值推广到 Mac 或用户节点。这不是用户真实节点测速、旧版完整界面耗时或帧率实测，不能据此证明真实 Mac、账户或 VoiceOver 已验收。

1.5.0 的订阅扩展核对 MainWindow.xaml、MainWindow.Subscriptions.cs、App.xaml 及 SubscriptionCatalog.swift、SubscriptionTraffic.swift。Windows 证据为 artifacts/ui-final-1.5.0/ 的选中、窄窗、状态与空列表图；Mac 最终双架构 CI 43339872373 的 25 张原生视图及 artifacts/macos-final-arm64-evidence/ui/ 对应样本覆盖静态未知轨道、低余量、到期及空列表。两端独立界面复核均为 ship，不把 CI 扩大为用户真实网络、账户或 VoiceOver 验收。

## Colors

色板以清晰的操作蓝和冷中性色为底，绿色、琥珀色、红色与信息蓝只传达具体结果或反馈。设计面板 sidecar 中的 tonalRamp 为这些色值派生的八级展示色带，不属于运行时颜色令牌；产品仅使用前置颜色表中的规范色值。

### Primary

- **操作蓝（Accent）**：主要按钮、输入焦点及当前导航；浅蓝选择底（AccentSubtle）用于选中导航、表格行和选择项。
- **信息蓝（Info / InfoBg）**：信息级别、进行中反馈及更新提示。它说明情况，不暗示检查成功。

### Secondary

- **标志三色（BrandCharcoal / BrandCoral / BrandCream）**：深炭底、珊瑚开口环与米白向上箭头共同构成应用标志。它们只定义身份资产，不替换按钮、导航、成功或注意的语义颜色。

**The 标志边界 Rule.** 标志沿用 `design/brand.svg` 的三色与几何；操作与结果继续使用既有共享颜色键。

### Neutral

- **冷灰蓝画布（Background）**：内容区外围，使白色工作面保持独立。
- **白色工作面（Surface / Sidebar）**：表单、卡片、列表、编辑窗和持续导航。
- **轻冷灰（SurfaceAlt）**：次要按钮、表头、交替行和底部内核信息块。
- **深石板（Ink）**：主文案与普通信息日志正文；**中石板（Muted）**用于辅助说明、时间和未选导航。
- **细冷灰边界（Line）**：卡片、输入框和区域分界。边界提供结构，不代替文字层级。
- **待检测灰（Pending / PendingBg）**：尚未连接、尚未检测或无可靠结论。

### Result semantics

| 语义 | 前景／底色令牌 | 状态文字 | 原生图形 |
| --- | --- | --- | --- |
| 成功 | Success / SuccessBg | 通过；Windows 日志为完成 | WPF 勾选路径；Mac checkmark.circle.fill |
| 注意 | Warning / WarningBg | 注意 | WPF 三角警示路径；Mac exclamationmark.triangle.fill |
| 错误 | Error / ErrorBg | 失败；Windows 日志为错误 | WPF 交叉路径；Mac xmark.circle.fill |
| 信息 | Info / InfoBg | 信息 | Mac info.circle；Windows 信息反馈使用文字与进度控件 |
| 待检测 | Pending / PendingBg | 待检测／未连接 | WPF 短横路径；Mac clock |

检查行的图形、结果值和状态文字采用同一语义前景，容器使用对应浅底色。说明文字保留 Muted，检查名称保留 Ink。WPF 检查模板当前实现通过、注意、失败和待检测四种状态；Mac 另有信息行。连接徽章表达当前会话状态；Windows 徽章使用文字，Mac 使用 Label 图形和文字，不把徽章本身当成完整检查报告。

**The 独立证据 Rule.** 代理已启动、API HTTPS 可达、网页检查与账户授权分别显示。一个检查通过不替代其他检查，未知结果使用 Pending。

## Typography

正文使用操作系统 UI 字体：Windows 为 Segoe UI，中文回退 Microsoft YaHei UI；Mac 为 SwiftUI `.system`，由系统选择中文字形。没有自定义展示字体。字号从操作任务的重要性提取，不使用强制比例刻度。

| 角色 | Windows | macOS | 用途 |
| --- | --- | --- | --- |
| 页面标题 | 26 / Semibold | 28 / Bold | 当前页面 |
| 会话卡片标题 | 21 / Semibold | 21 / Semibold | 会话准备表单 |
| 常规分区标题 | 17 / Semibold | 19 / Semibold | 设置或列表分区；Windows 检查标题为 16 |
| 订阅名称 | 17 / Semibold | 17 / Semibold | 卡片首行；单行省略，完整名称保留提示 |
| 正文 | 14 / Regular | 14 / Regular | 标签、结果说明、控件 |
| 导航 | 15 | 15 | 当前项为 Semibold |
| 辅助说明 | 13；检查细节 12 | 12 | 非主要结果和提示 |
| 日志正文 | 16，行高 26，段后 9 | 16，额外行距 6，记录间距 14 | 可选择的操作记录 |
| 时间／级别 | 时间为 Consolas；级别 Semibold | 13；时间使用 monospacedDigit | 日志扫描定位 |

节点原始配置编辑使用等宽字体：Windows 导入编辑框为 Consolas / Microsoft YaHei UI（12），Mac TextEditor 为系统 monospaced（14）。等宽字体服务于内容对齐，不扩展到普通中文正文。Mac InputField 空内容提示使用 Muted（13），真实 TextField 保留可访问名称；提示层不接收点击，也不重复进入可访问树。

**The 日志可读 Rule.** 运行记录正文保持 16 个逻辑单位，较早记录在上，新记录追加在下；信息正文使用 Ink，级别标签承担信息蓝色。

## Layout

桌面采用持续侧栏与可滚动内容区。两端侧栏宽度均为 224；页面标题、会话徽章、反馈区和底部恢复入口保持可辨认的位置。工作区按任务分组，不使用适用于手机的折叠导航或网页断点。

| 原生尺寸 | Windows WPF | macOS SwiftUI |
| --- | --- | --- |
| 默认窗口 | 1160 × 840 | 1280 × 850 |
| 最小窗口 | 1000 × 740 | 1040 × 700 |
| 侧栏内边距 | 横向 18 | 22 |
| 内容外围 | 横向 30；顶部 24；底部 18 | 28 |
| 普通卡片内边距 | 22 | 22 |
| 内容组间距 | 主要为 16 / 20 | 主要为 16 / 18 / 20 |

窗口缩小时保留侧栏与内容阅读顺序，长表单及检查结果纵向滚动。WPF 操作组使用 WrapPanel 按可用宽度换行，表格采用弹性列宽；Mac 在声明的最小宽度内保留 HStack 操作组，不虚构移动布局。Mac 自身截图中 1280 × 850 与最小尺寸的证据分别保存在 review 架构目录。

订阅列表使用单列伸展卡片，按名称／节点数、已用量与比例、流量轨道、剩余量／到期／更新时间的顺序阅读。Windows 卡片之间间距为 12、内边距为横向 18／纵向 16；Mac 卡片之间为 16、四周内边距为 18。订阅卡片高度由内容决定，不套用节点网格的固定密度。Windows 操作组位于列表滚动区外；Mac 沿用 RootView 的原生滚动内容，不声称工具栏固定。

节点页按操作及一键测速、搜索及地区／分组筛选与延迟排序、常驻地区和测速说明、卡片网格、数量及测速进度的阅读顺序排列。两端节点网格均按可用宽度每 190 个逻辑单位的预算计算列数，取整后限制为 1–4 列；用户窗口宽度及更宽时保持最多四列，较窄时减少列数。Windows 使用 ListBox／WrapPanel，先从网格宽度扣除系统纵向滚动条宽度和 4，再按 `floor(可用宽度 / 列数) - 8` 设置卡片宽度；Mac 使用 LazyVGrid 的等宽 flexible 列，由实际 `gridWidth` 在出现和宽度变化时重排。卡片间距为 8、内边距为 10，内部内容高度为 70；以名称、地区与来源／路径、协议与状态三行保留全部信息，完整卡片高度较 1.3.2 约再缩短 20%，保持纵向浏览和原有筛选、选择行为。这些密度参数仅适用于节点卡片，普通任务卡片仍使用 22 内边距。

表单标签、控件与辅助说明保持相邻，间距使用已出现的 6／8／10／12；组之间使用 16／20，普通任务卡片使用 22。日志从容器顶部开始，较早记录在上，新记录追加在下；新记录到达时可滚动到尾部，不倒置时间顺序。

## Elevation & Depth

默认工作面是平面的。白色卡片通过 1 个逻辑单位的 Line 边界、浅冷背景与留白形成层次，输入和结果底色提供局部关系。应用没有自定义卡片投影、模糊玻璃或位移阴影。macOS sheet／系统窗口、Windows 系统对话框的原生层级由操作系统绘制。

**The 平面分层 Rule.** 工作面依靠背景色、细边界与间距形成层级；不为卡片添加装饰性阴影。系统窗口与弹层的原生阴影由操作系统管理。

当前实现没有应用自定义过渡时长或缓动令牌。WPF 下拉弹出动画明确为 None；进度指示器使用原生行为。不要从浏览器面板预览的状态样式推断产品存在 CSS 或动画系统。

## Shapes

卡片以柔和圆角（12）收拢任务；WPF 按钮、输入、下拉与两端检查行采用较紧的圆角（8），WPF 下拉选项为（6）。Windows 连接徽章采用（14），Mac 使用原生 Capsule；胶囊仅用于简短状态，不作为所有按钮的轮廓。

控件边界主要为 1 个逻辑单位。Windows 键盘聚焦按钮边界增为 2；输入、密码框和选择器焦点改为 Accent 边界。图形是 WPF Path 或 OS 提供的 SF Symbols，不使用文字字符代替检查行图标。

应用标志的唯一几何来源为 `design/brand.svg`：深炭圆角底、珊瑚开口环、米白向上箭头。侧栏显示 42 × 42；WPF 由 `BrandAssets.Image` 从内嵌 SVG 读取几何并冻结为 DrawingImage，Mac 显示应用包图标。`scripts/create_icon.ps1` 先构建，再用项目 SDK 启动渲染器 DLL；`BrandAssets.Export` 对每个尺寸以 4 倍超采样渲染并高质量缩小，输出 1024px `App.png` 及 16／24／32／48／64／128／256 七尺寸 `App.ico`。PNG 内含来源元数据；Mac 构建从该 PNG 生成 ICNS。`create_icon.py` 仅是调用 PowerShell 脚本的兼容入口。安装器针对应用及快捷方式发出 `SHChangeNotify` 更新通知。标志不扩展成操作图标集，没有经批准的独立栅格界面合成稿。

## Components

### Buttons

主要行动是蓝色填充配白色 Semibold 文字；次要行动使用浅冷灰、Ink 和细边界。WPF 普通按钮内边距为横向 16、纵向 10，最小高度 40；底部恢复等紧凑入口有局部的较小尺寸。Mac 使用 `.borderedProminent` 与系统按钮，tint 为 Accent，尺寸和键盘反馈由系统控制。

WPF 悬停时边框容器透明度为 0.82，按下为 0.68，禁用为 0.42；键盘焦点为 Accent 的 2 单位边界。当前并未消费颜色文件中的 AccentHover，不把保留色值描述成已经使用的悬停背景。Mac 的 hover、press、focus、disabled 保留原生行为；本次只核对代码及渲染，未完成 Mac 键盘全流程测试。

### Inputs / Fields

WPF TextBox、PasswordBox 和 ComboBox 使用白底、Ink、Line 边界与圆角 8，普通最小高度 40，文字／选择控件的内边距为横向 12、纵向 9。焦点使用 Accent 边界；TextBox 禁用时框架透明度为 0.5，ComboBox 禁用时框架为 0.5 且文字改为 Muted。保留原生选择、滚动、密码遮蔽和 ComboBox 弹出控件，不把输入框画成静态图片。

Mac 使用原生 roundedBorder TextField、Picker、Toggle 与 TextEditor。InputField 以非交互提示层保证空值提示可读，输入本体具备独立的 accessibilityLabel。空值节点／订阅表单的保存按钮禁用；异步操作和已连接会话根据业务状态禁用不可修改的配置。禁用不借用绿色或红色表达成功或错误。

首次安装的 TUN、严格预检、临时 IPv6-off 与时区匹配以已勾选选项呈现，语言为 en-US，浏览器自动选择并优先 Chrome；已有明确偏好继续显示其保存值。勾选表示会话预设，不是检测通过或系统已经修改；系统变更只随用户发起并完成授权的连接执行。

### Navigation

两端五页保持同一顺序：工作台、节点管理、订阅管理、环境设置、运行记录。默认文字为 Muted，当前项使用 Accent、AccentSubtle 与 Semibold；WPF 为文字按钮，Mac 在文字前使用 SF Symbols。Mac 不用网页导航壳替换系统窗口。

WPF 导航的键盘焦点使用独立 `RoundedFocus`：Accent 边界宽 2、圆角 9、外距 2。焦点提示与当前导航的浅蓝底分别保留，不通过隐藏焦点来改善选中外观。

### Cards / Lists

普通 Card 为 Surface、Line 细边界、圆角 12、内边距 22。状态摘要卡可减少内边距，保持相同材料。订阅管理使用下述流量卡片，Windows 保留原生 ListBox 单选，Mac 以原生 plain Button 选择卡片，动作集中于上方操作组。节点管理使用下述地区卡片；没有记录时显示文字引导，不展示假的节点或成功统计。

### Subscription quota cards

卡片沿用 Surface、Line 的 1 单位边界和圆角 12。名称为 17／Semibold，已用量为 14，比例及剩余量为 13／Semibold；节点数、到期与更新时间为 12，状态标签与“最近更新”标题为 11。名称只占一行，完整内容保留 tooltip／help；已用量和辅助说明随可用空间排布。

选择使用 AccentSubtle、Accent 边界与独立勾选路径／SF Symbol；不将卡片整片涂成警告色。Windows 悬停改变边界，键盘焦点再叠加 2 单位 Accent 边界，单选状态由原生 ListBox 维护；Mac 保留原生 Button 的键盘反馈。刷新保持存在的选中订阅 ID。未选中时更新选中、编辑与删除禁用；Windows 无订阅时更新全部也禁用。空列表保留添加订阅入口和文字说明。

流量条只在上传、下载与正数总量均可用时表示已用比例，显示范围限制为 0–100%。Windows 轨道高 8、端部圆角 4，Mac 使用原生 ProgressView；总量未知或信息不完整时，两端改为纯灰 Line 轨道，文字保留“暂无比例／剩余流量未知”，不向读屏提供虚假的 0% 进度。剩余量不小于零，超出总量时保留“已超出总量”文字。

普通配额使用 Accent；已用达到 80% 或三天内到期时使用 Warning／WarningBg，已过期或配额用尽时使用 Error／ErrorBg。状态标签与进度条保留相同语义，选中底色不能覆盖警告和错误。到期、流量、节点数和最近更新时间分别呈现，服务商未提供的字段保持未知；旧版汇总仅作为有提示的估算，不暗示已经获得精确新数据。

Windows 行的可访问名称包含名称、节点数、已用量、剩余量、到期和状态；Mac 卡片名称包含名称、已用量、剩余量和到期，并通过 accessibilityValue 表达选择。两端数值进度控件都有“订阅流量使用比例”标签；未知轨道是装饰性结构，不作为可操作控件。源码具备这些属性不等于已完成用户设备读屏验收。

**The 配额事实 Rule.** 进度表示服务商给出的流量比例；未知不表示零或无限，选中不表示可连接，配额不表示 Claude 账户额度。

### Node region cards

节点卡片是用户 NaiLink 参考所指定的局部信息组织，压为三行：首行为地区国旗或未知地区图形、节点名称与选择图形；中间为“地区 · 来源分组”或“地区 · 链路路径”；底部为协议、独立的当前选择标识及延迟状态。名称采用 14／Semibold，合并辅助行与协议为 12，状态为 11／Semibold；内容高度为 70、内边距为 10，沿用圆角 12。长名称与合并辅助行单行截断，完整内容保留原生提示：Windows 的 `NodeRow.Subtitle` 同时提供中间行显示文字和 tooltip，Mac 的 `NodeTile.subtitle` 同时提供显示文字和 `.help(subtitle)`；名称保留原有 tooltip／help。链路显示“链式 · 2 跳”，地区按最终出口节点的名称推断，不删除地区、来源或路径等信息类别。

卡片底部状态位显示有效延迟数值及 `ms`，或“未测速”“测速中”“超时”“失败”“已取消”“节点失效”。有效延迟不大于 300 ms 使用 Success／SuccessBg，大于 300 至 1000 ms 使用 Warning／WarningBg，大于 1000 ms 使用 Error／ErrorBg；超时用 Warning，失败及失效用 Error，未测、进行中和取消用 Pending。数值、单位和状态文字必须与颜色一起保留；这些阈值仅用于比较响应耗时，不表示 Claude 账户可用或节点地区／住宅属性已验证。

当前选择由 AccentSubtle 工作面、Accent 边界、选中图形及协议旁的“当前”共同表达，延迟徽章不再承担选择状态。失效链路仍优先显示“节点失效”。选中表示保存的节点选择，不表示连接或检查通过。WPF 以圆环和勾选 Path 表达选择，未知地区用椭圆及地球经纬线几何；Mac 使用 circle、checkmark.circle.fill 和 globe SF Symbols。图形与文字配合，不用文字字符充当选择图标。WPF 悬停使用 SurfaceAlt 与 Accent 边界，键盘焦点边界增为 2；Mac 保留原生按钮焦点行为。

Mac `NodeChoice` 的可访问名称提供节点名称、地区与协议，动态 `accessibilityValue` 另提供“当前选择／未选择”和毫秒数或未测、进行中、超时、失败、取消、失效状态。选择与测速结果必须分别可读，不能只保留静态名称而丢失变化值。此项记录源码绑定，不扩大为用户 Mac 的 VoiceOver 实机验收。

搜索名称、地区、协议或分组，并与地区及分组筛选组合使用。筛选区下方常驻“地区按名称识别，实际出口以连接检测为准；测速为 HTTP 延迟，不代表下载速度或账户可用性。”，不能仅放进悬停提示。底部显示已显示／总数及链路数，空库与无匹配节点使用不同引导；Mac 在有筛选时另提供“清空筛选”。单击卡片保存当前选择并清除旧检测报告；Windows 双击或“前往工作台”另进入工作台。连接中的会话先断开再切换，不能把选择动作解释成立即连接。

42 个内置国旗为未经修改的 Twemoji v15.1.0 PNG，地区名称映射取自 NaiLink 的名称提示规则；来源与许可见 `docs/THIRD-PARTY-NOTICES.md`、`vendor/Twemoji-LICENSE.txt` 和 `vendor/NaiLink-LICENSE.txt`。这些素材不是 AI 生成图，名称映射不构成地理位置或住宅 IP 检测。

### Node latency actions

“一键测速”测试点击时当前筛选出的全部节点；链式节点按完整两跳路径测量。开始后显示完成数／本批总数，按钮变为“取消测速”并保持可用，连接、选择和节点／订阅编辑操作暂不可执行。已连接时先断开再测速，不为测试切换当前节点或修改 TUN、IPv6、系统时区。取消保留已完成结果，未完成项显示取消；结束后恢复相关操作。空筛选结果不启动内核，并给出无节点反馈。

延迟排序提供原始顺序、低到高、高到低三种选择。两种延迟排序均将有效毫秒值放在前面，未测、失败等无数值状态放在后面；相同值保持原先相对顺序。排序不能修改当前节点 ID。结果仅缓存在本次应用进程；节点或链路任一跳的连接配置变化后，旧结果不再展示为有效测速。

测试经独立、非管理员内核访问 `https://www.gstatic.com/generate_204`，快速测速每请求等待上限 3 秒，最多同时测试 12 条不同线路。完整线路配置完全相同的节点共享本次新测试，仍保留每个节点的独立身份、缓存校验和结果；不以旧缓存冒充新测。超时只表示本轮 3 秒内未及时返回，可重试，不直接断言节点永久不可用。界面称为 HTTP 端到端延迟，不将其包装为裸 TCP／ping、下载带宽、Claude 登录测试或账户安全评估。两端原生控件和进度位置保持各自实现，不把文档预览的网页控件当成产品运行层。

网络回调先收集结果，按 100 ms 批次回显，不能让网络工作线程同步等待整表重绘。Windows 通过 `INotifyPropertyChanged` 更新既有卡片，只在需要排序时使用 `ObservableCollection.Move` 移动条目，保持集合、行对象与当前选择；Mac 的每主机连接上限同步为 12，收集后批量发布。完成或取消前最后清空待回显结果，保留已完成项及动态可访问结果值，不因提速丢失结果或取消入口。

### Two-hop chain editor

两端沿用白色表单、Ink 标签、Muted 辅助说明和蓝色保存按钮。链路名称初始为中性的“新建双跳链路”；“入口节点”和“最终出口”为分别命名的原生选择器，下方浅蓝路径块按“本机 → 入口节点 → 最终出口 → 目标网站”显示方向，工作台也显示已选链路的完整方向。Windows 对话框宽 650，Mac sheet 内容宽 600；两端内边距为 28，路径块内边距 14、圆角 10。路径指明用户所选的节点，不为它们背书住宅属性。

仅保存两个有效且不同的普通节点，拒绝缺失、重复或嵌套链路；缺少任一跳时保持无效，不替换出口或绕过失败入口。保存失败在表单内使用 Error 文字说明，保留输入与取消入口。Windows 取消为默认／取消动作，Mac 保存为默认动作、取消为 Escape；不把两端原生键盘分配写成统一行为。HTTP 出口用于网页和 API 等 TCP 流量；UDP 需要节点额外支持，该限制常驻路径下方。

链路保存为节点记录，其 `ChainNodeIds` 按入口、最终出口顺序引用已有节点 ID；`SelectedNodeId` 保存所选链路 ID，沿用 Windows 用户加密与 Mac Keychain 支持的本机设置存储。链路不复制入口或出口凭据。保存成功选中该链路并清空搜索、地区及分组筛选；筛选是当前界面状态，不承诺跨重启持久化。

### Check rows / Status badges

检查行由图形、检查名、结果值、细节和状态组成；颜色语义见 Colors。Windows 行内边距横向 12／纵向 11，Mac 为 12；两端结果支持换行，保持与状态标签分离。Mac 徽章包含系统图形，横向内边距 10、纵向 7；Windows 顶部徽章横向 12、纵向 7。

断开和恢复后旧结果必须清除。当前 WPF UpdateState 已将出口和 API 摘要文字及前景一起重置为 Pending；Mac 摘要从当前 report 和 connected 派生。界面示例数据须显示“仅用于核对样式”等说明。

### Feedback / Dialogs / Logs

更新与进行中反馈使用 InfoBg；未完成恢复记录使用 WarningBg，给出可见恢复入口。忙碌时呈现进度文字和原生进度条，可取消的检测显示取消操作；错误包含可理解的文字。Windows 节点窗有标题、滚动表单、行内 Error 提示和底部取消／主操作；Mac 使用原生 sheet、一般 alert 与文件选择器。空白保存维持禁用，取消仍保持次要按钮身份。

应用自有 Windows 提示与确认使用 `AppDialog`，取代经典 MessageBox。白色窗体宽 600，标题 21／Semibold，语义图形置于 42 × 42、圆角 10 的浅色块中；头部内边距为横向 26／顶部 24。正文纵向滚动区最高 360，底部操作区使用 SurfaceAlt、Line 顶边界与横向 26／纵向 17 的内边距。信息和错误提示保留相应图形与语义色；单按钮提示以“确定”关闭。

启动确认先呈现 API HTTPS 传输结果，再列出未通过或未验证的项目。API 401 可显示网络可达，同时明确尚未验证账户授权；`https://claude.ai/login` 的自动探测 403 使用注意色并提示浏览器核对，不能据此认定浏览器打不开或账号被停用。尾注说明继续启动不改变检测结论，且检查不判断账号封禁风险。底部“返回检查”为次要按钮、默认动作和取消动作，“仍要启动”为蓝色明确选择；两种选择均保留检测报告。

Mac 的 `LaunchConfirmationView` 使用原生 sheet，宽 620、内边距 26，保留同样的标题、API 结果、注意列表和尾注层级；超过三条原因时列表以 280 高度滚动。Return 的默认动作为“返回检查”，Escape 返回检查。两端长内容只滚动内容区，底部操作始终可见；Windows 用独立底栏，Mac 用 Divider 分隔原生按钮组。此处键盘行为来自代码，Mac 尚未完成真实键盘全流程验收。

Windows 日志使用只读 RichTextBox／FlowDocument，内边距 18；Mac 使用 ScrollView／LazyVStack，内边距 20。两者均保留时间、级别和正文，长文字自然折行，支持选择；信息日志正文用 Ink，成功、注意和错误正文使用各自语义前景。Mac 级别 Label 额外带 SF Symbols，Windows 级别是文字。

`.impeccable/design.json` 的 HTML/CSS 仅供设计面板隔离预览：提取 WPF 的主要／次要按钮、输入、导航、卡片、检查行、日志、原始矢量标志、启动确认、节点卡片与双跳编辑材料。节点预览使用未知地区的内联地球图形，不替代产品内置国旗。预览不作为产品实现或原生交互验收，不能用浏览器预览替代 WPF／SwiftUI 截图或原生操作测试。

## Do's and Don'ts

### Do:

- **Do** 使用共享颜色键，先修改 design/tokens.json，再同步两端资源。
- **Do** 在检查行同时保留状态文字、矢量图形和对应的前景／浅底色。
- **Do** 在断开或恢复后清除旧检测结果与旧结果颜色，重新显示灰色待检测。
- **Do** 保持日志正文大小与从上到下的时间顺序，允许复制文字和纵向滚动。
- **Do** 保留原生焦点反馈、可访问名称、禁用状态及系统授权流程。
- **Do** 从 design/brand.svg 派生标志资源，保持暖色身份资产与操作蓝的边界。
- **Do** 在启动确认中分开呈现 API 传输结果、待核对项目和用户决定，并让返回检查与继续启动在长内容下仍可见。
- **Do** 在节点筛选区常驻名称推断地区及测速边界的说明，同时保留延迟数值／状态文字、独立选择图形和空结果引导。
- **Do** 让节点网格按 190 宽度预算保持最多四列，以 8 间距、10 内边距、70 内容高度和 14／Semibold 名称提高密度，按三行同时保留旗帜、地区、来源或路径、协议、状态和完整内容提示。
- **Do** 在测速期间保留取消和进度，将无数值结果排在有效延迟后面，排序后保持当前选择。
- **Do** 以有界并发、短超时及 100 ms 批量回显改善测速等待，复用已有卡片并在完成／取消时清空待回显结果。
- **Do** 在订阅卡片中同时保留流量比例、剩余量、到期及更新时间；用浅蓝底和勾选表达选择，独立保留警告颜色与键盘焦点。
- **Do** 在没有可选订阅时禁用依赖选择的操作，同时保留添加入口和未知信息说明。
- **Do** 在链路编辑与工作台显示本机、入口、最终出口、目标网站的顺序，并将校验错误留在表单内。

### Don't:

- **Don't** 把 API 401、网页 403 或代理启动合并成账户已获授权的结论。
- **Don't** 从网页自动探测 403 推断浏览器访问失败、账号停用或封禁风险。
- **Don't** 用颜色单独表达检查结果，或用绿色填充尚未验证的结果。
- **Don't** 以网页样式替换 macOS 原生控件，也不要把 WPF 控件高度套用到 Mac。
- **Don't** 为操作界面新增装饰字体、大面积渐变、卡片阴影或与结果无关的状态颜色。
- **Don't** 把界面示例截图当作用户网络、登录、键盘操作或管理员授权已经验证的证据。
- **Don't** 把国旗、名称推断地区或“当前选择”当成实测出口、住宅 IP 或连接成功。
- **Don't** 把较低延迟、绿色测速徽章或排序首位解释为下载带宽、Claude 账户可用性或封禁风险结论。
- **Don't** 在链路缺失或入口失败时替换出口、旁路直连，或把 HTTP 出口描述为已验证支持 UDP。
- **Don't** 把未知订阅流量画成 0% 或无限流量，也不要把服务商配额当成节点连通性、Claude 账户额度或安全结论。

未提升为系统规则的遗留项：Windows 标志下的 `NETWORK LAUNCHER` 小号英文眉题仅记录为本版残留排法，不推广成新页面的标题模式；未消费的 AccentHover 不作为交互规范。此次仅文档化，未修改这些产品代码。
