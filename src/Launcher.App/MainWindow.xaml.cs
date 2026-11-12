using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.Win32;
using Launcher.App.Services;
using Forms = System.Windows.Forms;

namespace Launcher.App;

public partial class MainWindow : Window
{
    private readonly LauncherSettings settings;
    private readonly SettingsStore store = new();
    private readonly SessionController session = new();
    private readonly Queue<LogRecord> logLines = new();
    private readonly Forms.NotifyIcon tray;
    private readonly bool smoke;
    private bool busy, refreshing, allowClose, activeTun, activeIPv6, activeTimeZone;
    private Process? claudeProcess;
    private DesktopInstallation? desktopInstallation;
    private bool desktopOpened;
    private CancellationTokenSource? networkCancellation;
    private NetworkReport? report;
    private bool ClaudeRunning => claudeProcess is not null;
    private bool DesktopMode => LaunchTargetCombo.SelectedValue as string != "cli";

    public MainWindow(LauncherSettings initialSettings, bool smokeMode = false)
    {
        settings = initialSettings; smoke = smokeMode;
        InitializeComponent();
        LanguageCombo.ItemsSource = new[] { "en-US", "zh-CN", "en-GB", "ja-JP" };
        BrowserCombo.ItemsSource = new[] { new BrowserChoice("auto", "自动（优先 Chrome）"), new BrowserChoice("chrome", "Chrome"), new BrowserChoice("edge", "Edge") };
        BrowserCombo.SelectedValue = settings.CheckBrowser;
        LaunchTargetCombo.ItemsSource = new[] { new BrowserChoice("desktop", "Claude 桌面客户端（推荐）"), new BrowserChoice("cli", "Claude Code 命令行（CLI）") };
        LaunchTargetCombo.SelectedValue = settings.LaunchTarget;
        DesktopPathBox.Text = settings.DesktopPath;
        AutoUpdateCheck.IsChecked = settings.AutoCheckUpdates;
        PortBox.Text = settings.ProxyPort.ToString(); ClaudePathBox.Text = settings.ClaudePath;
        LanguageCombo.SelectedItem = settings.ProcessLanguage;
        TunCheck.IsChecked = DesktopMode || settings.TunEnabled; StrictCheck.IsChecked = settings.StrictChecks;
        IPv6Check.IsChecked = settings.DisableIPv6; TimeZoneCheck.IsChecked = settings.MatchTimeZone;
        ProjectBox.ItemsSource = settings.RecentProjects; ProjectBox.Text = settings.ProjectDirectory;
        RefreshLists(); Navigate("Dashboard");
        tray = new Forms.NotifyIcon { Text = "Claude Link · 网络启动器", Visible = false };
        try { tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!); }
        catch { tray.Icon = System.Drawing.SystemIcons.Application; }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示启动器", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("退出并恢复环境", null, (_, _) => Dispatcher.Invoke(async () => await ExitFromTrayAsync()));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        session.UnexpectedExit += message => Dispatcher.Invoke(() =>
        {
            report = null; desktopOpened = false; Log(message, LogKind.Error); StatusText.Text = message; UpdateState();
        });
        Closed += (_, _) => { updateCancellation?.Cancel(); tray.Dispose(); };
        Loaded += async (_, _) =>
        {
            if (smoke) return;
            await RefreshDesktopAsync();
            if (settings.AutoCheckUpdates) await CheckUpdatesAsync(true);
        };
        UpdateState();
        Log("启动器已就绪。配置使用当前 Windows 用户加密保存。");
    }

    public void Navigate(string page)
    {
        DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        NodesPage.Visibility = page == "Nodes" ? Visibility.Visible : Visibility.Collapsed;
        SubscriptionsPage.Visibility = page == "Subscriptions" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        LogsPage.Visibility = page == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        var labels = new Dictionary<string, (string, string)>
        {
            ["Dashboard"] = ("工作台", "从节点到 Claude Code，一处准备就绪。"),
            ["Nodes"] = ("节点管理", "集中管理分享链接、手动节点与订阅节点。"),
            ["Subscriptions"] = ("订阅管理", "添加一次，随时更新。节点内容在本机解析。"),
            ["Settings"] = ("环境设置", "按需选择本次会话的代理与临时环境。"),
            ["Logs"] = ("运行记录", "查看最近的操作结果与恢复状态。")
        };
        (PageTitle.Text, PageSubtitle.Text) = labels[page];
        foreach (var button in new[] { NavDashboard, NavNodes, NavSubscriptions, NavSettings, NavLogs })
        {
            var selected = (string)button.Tag == page;
            button.Background = selected ? (Brush)FindResource("AccentSubtle") : Brushes.Transparent;
            button.Foreground = (Brush)FindResource(selected ? "Accent" : "Muted");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => Navigate((string)((Button)sender).Tag);

    private void RefreshLists()
    {
        refreshing = true;
        var selected = settings.Nodes.FirstOrDefault(x => x.Id == settings.SelectedNodeId) ?? settings.Nodes.FirstOrDefault();
        settings.SelectedNodeId = selected?.Id;
        NodeCombo.ItemsSource = null; NodeCombo.ItemsSource = settings.Nodes; NodeCombo.SelectedItem = selected;
        NodesGrid.ItemsSource = settings.Nodes.Select(x => new NodeRow(x, settings.Subscriptions.FirstOrDefault(s => s.Id == x.GroupId)?.Name ?? "手动添加")).ToList();
        SubscriptionsGrid.ItemsSource = settings.Subscriptions.Select(x => new SubscriptionRow(x, settings.Nodes.Count(n => n.GroupId == x.Id))).ToList();
        NodesEmpty.Visibility = settings.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubscriptionsEmpty.Visibility = settings.Subscriptions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NodeCountText.Text = $"{settings.Nodes.Count} 个节点 · {settings.Subscriptions.Count} 个订阅";
        refreshing = false;
    }
    private void NodeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!refreshing && NodeCombo.SelectedItem is ProxyNode node) { settings.SelectedNodeId = node.Id; report = null; }
    }
    private void SaveUISettings()
    {
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1024 or > 65535) throw new FormatException("本地端口应为 1024—65535。");
        settings.ProxyPort = port; settings.ClaudePath = ClaudePathBox.Text.Trim().Trim('"');
        settings.ProcessLanguage = LanguageCombo.SelectedItem as string ?? "en-US";
        settings.CheckBrowser = BrowserCombo.SelectedValue as string ?? "auto";
        settings.LaunchTarget = DesktopMode ? "desktop" : "cli";
        settings.DesktopPath = DesktopPathBox.Text.Trim().Trim('"');
        settings.AutoCheckUpdates = AutoUpdateCheck.IsChecked == true;
        settings.ProjectDirectory = ProjectBox.Text.Trim().Trim('"');
        settings.TunEnabled = TunCheck.IsChecked == true; settings.StrictChecks = StrictCheck.IsChecked == true;
        settings.DisableIPv6 = IPv6Check.IsChecked == true; settings.MatchTimeZone = TimeZoneCheck.IsChecked == true;
        if (NodeCombo.SelectedItem is ProxyNode node) settings.SelectedNodeId = node.Id;
        store.Save(settings);
    }
    private void Log(string text, LogKind kind = LogKind.Info)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"https?://\S+", "[地址已隐藏]");
        var line = $"{DateTime.Now:HH:mm:ss}  {text}";
        logLines.Enqueue(new(DateTime.Now, text, kind)); while (logLines.Count > 250) logLines.Dequeue();
        LogBox.Document.Blocks.Clear();
        foreach (var entry in logLines)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 9), LineHeight = 26 };
            paragraph.Inlines.Add(new Run(entry.Time.ToString("HH:mm:ss") + "   ") { Foreground = (Brush)FindResource("Muted"), FontFamily = new FontFamily("Consolas") });
            paragraph.Inlines.Add(new Run(entry.Label + "  ") { Foreground = (Brush)FindResource(entry.BrushKey), FontWeight = FontWeights.SemiBold });
            paragraph.Inlines.Add(new Run(entry.Message) { Foreground = entry.Kind == LogKind.Info ? (Brush)FindResource("Ink") : (Brush)FindResource(entry.BrushKey) });
            LogBox.Document.Blocks.Add(paragraph);
        }
        LogBox.ScrollToEnd();
        if (smoke) return;
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            var path = Path.Combine(AppPaths.Root, "operations.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (IOException) { }
    }
    private void UpdateState()
    {
        NodeCombo.IsEnabled = !session.Connected && !ClaudeRunning;
        TunCheck.IsEnabled = !session.Connected && !ClaudeRunning && !DesktopMode;
        TunCheck.Content = DesktopMode ? "TUN 虚拟网卡（桌面模式必需）" : "TUN 虚拟网卡";
        LaunchTargetCombo.IsEnabled = !session.Connected && !ClaudeRunning;
        PortBox.IsEnabled = IPv6Check.IsEnabled = TimeZoneCheck.IsEnabled = !session.Connected && !ClaudeRunning;
        LaunchButton.IsEnabled = !ClaudeRunning;
        LaunchButton.Content = DesktopMode ? "准备并打开 Claude 桌面客户端" : "准备并启动 Claude Code CLI";
        ConnectButton.IsEnabled = !ClaudeRunning;
        ConnectButton.Content = session.Connected ? "重新检测" : "连接并检测";
        DisconnectButton.IsEnabled = session.Connected && !ClaudeRunning;
        BrowserButton.IsEnabled = session.Connected;
        ConnectionBadge.Text = ClaudeRunning ? "Claude 运行中" : session.Connected
            ? report?.ApiReachable == true ? "API HTTPS 可达" : "本地代理已启动" : "未连接";
        var badgeKind = session.Connected ? report?.ApiReachable == true ? "Success" : "Info" : "Pending";
        ConnectionBadge.Foreground = (Brush)FindResource(badgeKind);
        ConnectionBadgeBorder.Background = (Brush)FindResource(badgeKind + "Bg");
        ProxyMetric.Text = session.Connected ? $"127.0.0.1:{session.Port}" : "未连接";
        TunMetric.Text = session.Connected ? activeTun ? "TUN 已启用" : "进程代理" : TunCheck.IsChecked == true ? "TUN 待启用" : "进程代理";
        ClaudeHint.Text = DesktopMode
            ? desktopOpened && session.Connected ? "桌面客户端已打开；网络保持到你点击断开，或从托盘退出并恢复。"
                : desktopInstallation != null ? "已找到官方 Claude 桌面客户端；打开 Code 页面后由 Claude 确认项目文件夹。"
                    : "桌面模式使用 TUN；客户端会自动查找，也可在环境设置指定桌面 EXE。"
            : ClaudeRunning ? "Claude CLI 终端运行中；正常退出后自动断开并恢复。关闭本窗口会进入托盘。"
                : ClaudeTerminal.FindClaude(settings.ClaudePath) is not null ? "已找到 Claude Code CLI，将在独立终端中打开。" : "尚未找到 Claude Code CLI，请在环境设置指定命令行路径。";
        RecoveryBanner.Visibility = SessionController.PendingRecovery().Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!session.Connected && report is null)
        {
            ExitMetric.Text = "待检测"; ApiMetric.Text = "待检测";
            ExitMetric.Foreground = ApiMetric.Foreground = (Brush)FindResource("Pending");
        }
    }
    private void LaunchTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (DesktopMode && !session.Connected) TunCheck.IsChecked = true;
        UpdateState();
    }
    private async Task RefreshDesktopAsync()
    {
        try { desktopInstallation = await ClaudeDesktop.FindAsync(settings.DesktopPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { desktopInstallation = null; }
        UpdateState();
    }
    private async Task RunBusyAsync(Func<Task> action, string message = "正在处理…")
    {
        if (busy) return;
        busy = true; Workspace.IsEnabled = false; ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = message; StatusText.Text = message;
        try { await action(); }
        catch (OperationCanceledException)
        {
            Log("操作已取消。", LogKind.Warning);
            if (!ClaudeRunning) { try { await session.StopAsync(); } catch (Exception ex) { Log(SafeError(ex), LogKind.Error); } }
            StatusText.Text = "操作已取消，已尝试恢复临时环境";
        }
        catch (Exception ex)
        {
            var text = SafeError(ex); Log(text, LogKind.Error); StatusText.Text = text;
            AppDialog.Notify(this, text, "操作未完成", "Error");
        }
        finally
        {
            busy = false; Workspace.IsEnabled = true; ProgressPanel.Visibility = Visibility.Collapsed;
            networkCancellation?.Dispose(); networkCancellation = null; CancelButton.IsEnabled = false; UpdateState();
        }
    }
    private static string SafeError(Exception ex) => ex switch
    {
        Win32Exception win when win.NativeErrorCode == 1223 => "已取消管理员授权，本次连接未启动。",
        HttpRequestException => "网络请求失败，请检查订阅地址或当前连接。原节点已保留。",
        TimeoutException => "等待操作超时，请检查连接或使用恢复临时设置。",
        CryptographicException => "本地配置无法解密，请使用保存配置时的 Windows 用户。",
        FormatException format when format is not UriFormatException && format.Message.Length < 160 => format.Message,
        IOException io when io.Message.Length < 180 && !io.Message.Contains("://") => io.Message,
        _ => "操作未完成，请检查输入、文件权限或网络后重试。"
    };

    private async Task EnsureConnectedAsync()
    {
        SaveUISettings();
        if (session.Connected) return;
        if (DesktopMode && !settings.TunEnabled) throw new IOException("桌面客户端模式需要启用 TUN，以覆盖客户端及其 Code 进程的网络。");
        if (ClaudeRunning) throw new IOException("请先正常退出当前 Claude 终端。");
        var node = settings.Nodes.FirstOrDefault(x => x.Id == settings.SelectedNodeId) ?? throw new IOException("请先添加并选择一个节点。");
        ProgressText.Text = settings.TunEnabled || settings.DisableIPv6 || settings.MatchTimeZone ? "正在启动内核；请在系统提示中允许管理员授权…" : "正在启动内置代理…";
        await session.StartAsync(settings, node);
        activeTun = settings.TunEnabled; activeIPv6 = settings.DisableIPv6; activeTimeZone = settings.MatchTimeZone;
        report = null; Log(activeTun ? "内置 TUN 与本地代理已启动。" : "内置进程代理已启动。", LogKind.Success);
        CheckUpdatesAfterConnection();
        UpdateState();
    }
    private async Task CheckNowAsync()
    {
        networkCancellation = new CancellationTokenSource();
        CancelButton.IsEnabled = true;
        report = await NetworkServices.CheckAsync(session.Port, activeTun, settings.StrictChecks || activeIPv6,
            new Progress<string>(text => ProgressText.Text = text), networkCancellation.Token);
        if (activeTimeZone && !report.TimeZoneMatches && report.ClaudeIP.Length > 0 && report.WindowsTimeZone.Length > 0)
        {
            ProgressText.Text = "正在临时匹配系统时区…";
            await session.ApplyTimeZoneAsync(report.WindowsTimeZone);
            report.TimeZoneMatches = true;
            var item = report.Results.First(x => x.Label == "系统时区"); item.Value = "已临时匹配"; item.State = "pass";
            Log("系统时区已临时匹配，结束会话后恢复。", LogKind.Success);
        }
        CancelButton.IsEnabled = false;
        CheckList.ItemsSource = null; CheckList.ItemsSource = report.Results;
        ExitMetric.Text = report.Country.Length > 0 ? report.Country : "未验证";
        ApiMetric.Text = report.ApiReachable ? "HTTPS 可达" : "未通过";
        ApiMetric.Foreground = (Brush)FindResource(report.ApiReachable ? "Success" : "Error");
        ExitMetric.Foreground = (Brush)FindResource(report.Country.Length > 0 ? "Success" : "Pending");
        var reasons = report.BlockingReasons(settings.StrictChecks);
        CheckSummary.Text = settings.StrictChecks
            ? report.StrictReady ? "严格预检通过。账户登录与授权仍由 Claude Code 处理。"
                : (report.BasicReady ? "API 基础网络可达；严格预检未通过：" : "严格预检未通过：") + string.Join("；", reasons)
            : report.BasicReady ? "基础网络可达。账户授权未验证；其他检查结果供核对。" : "基础检测未通过：" + string.Join("；", reasons);
        ProgressText.Text = "检测完成";
        if (session.DirectoryPath is { } path) AppPaths.WriteJson(Path.Combine(path, "network-report.json"), report);
        StatusText.Text = CheckSummary.Text;
        CheckSummary.Foreground = (Brush)FindResource(reasons.Count == 0 ? "Success" : report.BasicReady ? "Warning" : "Error");
        Log(report.BasicReady ? "基础出口与 API 连通性检测完成：可达。" : "基础出口与 API 连通性未全部通过。", report.BasicReady ? LogKind.Success : LogKind.Error);
        if (reasons.Count > 0) Log(string.Join("；", reasons), LogKind.Warning);
    }
    private async void Connect_Click(object sender, RoutedEventArgs e) => await RunBusyAsync(async () => { await EnsureConnectedAsync(); await CheckNowAsync(); }, "正在准备连接…");
    private async void Launch_Click(object sender, RoutedEventArgs e) => await RunBusyAsync(async () =>
    {
        SaveUISettings();
        string? executable = null;
        if (DesktopMode) desktopInstallation = await ClaudeDesktop.FindAsync(settings.DesktopPath)
            ?? throw new IOException("尚未找到 Claude 桌面客户端。请安装官方桌面应用，或在环境设置指定桌面 EXE。");
        else executable = ClaudeTerminal.FindClaude(settings.ClaudePath) ?? throw new IOException("尚未找到 Claude Code CLI，请检查命令行安装路径。");
        if (!Directory.Exists(settings.ProjectDirectory)) throw new IOException("项目文件夹不存在，请重新选择。");
        await EnsureConnectedAsync(); await CheckNowAsync();
        var ready = settings.StrictChecks ? report!.StrictReady : report!.BasicReady;
        ProgressPanel.Visibility = Visibility.Collapsed;
        if (!ready && !AppDialog.ConfirmLaunch(this, report!, settings.StrictChecks))
        { StatusText.Text = "已保留当前连接，尚未启动 Claude Code"; return; }
        settings.RecentProjects.RemoveAll(x => x.Equals(settings.ProjectDirectory, StringComparison.OrdinalIgnoreCase));
        settings.RecentProjects.Insert(0, settings.ProjectDirectory);
        settings.RecentProjects = settings.RecentProjects.Take(12).ToList();
        store.Save(settings);
        if (DesktopMode)
        {
            ClaudeDesktop.Open(desktopInstallation!, settings.ProjectDirectory);
            desktopOpened = true;
            Log("已请求打开官方 Claude 桌面客户端的 Code 页面；TUN 网络保持，使用完请点击断开。");
            StatusText.Text = "已打开 Claude 桌面客户端；请在客户端确认项目文件夹。";
            return;
        }
        claudeProcess = ClaudeTerminal.Start(executable!, settings.ProjectDirectory, session.DirectoryPath!, session.Port, settings.ProcessLanguage);
        Log(ready ? "Claude Code 终端已启动。" : "用户确认后启动 Claude Code；未通过项目保持原状态。");
        StatusText.Text = "Claude Code 运行中，退出后自动恢复";
        _ = WatchClaudeAsync(claudeProcess, session.DirectoryPath!);
    }, "正在准备 Claude Code…");
    private async Task WatchClaudeAsync(Process process, string directory)
    {
        try
        {
            while (!process.HasExited && !File.Exists(Path.Combine(directory, "claude-complete.json"))) await Task.Delay(500);
            var completion = AppPaths.ReadJson<Dictionary<string, int>>(Path.Combine(directory, "claude-complete.json"));
            Log(completion?.GetValueOrDefault("exitCode") is > 0 ? "Claude Code 已结束并返回错误，请查看终端。" : "Claude Code 会话已结束，正在恢复。");
            await session.StopAsync(); report = null;
            StatusText.Text = "会话已结束，临时设置已恢复";
        }
        catch (Exception ex) { Log(SafeError(ex), LogKind.Error); StatusText.Text = SafeError(ex); }
        finally { claudeProcess = null; process.Dispose(); UpdateState(); }
    }
    private async void Disconnect_Click(object sender, RoutedEventArgs e) => await RunBusyAsync(async () =>
    {
        await session.StopAsync(); desktopOpened = false; report = null; CheckList.ItemsSource = null; CheckSummary.Text = "已断开，临时环境已恢复。";
        StatusText.Text = "已断开，临时环境已恢复"; Log("已停止代理并恢复临时环境。", LogKind.Success);
    }, "正在断开并恢复…");
    private void Cancel_Click(object sender, RoutedEventArgs e) { networkCancellation?.Cancel(); CancelButton.IsEnabled = false; }
    private async void Browser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveUISettings();
            BrowserButton.IsEnabled = false;
            StatusText.Text = "正在打开核对页并开启隐藏 IP…";
            var opened = await ClaudeTerminal.OpenBrowserCheckAsync(session.DirectoryPath!, session.Port, settings.ProcessLanguage, settings.CheckBrowser);
            Log($"已打开专用 {opened.BrowserName} 核对窗口；所选语言 {opened.Language}；" + (opened.IpHidden ? "已开启页面隐藏 IP。" : "请在网页手动开启隐藏 IP。"));
            StatusText.Text = opened.IpHidden ? $"专用 {opened.BrowserName} 已打开核对页，页面 IP 已隐藏。" : "核对窗口已打开；未能自动确认隐藏 IP，请检查网页开关。";
        }
        catch (Exception ex) { AppDialog.Notify(this, SafeError(ex), "浏览器核对"); }
        finally { BrowserButton.IsEnabled = session.Connected; }
    }
    private void BrowseProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 Claude Code 项目文件夹", InitialDirectory = Directory.Exists(ProjectBox.Text) ? ProjectBox.Text : "" };
        if (dialog.ShowDialog(this) == true) ProjectBox.Text = dialog.FolderName;
    }
    private void BrowseClaude_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Claude Code|claude.exe;claude.cmd|可执行程序|*.exe;*.cmd;*.bat" };
        if (dialog.ShowDialog(this) == true) ClaudePathBox.Text = dialog.FileName;
    }
    private void BrowseDesktop_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择官方 Claude 桌面客户端（不是 CLI）", Filter = "Claude 桌面客户端|Claude.exe|可执行程序|*.exe" };
        if (dialog.ShowDialog(this) == true) DesktopPathBox.Text = dialog.FileName;
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try { SaveUISettings(); await RefreshDesktopAsync(); Log("设置已保存。", LogKind.Success); StatusText.Text = session.Connected ? "设置已保存；连接参数在下次连接时生效" : "设置已保存"; UpdateState(); }
        catch (Exception ex) { AppDialog.Notify(this, SafeError(ex), "设置未保存"); }
    }
    private void Import_Click(object sender, RoutedEventArgs e) => ShowImport();
    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try { ShowImport(System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : ""); }
        catch (ExternalException) { AppDialog.Notify(this, "剪贴板暂时不可用，请重试。"); }
    }
    private void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "节点配置|*.txt;*.yaml;*.yml;*.json|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > NodeImporter.MaxBytes) throw new IOException("文件超过 8 MB。");
            ShowImport(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) { AppDialog.Notify(this, SafeError(ex)); }
    }
    private void ShowImport(string text = "")
    {
        var dialog = new NodeDialog(text) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        var known = settings.Nodes.Where(x => x.GroupId == "manual").Select(x => x.Fingerprint).ToHashSet();
        var added = dialog.Result.Nodes.Where(x => known.Add(x.Fingerprint)).ToList();
        settings.Nodes.AddRange(added);
        settings.SelectedNodeId ??= added.FirstOrDefault()?.Id;
        store.Save(settings); RefreshLists(); Log($"节点导入完成：新增 {added.Count} 个，跳过 {dialog.Result.Warnings.Count} 个不支持的条目。");
        StatusText.Text = $"已添加 {added.Count} 个节点";
        if (dialog.Result.Warnings.Count > 0) AppDialog.Notify(this, string.Join("\n", dialog.Result.Warnings.Take(8)), "部分条目未导入");
    }
    private void EditNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodesGrid.SelectedItem is not NodeRow row) { StatusText.Text = "请先选中一个节点"; return; }
        if (session.Connected && row.Node.Id == session.NodeId) { AppDialog.Notify(this, "请先断开当前节点，再编辑。"); return; }
        var dialog = new NodeDialog(existing: row.Node) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result?.Nodes.Count != 1) return;
        var replacement = dialog.Result.Nodes[0]; replacement.Id = row.Node.Id; replacement.GroupId = row.Node.GroupId;
        settings.Nodes[settings.Nodes.IndexOf(row.Node)] = replacement;
        store.Save(settings); RefreshLists(); Log("节点配置已更新。", LogKind.Success);
    }
    private void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodesGrid.SelectedItem is not NodeRow row) return;
        if (session.Connected && row.Node.Id == session.NodeId) { AppDialog.Notify(this, "请先断开当前节点。"); return; }
        if (!AppDialog.Confirm(this, "删除节点", "删除选中的节点？订阅节点可能在下次更新时重新出现。", "删除节点")) return;
        settings.Nodes.Remove(row.Node); store.Save(settings); RefreshLists(); Log("节点已删除。");
    }
    private void SelectNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodesGrid.SelectedItem is not NodeRow row) return;
        if (session.Connected || ClaudeRunning) { StatusText.Text = "请先退出当前会话并断开，再切换节点"; return; }
        settings.SelectedNodeId = row.Node.Id; RefreshLists(); store.Save(settings); Navigate("Dashboard"); StatusText.Text = "已选择节点";
    }
    private async void AddSubscription_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SubscriptionDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var subscription = new Subscription { Name = dialog.SubscriptionName, Url = dialog.Url };
        settings.Subscriptions.Add(subscription); store.Save(settings); RefreshLists();
        await RunBusyAsync(() => UpdateSubscriptionAsync(subscription), "正在下载并解析订阅…");
    }
    private async Task UpdateSubscriptionAsync(Subscription subscription)
    {
        if (session.Connected && settings.Nodes.Any(x => x.Id == session.NodeId && x.GroupId == subscription.Id))
            throw new IOException("此订阅的节点正在使用，请先断开，再更新该订阅。");
        var (parsed, usage) = await NetworkServices.DownloadSubscriptionAsync(subscription.Url, session.Connected ? session.Port : null);
        SubscriptionMerge.Replace(settings, subscription, parsed); subscription.Usage = usage;
        store.Save(settings); RefreshLists();
        Log($"订阅更新完成：{parsed.Nodes.Count} 个节点，{parsed.Warnings.Count} 个条目未支持。");
        StatusText.Text = $"订阅已更新：{parsed.Nodes.Count} 个节点";
        if (parsed.Warnings.Count > 0) AppDialog.Notify(this, string.Join("\n", parsed.Warnings.Take(8)), "部分条目未支持");
    }
    private async void UpdateSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsGrid.SelectedItem is SubscriptionRow row) await RunBusyAsync(() => UpdateSubscriptionAsync(row.Subscription), "正在更新订阅…");
        else StatusText.Text = "请先选中一个订阅";
    }
    private async void UpdateAllSubscriptions_Click(object sender, RoutedEventArgs e) => await RunBusyAsync(async () =>
    {
        var failed = 0;
        foreach (var subscription in settings.Subscriptions.ToList())
        {
            try { await UpdateSubscriptionAsync(subscription); }
            catch (Exception ex) { failed++; Log(SafeError(ex), LogKind.Error); }
        }
        StatusText.Text = failed == 0 ? "全部订阅更新完成" : $"{failed} 个订阅更新失败，已保留旧节点；请查看运行记录";
    }, "正在逐个更新订阅…");
    private void EditSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsGrid.SelectedItem is not SubscriptionRow row) return;
        var dialog = new SubscriptionDialog(row.Subscription) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        row.Subscription.Name = dialog.SubscriptionName; row.Subscription.Url = dialog.Url;
        store.Save(settings); RefreshLists(); Log("订阅设置已保存。", LogKind.Success);
    }
    private void DeleteSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsGrid.SelectedItem is not SubscriptionRow row) return;
        if (session.Connected && settings.Nodes.Any(x => x.GroupId == row.Subscription.Id && x.Id == session.NodeId))
        { AppDialog.Notify(this, "此订阅的节点正在使用，请先断开。"); return; }
        if (!AppDialog.Confirm(this, "删除订阅", "删除此订阅及其节点？", "删除订阅")) return;
        settings.Nodes.RemoveAll(x => x.GroupId == row.Subscription.Id); settings.Subscriptions.Remove(row.Subscription);
        store.Save(settings); RefreshLists(); Log("订阅及其节点已删除。");
    }
    private async void Restore_Click(object sender, RoutedEventArgs e) => await RunBusyAsync(async () =>
    {
        if (ClaudeRunning) throw new IOException("Claude 仍在运行，请先在终端正常退出，再恢复环境。");
        await session.StopAsync(allowPendingRecovery: true);
        foreach (var id in SessionController.PendingRecovery())
        {
            using var process = ProcessTools.StartSelf("--recover", id, true);
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new IOException("部分设置尚未确认恢复，请检查网卡或时区后重试。");
        }
        desktopOpened = false; report = null; CheckList.ItemsSource = null; Log("临时设置恢复已完成。", LogKind.Success); StatusText.Text = "恢复已完成";
    }, "正在恢复临时设置…");
    private void OpenData_Click(object sender, RoutedEventArgs e) { Directory.CreateDirectory(AppPaths.Root); Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "\"" + AppPaths.Root + "\"", UseShellExecute = true }); }
    private void InstallHelp_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(DesktopMode ? "https://claude.com/download" : "https://code.claude.com/docs/en/setup") { UseShellExecute = true });
    private void ClearLogs_Click(object sender, RoutedEventArgs e) { logLines.Clear(); LogBox.Document.Blocks.Clear(); }
    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); tray.Visible = false; }
    private async Task ExitFromTrayAsync()
    {
        ShowFromTray();
        if (ClaudeRunning) { AppDialog.Notify(this, "Claude 终端仍在运行，请先正常退出，以便保存工作并恢复环境。"); return; }
        await RunBusyAsync(async () => { await session.StopAsync(); allowClose = true; }, "正在退出并恢复…");
        if (allowClose) Close();
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (allowClose || smoke) return;
        if (busy || session.Connected || ClaudeRunning)
        {
            e.Cancel = true; tray.Visible = true; Hide();
            tray.ShowBalloonTip(3500, "Claude Link 仍在维护当前会话", DesktopMode ? "桌面客户端使用完后，请从托盘退出并恢复环境。" : "从托盘重新打开；Claude CLI 退出后自动恢复。", Forms.ToolTipIcon.Info);
            return;
        }
        try { SaveUISettings(); } catch { }
    }
    public void CloseForSmoke() { allowClose = true; Close(); }
    public void FillSmokeReport()
    {
        CheckList.ItemsSource = new List<CheckResult> {
            new() { Label = "普通出口", Value = "198.51.100.24", Detail = "界面示例数据，不代表真实网络检测结果", State = "pass" },
            new() { Label = "Anthropic API", Value = "HTTP 401 · HTTPS 可达", Detail = "未提供认证，账户授权仍未验证", State = "pass" },
            new() { Label = "Claude 网页", Value = "HTTP 403", Detail = "示例：网页请求被拒绝，与 API 连通性分别显示", State = "warn" },
            new() { Label = "连接失败示例", Value = "DNS 解析失败", Detail = "示例：请更换节点或检查服务器域名", State = "fail" },
            new() { Label = "系统时区", Value = "尚未检测", Detail = "无可靠检测结果时保持未验证", State = "pending" }
        };
        CheckSummary.Text = "状态示例：绿色通过、橙色注意、红色失败、灰色待检测。下方均为界面验证数据。";
        Log("界面示例：启动器已准备好。", LogKind.Info);
        Log("界面示例：代理出口与 API HTTPS 检测完成。", LogKind.Success);
        Log("界面示例：网页返回 HTTP 403，请核对检查项。", LogKind.Warning);
        Log("界面示例：节点连接失败，请检查服务器配置。", LogKind.Error);
    }
    public sealed record NodeRow(ProxyNode Node, string Group)
    {
        public string Name => Node.Name; public string Protocol => Node.Protocol.ToUpperInvariant(); public string Server => Node.Server;
    }
    public sealed record BrowserChoice(string Id, string Label);
    public sealed record SubscriptionRow(Subscription Subscription, int Count)
    {
        public string Name => Subscription.Name;
        public string Updated => Subscription.UpdatedAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "尚未更新";
        public string Usage => Subscription.Usage ?? "未提供";
    }
}
