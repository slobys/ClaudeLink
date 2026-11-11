using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
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
        foreach (var page in new[] { "Dashboard", "Nodes", "Subscriptions", "Settings", "Logs" })
        {
            window.Navigate(page);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(150);
            Capture(window, Path.Combine(directory, page.ToLowerInvariant() + ".png"));
        }
        var dialog = new NodeDialog { Owner = window };
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
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
