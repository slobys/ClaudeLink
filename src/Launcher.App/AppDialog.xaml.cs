using System.Windows;
using System.Windows.Media;
using Launcher.App.Services;

namespace Launcher.App;

public partial class AppDialog : Window
{
    public AppDialog(string title, string message, string confirm = "确定", string? cancel = null, string tone = "Info")
    {
        InitializeComponent(); Heading.Text = title; Title = title;
        Message.Text = message; ConfirmButton.Content = confirm;
        if (cancel is null) { CancelButton.Visibility = Visibility.Collapsed; ConfirmButton.IsDefault = true; ConfirmButton.IsCancel = true; }
        else CancelButton.Content = cancel;
        ToneTile.Background = (Brush)FindResource(tone + "Bg"); ToneIcon.Stroke = (Brush)FindResource(tone);
        if (tone == "Info") ToneIcon.Data = Geometry.Parse("M10,1 A9,9 0 1 1 9.99,1 M10,8 L10,15 M10,5 L10,5.5");
        else if (tone == "Error") ToneIcon.Data = Geometry.Parse("M10,1 A9,9 0 1 1 9.99,1 M6,6 L14,14 M14,6 L6,14");
        Introduction.Visibility = Visibility.Collapsed; Footnote.Visibility = Visibility.Collapsed;
        Loaded += (_, _) => { if (CancelButton.IsVisible) CancelButton.Focus(); else ConfirmButton.Focus(); };
    }
    public static AppDialog Launch(NetworkReport report, bool strict)
    {
        var dialog = new AppDialog("确认启动 Claude", "以下项目未通过或尚未验证：", "仍要启动", "返回检查", "Warning");
        dialog.Introduction.Text = "部分网络检查需要核对，请确认后再打开应用。"; dialog.Introduction.Visibility = Visibility.Visible;
        dialog.ApiPanel.Visibility = Visibility.Visible;
        dialog.ApiTitle.Text = report.ApiReachable ? $"API HTTPS 可达 · HTTP {report.ApiCode}" : "API 连通性尚未通过";
        dialog.ApiTitle.Foreground = (Brush)dialog.FindResource(report.ApiReachable ? "Success" : "Error");
        dialog.ApiPanel.Background = (Brush)dialog.FindResource(report.ApiReachable ? "SuccessBg" : "ErrorBg");
        dialog.ApiDetail.Text = "此项仅表示网络连通性，尚未验证账户授权。";
        dialog.Reasons.ItemsSource = report.BlockingReasons(strict);
        dialog.Footnote.Text = "继续启动不会改变检测结论；这些检查不判断账号封禁风险。"; dialog.Footnote.Visibility = Visibility.Visible;
        return dialog;
    }
    public static bool ConfirmLaunch(Window owner, NetworkReport report, bool strict) => ShowOwned(Launch(report, strict), owner);
    public static bool Confirm(Window? owner, string title, string message, string confirm = "确认", string cancel = "取消") => ShowOwned(new(title, message, confirm, cancel, "Warning"), owner);
    public static void Notify(Window? owner, string message, string title = "Claude Link", string tone = "Info") => ShowOwned(new(title, message, tone: tone), owner);
    private static bool ShowOwned(AppDialog dialog, Window? owner)
    {
        if (owner?.IsVisible == true) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 40);
        return dialog.ShowDialog() == true;
    }
    private void Confirm_Click(object sender, RoutedEventArgs e) { DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
}
