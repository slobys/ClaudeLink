using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using Launcher.App.Services;

namespace Launcher.App;

public static class UiSmoke
{
    public static LauncherSettings DemoSettings()
    {
        var settings = new LauncherSettings { ProjectDirectory = @"C:\Projects\my-project" };
        var subscription=new Subscription { Name = "示例订阅", Url = "https://example.com/subscription", UpdatedAt=DateTimeOffset.UtcNow, Usage="已用 162.8 / 400.0 GB" };settings.Subscriptions.Add(subscription);
        settings.Subscriptions.Add(new() {Name="余量较低 · 示例", Url="https://example.com/low",Traffic=SubscriptionTraffic.Parse("upload=0; download=96636764160; total=107374182400; expire=1900000000")});
        settings.Subscriptions.Add(new() {Name="未提供流量 · 示例", Url="https://example.com/unknown"});
        settings.Subscriptions.Add(new() {Name="已过期 · 示例", Url="https://example.com/expired",Traffic=SubscriptionTraffic.Parse("upload=0; download=10737418240; total=107374182400; expire=1600000000")});
        foreach(var name in new[]{"香港 01 · 示例","香港 02 超长名称用于核对省略显示 · 示例","日本 03 · 示例","新加坡 04 · 示例","台湾 05 · 示例","英国 06 · 示例","德国 07 · 示例","美国住宅出口 · 示例","自定义节点 · 示例"})
        {
            var node=NodeImporter.Parse("socks5://demo:example@node.example.com:1080#"+Uri.EscapeDataString(name)).Nodes.Single();
            node.GroupId=subscription.Id;settings.Nodes.Add(node);
        }
        var chain=ProxyChain.Create("美国出口链路 · 示例",settings.Nodes[0].Id,settings.Nodes[7].Id,settings.Nodes);
        settings.Nodes.Insert(0,chain);settings.SelectedNodeId=chain.Id;
        return settings;
    }
    public static async Task CaptureAsync(MainWindow window, string directory)
    {
        Directory.CreateDirectory(directory);
        var assertions = new List<string>();
        if (new[] { "TunCheck", "StrictCheck", "IPv6Check", "TimeZoneCheck" }.Any(name => ((CheckBox)window.FindName(name)).IsChecked != true)
            || ((ComboBox)window.FindName("LanguageCombo")).SelectedItem?.ToString() != "en-US" || ((ComboBox)window.FindName("BrowserCombo")).SelectedValue?.ToString() != "auto")
            throw new InvalidOperationException("Fresh setup preset did not bind to the visible controls.");
        assertions.Add("Fresh install preset is selected in all network, language and browser controls");
        window.Navigate("Nodes");
        var cards=(ListBox)window.FindName("NodesGrid");
        var search=(TextBox)window.FindName("NodeSearchBox");
        var region=(ComboBox)window.FindName("NodeRegionFilter");
        var initialRows=cards.Items.OfType<MainWindow.NodeRow>().ToList();
        if(initialRows.Count < 10 || initialRows.First(x=>x.Current).RegionLabel != "美国" || initialRows.Where(x=>x.Flag != null).Any(x=>x.Flag!.PixelWidth != 72)) throw new InvalidOperationException("Region card data or bundled flags did not render.");
        search.Text="日本";
        if(cards.Items.Count != 1) throw new InvalidOperationException("Node search did not filter by region.");
        var japanese=(MainWindow.NodeRow)cards.Items[0];cards.SelectedItem=japanese;
        if(((ComboBox)window.FindName("NodeCombo")).SelectedItem is not ProxyNode selected || selected.Id != japanese.Node.Id) throw new InvalidOperationException("Single card selection did not reach the workbench.");
        search.Text="";region.SelectedValue="US";
        if(cards.Items.OfType<MainWindow.NodeRow>().Any(x=>x.RegionLabel != "美国") || cards.Items.Count < 2) throw new InvalidOperationException("Exit region filter did not include its chain.");
        search.Text="不存在的示例";
        Capture(window,Path.Combine(directory,"nodes-empty.png"));
        if(cards.Items.Count != 0 || ((TextBlock)window.FindName("NodesEmpty")).Visibility != Visibility.Visible) throw new InvalidOperationException("No-result state missing.");
        search.Text="";region.SelectedValue="all";
        cards.SelectedItem=cards.Items.OfType<MainWindow.NodeRow>().Single(x=>x.Node.IsChain);
        if(((TextBlock)window.FindName("ActiveChainText")).Text.Count(x=>x=='→') != 3) throw new InvalidOperationException("Workbench chain direction missing.");
        assertions.Add("Node cards show region flags, filter regions and chains, select in one click and preserve empty-state feedback");
        window.FillLatencyPreview();
        var sorter=(ComboBox)window.FindName("NodeSort");sorter.SelectedIndex=1;
        var latencyRows=cards.Items.OfType<MainWindow.NodeRow>().ToList();
        if(!latencyRows.Where(x=>x.Latency?.HasDelay==true).Select(x=>x.Latency!.Milliseconds).SequenceEqual(latencyRows.Where(x=>x.Latency?.HasDelay==true).Select(x=>x.Latency!.Milliseconds).OrderBy(x=>x)) || latencyRows.Count(x=>x.Current)!=1)throw new InvalidOperationException("Latency sorting or selection preservation failed.");
        Capture(window,Path.Combine(directory,"nodes-latency.png"));sorter.SelectedIndex=2;
        var descending=cards.Items.OfType<MainWindow.NodeRow>().ToList();
        if(descending.First().Latency?.HasDelay!=true || descending.Last().Latency?.HasDelay==true)throw new InvalidOperationException("Unmeasured nodes must stay last in both sort directions.");
        assertions.Add("Latency states fit compact cards, measured nodes sort in both directions, and selection stays unchanged");
        sorter.SelectedIndex=0;
        var stableSource=cards.ItemsSource;
        var stableRow=cards.Items.OfType<MainWindow.NodeRow>().First(x=>!x.Current);
        var chosenId=cards.Items.OfType<MainWindow.NodeRow>().Single(x=>x.Current).Node.Id;
        window.PreviewIncrementalLatency(stableRow.Node.Id,51);
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(!ReferenceEquals(stableSource,cards.ItemsSource) || !ReferenceEquals(stableRow,cards.Items.OfType<MainWindow.NodeRow>().Single(x=>x.Node.Id==stableRow.Node.Id)) || stableRow.State!="51 ms" || cards.Items.OfType<MainWindow.NodeRow>().Single(x=>x.Current).Node.Id!=chosenId)throw new InvalidOperationException("Incremental latency updates rebuilt the grid or changed selection.");
        assertions.Add("Streaming latency updates keep the collection, row objects and selected node intact");
        window.PreviewNodeTest(true);
        if(((Button)window.FindName("ConnectButton")).IsEnabled || ((WrapPanel)window.FindName("NodeActions")).IsEnabled || !((Button)window.FindName("NodeTestButton")).IsEnabled)throw new InvalidOperationException("Latency progress must block connect and mutations while keeping cancel enabled.");
        Capture(window,Path.Combine(directory,"nodes-latency-running.png"));
        ((Button)window.FindName("NodeTestButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(!window.PreviewNodeTest(false))throw new InvalidOperationException("Latency cancel button did not cancel the active batch.");
        assertions.Add("Latency progress keeps cancellation reachable and prevents concurrent connect or node edits");
        var sources=cards.Items.OfType<MainWindow.NodeRow>().Select(x=>x.Node).ToList();
        var chainDialog=new ChainDialog(sources) {Owner=window};
        SubmitAppDialog(chainDialog,()=>{
            var entry=(ComboBox)chainDialog.FindName("EntryCombo");var exit=(ComboBox)chainDialog.FindName("ExitCombo");entry.SelectedIndex=0;
            var exitId=sources.First(x=>!x.IsChain && NodeRegion.FromName(x.Name)?.Code == "US").Id;
            exit.SelectedValue=exitId;Capture(chainDialog,Path.Combine(directory,"chain-editor.png"));
            exit.SelectedValue=((ProxyNode)entry.SelectedItem).Id;
            ((Button)chainDialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(string.IsNullOrEmpty(((TextBlock)chainDialog.FindName("ErrorText")).Text))throw new InvalidOperationException("Duplicate hops were accepted.");
            Capture(chainDialog,Path.Combine(directory,"chain-editor-error.png"));
            exit.SelectedValue=exitId;
            ((Button)chainDialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        if(chainDialog.Result?.ChainNodeIds.Count != 2)throw new InvalidOperationException("Valid chain form did not save.");
        assertions.Add("Chain editor rejects identical hops inline and saves explicit entry-to-exit direction");
        var launchReport = new NetworkReport { GeneralIP = "198.51.100.24", CloudflareIP = "198.51.100.24", ClaudeIP = "198.51.100.24", ApiCode = 401, ApiReachable = true, WebCode = 403, WebReachable = false, SameIPv4 = true, Country = "US", Tun = true, IPv6Disabled = true, TimeZoneMatches = true };
        foreach (var proceed in new[] { false, true })
        {
            var confirmation = AppDialog.Launch(launchReport, true); confirmation.Owner = window;
            SubmitAppDialog(confirmation, () =>
            {
                if (!((Button)confirmation.FindName("CancelButton")).IsDefault || ((Button)confirmation.FindName("ConfirmButton")).IsDefault)
                    throw new InvalidOperationException("Launch confirmation must default to returning to checks.");
                if (!proceed) Capture(confirmation, Path.Combine(directory, "launch-confirmation.png"));
                ((Button)confirmation.FindName(proceed ? "ConfirmButton" : "CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            if (confirmation.DialogResult != proceed || launchReport.StrictReady) throw new InvalidOperationException("Launch decision changed report state or ignored cancellation.");
        }
        assertions.Add("Themed launch dialog defaults to cancel, handles both decisions and preserves failed checks");
        var notice = new AppDialog("操作未完成", "示例：连接未完成，请检查节点设置后重试。", tone: "Error") { Owner = window };
        SubmitAppDialog(notice, () => { Capture(notice, Path.Combine(directory, "notice-error.png")); ((Button)notice.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
        var longDialog = AppDialog.Launch(launchReport, true); longDialog.Owner = window;
        ((ItemsControl)longDialog.FindName("Reasons")).ItemsSource = Enumerable.Range(1, 18).Select(i => $"检查示例 {i}：此项目尚未验证，请返回结果页面核对网络、地区与环境设置。");
        SubmitAppDialog(longDialog, () => { Capture(longDialog, Path.Combine(directory, "launch-confirmation-long.png")); ((Button)longDialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
        assertions.Add("Themed notice and long confirmation keep readable content and fixed action buttons");
        var gallery = new Window { Title = "Brand scale fixtures", Width = 660, SizeToContent = SizeToContent.Height, Owner = window, Background = (Brush)window.FindResource("Surface") };
        var icons = new WrapPanel { Margin = new Thickness(24) };
        foreach (var size in new[] { 16, 24, 32, 42, 64, 128 }) icons.Children.Add(new Image { Source = BrandAssets.Image, Width = size, Height = size, Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Center });
        var gallerySurface = new Grid { Background = (Brush)window.FindResource("Surface") }; gallerySurface.Children.Add(icons);
        gallery.Content = gallerySurface; gallery.Show(); gallery.UpdateLayout(); Capture(gallery, Path.Combine(directory, "brand-scales.png")); gallery.Close();
        var vector = new RenderTargetBitmap(42, 42, 96, 96, PixelFormats.Pbgra32); var mark = new DrawingVisual();
        using (var drawing = mark.RenderOpen()) drawing.DrawImage(BrandAssets.Image, new Rect(0, 0, 42, 42)); vector.Render(mark);
        var pixels = new byte[42 * 42 * 4]; vector.CopyPixels(pixels, 42 * 4, 0);
        if (!Enumerable.Range(0, 42 * 42).Any(i => pixels[i * 4 + 3] is > 0 and < 255)) throw new InvalidOperationException("Logo edge is not anti-aliased.");
        assertions.Add("Vector brand renders all UI sizes with anti-aliased transparent edges");
        var imported = new NodeDialog("trojan://test-only-placeholder@127.0.0.1:9443?sni=example.com#界面导入测试") { Owner = window };
        SubmitDialog(imported, () => ((Button)imported.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        if (imported.Result?.Nodes.Single().Name != "界面导入测试") throw new InvalidOperationException("Node import dialog failed.");
        assertions.Add("Node share-link dialog submits through real WPF click handler");
        var manual = new NodeDialog { Owner = window };
        SubmitDialog(manual, () =>
        {
            ((TabControl)manual.FindName("ModeTabs")).SelectedIndex = 1;
            ((ComboBox)manual.FindName("ProtocolCombo")).SelectedItem = "trojan";
            ((TextBox)manual.FindName("NameBox")).Text = "手动表单测试";
            ((TextBox)manual.FindName("ServerBox")).Text = "example.com";
            ((PasswordBox)manual.FindName("PasswordInput")).Password = "test-only-placeholder";
            Capture(manual, Path.Combine(directory, "manual-node.png"));
            ((Button)manual.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        if (manual.Result?.Nodes.Single().Protocol != "trojan") throw new InvalidOperationException("Manual node form failed.");
        assertions.Add("Manual protocol/credential form builds a validated node");
        var invalid = new NodeDialog("ssr://unsupported-test") { Owner = window };
        SubmitDialog(invalid, () =>
        {
            ((Button)invalid.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (((TextBlock)invalid.FindName("ErrorText")).Text.Length == 0) throw new InvalidOperationException("Validation feedback missing.");
            invalid.Close();
        });
        assertions.Add("Unsupported node keeps dialog open with inline validation");
        ((ComboBox)window.FindName("LaunchTargetCombo")).SelectedValue = "cli";
        if (!((Button)window.FindName("LaunchButton")).Content.ToString()!.Contains("CLI") || !((CheckBox)window.FindName("TunCheck")).IsEnabled)
            throw new InvalidOperationException("CLI selection did not update launch behavior.");
        assertions.Add("CLI choice labels terminal mode and permits optional TUN");
        window.Navigate("Settings");
        Capture(window, Path.Combine(directory, "settings-cli.png"));
        ((ComboBox)window.FindName("LaunchTargetCombo")).SelectedValue = "desktop";
        if (((CheckBox)window.FindName("TunCheck")).IsEnabled || ((CheckBox)window.FindName("TunCheck")).IsChecked != true)
            throw new InvalidOperationException("Desktop mode must use TUN.");
        assertions.Add("Desktop choice selects required TUN and opens the graphical client mode");
        foreach (var previous in new[] { "Success", "Error" })
        {
            var api = (TextBlock)window.FindName("ApiMetric");
            var exit = (TextBlock)window.FindName("ExitMetric");
            api.Foreground = exit.Foreground = (Brush)window.FindResource(previous);
            var reset = previous == "Success" ? (Button)window.FindName("DisconnectButton")
                : Descendants<Button>(window).First(x => x.Content?.ToString() == "恢复临时设置");
            reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(150);
            var pending = ((Brush)window.FindResource("Pending")).ToString();
            if (api.Text != "待检测" || api.Foreground.ToString() != pending || exit.Foreground.ToString() != pending)
                throw new InvalidOperationException("Disconnect/recovery left stale metric colors.");
        }
        assertions.Add("Disconnect after success and recovery after failure both reset pending metric colors");
        window.FillSmokeReport();
        var log = (RichTextBox)window.FindName("LogBox");
        if (log.FontSize < 16 || log.VerticalContentAlignment != VerticalAlignment.Top || log.Document.Blocks.Count < 4)
            throw new InvalidOperationException("Structured logs must stay readable and top aligned.");
        var logColors = log.Document.Blocks.Cast<Paragraph>().SelectMany(p => p.Inlines.OfType<Run>()).Select(r => r.Foreground.ToString()).Distinct().ToArray();
        if (logColors.Length < 4) throw new InvalidOperationException("Log severity colors were not applied.");
        assertions.Add("Logs render from the top with 16px text and distinct severity colors");
        foreach (var page in new[] { "Dashboard", "Nodes", "Subscriptions", "Settings", "Logs" })
        {
            window.Navigate(page);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(150);
            Capture(window, Path.Combine(directory, page.ToLowerInvariant() + ".png"));
        }
        window.Navigate("Subscriptions");
        var subscriptions=(ListBox)window.FindName("SubscriptionsGrid");
        var subscriptionRows=subscriptions.Items.OfType<MainWindow.SubscriptionRow>().ToArray();
        if(subscriptionRows.Length!=4 || Math.Abs(subscriptionRows[0].Traffic.Percent-40.7)>0.01 || subscriptionRows[1].Traffic.Tone!="Warning" || subscriptionRows[2].Traffic.HasQuota || subscriptionRows[3].Traffic.Tone!="Error") throw new InvalidOperationException("Subscription quota states did not bind.");
        subscriptions.SelectedIndex=1; subscriptions.UpdateLayout();
        var subscriptionItem=(ListBoxItem)subscriptions.ItemContainerGenerator.ContainerFromIndex(1);subscriptionItem.Focus();
        Capture(window,Path.Combine(directory,"subscriptions-selected.png"));
        window.RefreshSubscriptions();
        if(((MainWindow.SubscriptionRow)subscriptions.SelectedItem).Subscription.Id!=subscriptionRows[1].Subscription.Id)throw new InvalidOperationException("Subscription refresh lost selection.");
        subscriptions.ScrollIntoView(subscriptions.Items[3]);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Capture(window,Path.Combine(directory,"subscriptions-states.png"));
        var originalItems=subscriptions.ItemsSource;subscriptions.ItemsSource=Array.Empty<MainWindow.SubscriptionRow>();((TextBlock)window.FindName("SubscriptionsEmpty")).Visibility=Visibility.Visible;
        Capture(window,Path.Combine(directory,"subscriptions-empty.png"));subscriptions.ItemsSource=originalItems;((TextBlock)window.FindName("SubscriptionsEmpty")).Visibility=Visibility.Collapsed;
        assertions.Add("Subscription cards expose usage, remaining and expiry, distinguish low quota, unknown and expired, and preserve selection after refresh");
        window.Width=1000;window.Height=740;subscriptions.ScrollIntoView(subscriptions.Items[0]);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(window,Path.Combine(directory,"subscriptions-small.png"));window.Width=1280;window.Height=850;
        var fullWidth=window.Width;
        window.Width=1140; window.Navigate("Nodes");
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Capture(window,Path.Combine(directory,"nodes-user-width.png"));
        window.Width=fullWidth;
        var dialog = new NodeDialog { Owner = window };
        window.Navigate("Dashboard");
        ((ScrollViewer)window.FindName("DashboardPage")).ScrollToBottom();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(directory, "checks.png"));
        var rows = Descendants<Border>((ItemsControl)window.FindName("CheckList")).Where(x => x.Name == "ResultRow").ToArray();
        if (rows.Length < 4 || rows.Select(x => x.Background.ToString()).Distinct().Count() < 4)
            throw new InvalidOperationException("Check result colors did not bind to their semantic states.");
        assertions.Add("Native result templates bind separate success, warning, error and pending backgrounds");
        ((ScrollViewer)window.FindName("DashboardPage")).ScrollToTop();
        window.Navigate("Settings");
        ((ScrollViewer)window.FindName("SettingsPage")).ScrollToBottom();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(directory, "settings-presets.png"));
        ((ScrollViewer)window.FindName("SettingsPage")).ScrollToTop(); window.Navigate("Dashboard");
        dialog.Show();
        await dialog.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(dialog, Path.Combine(directory, "add-node.png"));
        dialog.Close();
        window.Navigate("Dashboard"); window.FillSmokeUpdate();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(directory, "update-notice.png"));
        window.Width = 1000; window.Height = 740; window.Navigate("Dashboard");
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(directory, "dashboard-small.png"));
        window.Navigate("Nodes"); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Capture(window, Path.Combine(directory, "nodes-small.png"));
        AppPaths.WriteJson(Path.Combine(directory, "ui-results.json"), new { Passed = assertions.Count, Checks = assertions });
        window.CloseForSmoke();
        // This exact GUID directory was created solely for this smoke test.
        var testRoot = Path.GetFullPath(AppPaths.Root);
        if (Guid.TryParseExact(Path.GetFileName(testRoot), "N", out _) && Path.GetFileName(Path.GetDirectoryName(testRoot)) == "ui-tests" && Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }
    private static void SubmitDialog(NodeDialog dialog, Action submit)
    {
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { submit(); if (dialog.IsVisible) { failure = new InvalidOperationException("Dialog did not submit."); dialog.Close(); } }
            catch (Exception ex) { failure = ex; dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        dialog.ShowDialog();
        if (failure is not null) throw failure;
    }
    private static void SubmitAppDialog(Window dialog, Action submit)
    {
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { submit(); }
            catch (Exception ex) { failure = ex; dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        dialog.ShowDialog(); if (failure is not null) throw failure;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
        var height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        // A root content Margin is a layout offset; rendering the Visual directly
        // translates it into the bitmap and clips the opposite edges of dialogs.
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, width + 1, height + 1));
            context.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill, ViewboxUnits=BrushMappingMode.Absolute, Viewbox=new Rect(0,0,content.ActualWidth,content.ActualHeight) }, null, new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight));
        }
        if(window is MainWindow) bitmap.Render(content);
        else bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
