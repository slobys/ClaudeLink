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
        var result = NodeImporter.Parse("vless://11111111-1111-4111-8111-111111111111@node.example.com:443?security=tls&sni=node.example.com#示例节点（未连接）");
        var settings = new LauncherSettings { Nodes = result.Nodes, ProjectDirectory = @"C:\Projects\my-project" };
        settings.Subscriptions.Add(new Subscription { Name = "示例订阅", Url = "https://example.com/subscription" });
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
    private static void SubmitAppDialog(AppDialog dialog, Action submit)
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
            context.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight));
        }
        if (content.Margin == new Thickness(0)) bitmap.Render(content);
        else bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
