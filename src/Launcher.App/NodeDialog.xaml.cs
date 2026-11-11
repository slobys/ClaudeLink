using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Launcher.App;

public partial class NodeDialog : Window
{
    public ImportResult? Result { get; private set; }
    public NodeDialog(string initialText = "", ProxyNode? existing = null)
    {
        InitializeComponent();
        ProtocolCombo.ItemsSource = new[] { "vless", "vmess", "trojan", "shadowsocks", "hysteria2", "tuic", "socks", "http", "anytls" };
        SecurityCombo.ItemsSource = new[] { "none", "tls", "reality" };
        TransportCombo.ItemsSource = new[] { "tcp", "ws", "grpc", "httpupgrade" };
        SecurityCombo.SelectedIndex = 0; TransportCombo.SelectedIndex = 0; ProtocolCombo.SelectedIndex = 0;
        ImportBox.Text = initialText;
        if (existing is not null)
        {
            Title = "编辑节点"; SaveButton.Content = "保存节点";
            var data = JsonNode.Parse(existing.OutboundJson)!.AsObject(); data["tag"] = existing.Name;
            ImportBox.Text = data.ToJsonString(JsonDefaults.Pretty);
        }
    }
    private void Protocol_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SecurityCombo is null || CipherBox is null) return;
        var protocol = ProtocolCombo.SelectedItem as string;
        SecurityCombo.SelectedItem = protocol is "trojan" or "hysteria2" or "tuic" or "anytls" ? "tls" : "none";
        CipherBox.Text = protocol == "vmess" ? "auto" : "aes-128-gcm";
    }
    private void BrowseImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "节点配置|*.txt;*.yaml;*.yml;*.json|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        if (new FileInfo(dialog.FileName).Length > NodeImporter.MaxBytes) { ErrorText.Text = "文件超过 8 MB。"; return; }
        ImportBox.Text = File.ReadAllText(dialog.FileName);
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = ModeTabs.SelectedIndex == 0 ? ImportBox.Text : ManualJson();
            Result = NodeImporter.Parse(text);
            if (Result.Nodes.Count == 0)
            {
                ErrorText.Text = Result.Warnings.Count > 0 ? string.Join("\n", Result.Warnings.Take(3)) : "没有找到可用节点。";
                return;
            }
            DialogResult = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { ErrorText.Text = ex is FormatException && ex.Message.Length < 160 && !ex.Message.Contains("://") ? ex.Message : "无法解析内容，请检查协议和字段格式。"; }
    }
    private string ManualJson()
    {
        if (!int.TryParse(ServerPortBox.Text, out var port) || port is < 1 or > 65535) throw new FormatException("端口应为 1—65535。");
        var type = (string)ProtocolCombo.SelectedItem;
        var o = new JsonObject { ["type"] = type, ["server"] = ServerBox.Text.Trim(), ["server_port"] = port };
        if (type is "vless" or "vmess" or "tuic") o["uuid"] = UserBox.Text.Trim();
        if (type is "trojan" or "shadowsocks" or "hysteria2" or "tuic" or "anytls") o["password"] = PasswordInput.Password;
        if (type is "http" or "socks")
        {
            o["username"] = UserBox.Text; o["password"] = PasswordInput.Password;
            if (type == "socks") o["version"] = "5";
        }
        if (type == "vmess") { o["security"] = CipherBox.Text.Trim(); o["alter_id"] = 0; }
        if (type == "shadowsocks") o["method"] = CipherBox.Text.Trim();
        if (type == "vless" && FlowBox.Text.Trim().Length > 0) o["flow"] = FlowBox.Text.Trim();
        var security = (string)SecurityCombo.SelectedItem;
        if (security != "none")
        {
            var tls = new JsonObject { ["enabled"] = true, ["insecure"] = InsecureCheck.IsChecked == true };
            if (SniBox.Text.Trim().Length > 0) tls["server_name"] = SniBox.Text.Trim();
            if (security == "reality")
            {
                if (PublicKeyBox.Text.Trim().Length == 0) throw new FormatException("Reality 需要填写公钥。");
                tls["utls"] = new JsonObject { ["enabled"] = true, ["fingerprint"] = "chrome" };
                tls["reality"] = new JsonObject { ["enabled"] = true, ["public_key"] = PublicKeyBox.Text.Trim(), ["short_id"] = ShortIdBox.Text.Trim() };
            }
            o["tls"] = tls;
        }
        var transport = (string)TransportCombo.SelectedItem;
        if (transport != "tcp" && type is "vless" or "vmess" or "trojan")
        {
            var t = new JsonObject { ["type"] = transport };
            if (transport == "grpc") t["service_name"] = PathBox.Text.Trim('/');
            else
            {
                t["path"] = PathBox.Text.Length > 0 ? PathBox.Text : "/";
                if (HostBox.Text.Trim().Length > 0)
                {
                    if (transport == "ws") t["headers"] = new JsonObject { ["Host"] = HostBox.Text.Trim() };
                    else t["host"] = HostBox.Text.Trim();
                }
            }
            o["transport"] = t;
        }
        o["tag"] = NameBox.Text.Trim();
        return o.ToJsonString();
    }
}
