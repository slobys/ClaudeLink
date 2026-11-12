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

表单标签、控件与辅助说明保持相邻，间距使用已出现的 6／8／10／12；组之间使用 16／20，卡片统一使用 22。日志从容器顶部开始，较早记录在上，新记录追加在下；新记录到达时可滚动到尾部，不倒置时间顺序。

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

### Cards / Lists

普通 Card 为 Surface、Line 细边界、圆角 12、内边距 22。状态摘要卡可减少内边距，保持相同材料。Windows 节点／订阅使用原生只读 DataGrid：行高 48、全行单选、无网格线、SurfaceAlt 表头与交替行、AccentSubtle 选中行。Mac 使用卡片内的横向记录和 Divider，保留原生行内按钮。没有记录时显示文字引导，不展示假的节点或成功统计。

### Check rows / Status badges

检查行由图形、检查名、结果值、细节和状态组成；颜色语义见 Colors。Windows 行内边距横向 12／纵向 11，Mac 为 12；两端结果支持换行，保持与状态标签分离。Mac 徽章包含系统图形，横向内边距 10、纵向 7；Windows 顶部徽章横向 12、纵向 7。

断开和恢复后旧结果必须清除。当前 WPF UpdateState 已将出口和 API 摘要文字及前景一起重置为 Pending；Mac 摘要从当前 report 和 connected 派生。界面示例数据须显示“仅用于核对样式”等说明。

### Feedback / Dialogs / Logs

更新与进行中反馈使用 InfoBg；未完成恢复记录使用 WarningBg，给出可见恢复入口。忙碌时呈现进度文字和原生进度条，可取消的检测显示取消操作；错误包含可理解的文字。Windows 节点窗有标题、滚动表单、行内 Error 提示和底部取消／主操作；Mac 使用原生 sheet、一般 alert 与文件选择器。空白保存维持禁用，取消仍保持次要按钮身份。

应用自有 Windows 提示与确认使用 `AppDialog`，取代经典 MessageBox。白色窗体宽 600，标题 21／Semibold，语义图形置于 42 × 42、圆角 10 的浅色块中；头部内边距为横向 26／顶部 24。正文纵向滚动区最高 360，底部操作区使用 SurfaceAlt、Line 顶边界与横向 26／纵向 17 的内边距。信息和错误提示保留相应图形与语义色；单按钮提示以“确定”关闭。

启动确认先呈现 API HTTPS 传输结果，再列出未通过或未验证的项目。API 401 可显示网络可达，同时明确尚未验证账户授权；`https://claude.ai/login` 的自动探测 403 使用注意色并提示浏览器核对，不能据此认定浏览器打不开或账号被停用。尾注说明继续启动不改变检测结论，且检查不判断账号封禁风险。底部“返回检查”为次要按钮、默认动作和取消动作，“仍要启动”为蓝色明确选择；两种选择均保留检测报告。

Mac 的 `LaunchConfirmationView` 使用原生 sheet，宽 620、内边距 26，保留同样的标题、API 结果、注意列表和尾注层级；超过三条原因时列表以 280 高度滚动。Return 的默认动作为“返回检查”，Escape 返回检查。两端长内容只滚动内容区，底部操作始终可见；Windows 用独立底栏，Mac 用 Divider 分隔原生按钮组。此处键盘行为来自代码，Mac 尚未完成真实键盘全流程验收。

Windows 日志使用只读 RichTextBox／FlowDocument，内边距 18；Mac 使用 ScrollView／LazyVStack，内边距 20。两者均保留时间、级别和正文，长文字自然折行，支持选择；信息日志正文用 Ink，成功、注意和错误正文使用各自语义前景。Mac 级别 Label 额外带 SF Symbols，Windows 级别是文字。

`.impeccable/design.json` 的 HTML/CSS 仅供设计面板隔离预览：提取 WPF 的主要／次要按钮、输入、导航、卡片、检查行、日志、原始矢量标志与启动确认材料。预览不作为产品实现或原生交互验收，不能用浏览器预览替代 WPF／SwiftUI 截图或原生操作测试。

## Do's and Don'ts

### Do:

- **Do** 使用共享颜色键，先修改 design/tokens.json，再同步两端资源。
- **Do** 在检查行同时保留状态文字、矢量图形和对应的前景／浅底色。
- **Do** 在断开或恢复后清除旧检测结果与旧结果颜色，重新显示灰色待检测。
- **Do** 保持日志正文大小与从上到下的时间顺序，允许复制文字和纵向滚动。
- **Do** 保留原生焦点反馈、可访问名称、禁用状态及系统授权流程。
- **Do** 从 design/brand.svg 派生标志资源，保持暖色身份资产与操作蓝的边界。
- **Do** 在启动确认中分开呈现 API 传输结果、待核对项目和用户决定，并让返回检查与继续启动在长内容下仍可见。

### Don't:

- **Don't** 把 API 401、网页 403 或代理启动合并成账户已获授权的结论。
- **Don't** 从网页自动探测 403 推断浏览器访问失败、账号停用或封禁风险。
- **Don't** 用颜色单独表达检查结果，或用绿色填充尚未验证的结果。
- **Don't** 以网页样式替换 macOS 原生控件，也不要把 WPF 控件高度套用到 Mac。
- **Don't** 为操作界面新增装饰字体、大面积渐变、卡片阴影或与结果无关的状态颜色。
- **Don't** 把界面示例截图当作用户网络、登录、键盘操作或管理员授权已经验证的证据。

未提升为系统规则的遗留项：Windows 标志下的 `NETWORK LAUNCHER` 小号英文眉题仅记录为本版残留排法，不推广成新页面的标题模式；未消费的 AccentHover 不作为交互规范。此次仅文档化，未修改这些产品代码。
