using System.Windows;
using System.Windows.Controls;

namespace Launcher.App;

public partial class ChainDialog : Window
{
    private readonly IReadOnlyList<ProxyNode> nodes;
    private readonly ProxyNode? existing;
    public ProxyNode? Result { get; private set; }
    public ChainDialog(IReadOnlyList<ProxyNode> nodes, ProxyNode? existing = null, string? selected = null)
    {
        InitializeComponent(); this.nodes = nodes; this.existing = existing;
        Heading.Text = existing == null ? "创建链式代理" : "编辑链式代理";
        NameBox.Text = existing?.Name ?? "新建双跳链路";
        EntryCombo.ItemsSource = nodes.Where(x => !x.IsChain).ToList();
        ExitCombo.ItemsSource = nodes.Where(ProxyChain.SupportsExit).ToList();
        EntryCombo.SelectedValue = existing?.ChainNodeIds.ElementAtOrDefault(0) ?? selected;
        ExitCombo.SelectedValue = existing?.ChainNodeIds.ElementAtOrDefault(1);
        if (existing == null && EntryCombo.SelectedItem == null) EntryCombo.SelectedIndex = 0;
        RouteChanged(null!, null!);
    }
    private void RouteChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoutePreview == null) return;
        var entry = (EntryCombo.SelectedItem as ProxyNode)?.Name ?? "选择入口";
        var exit = (ExitCombo.SelectedItem as ProxyNode)?.Name ?? "选择出口";
        RoutePreview.Text = $"本机 → {entry} → {exit} → 目标网站";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (EntryCombo.SelectedValue is not string entry || ExitCombo.SelectedValue is not string exit) throw new FormatException("请选择入口节点和最终出口。");
            Result = ProxyChain.Create(NameBox.Text, entry, exit, nodes, existing?.Id);
            DialogResult = true;
        }
        catch (FormatException ex) { ErrorText.Text = ex.Message; }
    }
}
