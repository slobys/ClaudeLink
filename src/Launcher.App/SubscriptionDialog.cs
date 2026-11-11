using System.Windows;
using System.Windows.Controls;

namespace Launcher.App;

public sealed class SubscriptionDialog : Window
{
    private readonly TextBox nameInput = new();
    private readonly TextBox urlInput = new();
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Firebrick };
    public string SubscriptionName => nameInput.Text.Trim();
    public string Url => urlInput.Text.Trim();
    public SubscriptionDialog(Subscription? subscription = null)
    {
        Title = subscription is null ? "添加订阅" : "编辑订阅";
        Style = (Style)FindResource(typeof(Window));
        Width = 600; Height = 380; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var grid = new Grid { Margin = new Thickness(26) };
        grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = "订阅名称", Margin = new Thickness(0, 0, 0, 7) });
        nameInput.Text = subscription?.Name ?? ""; fields.Children.Add(nameInput);
        fields.Children.Add(new TextBlock { Text = "订阅地址", Margin = new Thickness(0, 17, 0, 7) });
        urlInput.Text = subscription?.Url ?? ""; fields.Children.Add(urlInput);
        fields.Children.Add(new TextBlock { Text = "支持链接列表、Base64、Clash YAML、sing-box JSON。\n在本机解析，更新失败会保留旧节点。", FontSize = 12, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 12, 0, 10), TextWrapping = TextWrapping.Wrap });
        fields.Children.Add(error);
        grid.Children.Add(fields);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
        var save = new Button { Content = subscription is null ? "添加并更新" : "保存", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
        save.Click += (_, _) =>
        {
            if (SubscriptionName.Length == 0) { error.Text = "请填写订阅名称。"; return; }
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { error.Text = "请填写有效的 HTTP / HTTPS 订阅地址。"; return; }
            DialogResult = true;
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save);
        Grid.SetRow(buttons, 1); grid.Children.Add(buttons); Content = grid;
    }
}
