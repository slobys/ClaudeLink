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
