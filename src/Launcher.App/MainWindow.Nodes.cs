using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Launcher.App.Services;

namespace Launcher.App;

public partial class MainWindow
{
    private Style? nodeCardBaseStyle;
    private double nodeCardWidth;
    private void NodeGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width=NodesGrid.ActualWidth-SystemParameters.VerticalScrollBarWidth-4;
        if(width <= 0)return;
        var columns=Math.Clamp((int)(width/190),1,4);var itemWidth=Math.Floor(width/columns)-8;
        if(Math.Abs(itemWidth-nodeCardWidth)<1)return;
        nodeCardWidth=itemWidth;nodeCardBaseStyle ??= NodesGrid.ItemContainerStyle;
        var style=new Style(typeof(ListBoxItem),nodeCardBaseStyle);style.Setters.Add(new Setter(WidthProperty,itemWidth));NodesGrid.ItemContainerStyle=style;
    }
    public sealed record NodeFilter(string Code, string Label);
    private void RefreshChainPreview()
    {
        var node=NodeCombo.SelectedItem as ProxyNode;
        ActiveChainText.Visibility=node?.IsChain == true ? Visibility.Visible : Visibility.Collapsed;
        ActiveChainText.Text=node?.IsChain == true ? "本机 → "+ProxyChain.Path(node,settings.Nodes)+" → 目标网站" : "";
    }
    public sealed record NodeRow(ProxyNode Node, string Group, IReadOnlyList<ProxyNode> Available, bool Current, NodeLatencyResult? Latency = null)
    {
        private NodeRegion? Region => NodeRegion.ForNode(Node, Available);
        private static readonly Dictionary<string, BitmapImage> Flags = new();
        public string Name => Region == null ? Node.Name : Node.Name.Replace(Region.Emoji, "").Trim();
        public string Protocol => Node.IsChain ? "链式 · 2 跳" : Node.Protocol.ToUpperInvariant();
        public string Server => Node.Server;
        public string RegionLabel => Region?.Name ?? "未识别地区";
        public string RegionHint => $"按节点名称识别：{RegionLabel}；实际出口以连接检测为准";
        public string Description => Node.IsChain ? ProxyChain.Path(Node, Available) : Group;
        public string Subtitle => $"{RegionLabel} · {Description}";
        public bool Invalid => Node.IsChain && ProxyChain.IsMissing(Node, Available);
        public string ProtocolLabel => Protocol + (Current ? " · 当前" : "");
        public string State => Invalid ? "节点失效" : Latency?.Text ?? "未测速";
        public string Tone => Invalid ? "Error" : Latency?.Tone ?? "Pending";
        public string LatencyHint => Latency == null ? "点击一键测速，检测当前筛选结果。" : $"{Latency.MeasuredAt:HH:mm:ss} · {State}\n通过节点访问 HTTPS 测试地址；超时上限 8 秒。\n颜色仅供比较延迟，不代表账户状态。";
        public BitmapImage? Flag
        {
            get
            {
                if (Region == null) return null;
                if (!Flags.TryGetValue(Region.Code, out var flag))
                {
                    flag = new BitmapImage(new Uri($"pack://application:,,,/Assets/Flags/{Region.Code.ToLowerInvariant()}.png"));
                    flag.Freeze(); Flags[Region.Code] = flag;
                }
                return flag;
            }
        }
    }
    private void RefreshNodeFilters()
    {
        var region = NodeRegionFilter.SelectedValue as string ?? "all";
        var group = NodeGroupFilter.SelectedValue as string ?? "all";
        var regions = settings.Nodes.Select(x => NodeRegion.ForNode(x, settings.Nodes)).Where(x => x != null).DistinctBy(x => x!.Code)
            .OrderBy(x => x!.Name).Select(x => new NodeFilter(x!.Code, x.Name)).ToList();
        regions.Insert(0, new("all", "全部地区")); regions.Add(new("unknown", "未识别地区"));
        NodeRegionFilter.ItemsSource = regions;
        NodeRegionFilter.SelectedValue = regions.Any(x => x.Code == region) ? region : "all";
        var groups = new List<NodeFilter> { new("all", "全部分组"), new("manual", "手动添加") };
        groups.AddRange(settings.Subscriptions.Select(x => new NodeFilter(x.Id, x.Name)));
        NodeGroupFilter.ItemsSource = groups;
        NodeGroupFilter.SelectedValue = groups.Any(x => x.Code == group) ? group : "all";
    }
    private void RefreshNodeCards()
    {
        if (settings == null || NodesGrid == null) return;
        var prior = refreshing; refreshing = true;
        try
        {
            var query = NodeSearchBox.Text.Trim();
            var region = NodeRegionFilter.SelectedValue as string ?? "all";
            var group = NodeGroupFilter.SelectedValue as string ?? "all";
            var rows = settings.Nodes.Select(x => new NodeRow(x, settings.Subscriptions.FirstOrDefault(s => s.Id == x.GroupId)?.Name ?? "手动添加", settings.Nodes, x.Id == settings.SelectedNodeId, CurrentLatency(x)))
                .Where(x => (group == "all" || x.Node.GroupId == group)
                    && (region == "all" || (NodeRegion.ForNode(x.Node, settings.Nodes)?.Code ?? "unknown") == region)
                    && (query.Length == 0 || $"{x.Name} {x.Protocol} {x.Group} {x.RegionLabel} {NodeRegion.ForNode(x.Node,settings.Nodes)?.Code} {x.Server} {x.Description}".Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
            rows = NodeLatency.Sort(rows, x => x.Latency, NodeSort?.SelectedIndex ?? 0).ToList();
            NodesGrid.ItemsSource = rows;
            NodesGrid.SelectedItem = rows.FirstOrDefault(x => x.Current);
            NodesEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NodesEmpty.Text = settings.Nodes.Count == 0 ? "还没有节点\n添加分享链接，或前往订阅管理导入订阅。" : "没有匹配节点\n清空搜索或更换地区与分组。";
            NodeCountText.Text = $"显示 {rows.Count} / {settings.Nodes.Count} · {settings.Nodes.Count(x => x.IsChain)} 条链路";
        }
        finally { refreshing = prior; }
    }
    private void NodeFilterChanged(object sender, RoutedEventArgs e) { if (!refreshing && settings != null) RefreshNodeCards(); }
    private void NodeCardSelected(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || NodesGrid.SelectedItem is not NodeRow row || row.Node.Id == settings.SelectedNodeId) return;
        if (nodeTesting || session.Connected || ClaudeRunning) { StatusText.Text = "请先断开当前会话，再切换节点。"; RefreshNodeCards(); return; }
        settings.SelectedNodeId = row.Node.Id; report = null; store.Save(settings); RefreshLists();
        StatusText.Text = "已选择：" + row.Node.Name;
    }
    private void AddChain_Click(object sender, RoutedEventArgs e) => EditChain(null);
    private void EditChain(ProxyNode? existing)
    {
        if (nodeTesting || session.Connected || ClaudeRunning) { AppDialog.Notify(this, "请先断开当前会话，再编辑链式代理。"); return; }
        if (settings.Nodes.Count(x => !x.IsChain) < 2) { AppDialog.Notify(this, "请先添加入口与出口两个节点，再创建链式代理。"); return; }
        var dialog = new ChainDialog(settings.Nodes, existing, settings.SelectedNodeId) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        if (existing == null) settings.Nodes.Add(dialog.Result);
        else settings.Nodes[settings.Nodes.IndexOf(existing)] = dialog.Result;
        settings.SelectedNodeId = dialog.Result.Id; store.Save(settings);
        refreshing = true; NodeSearchBox.Text = ""; NodeRegionFilter.SelectedValue = "all"; NodeGroupFilter.SelectedValue = "all"; refreshing = false;
        RefreshLists();
        Log("链式代理已保存：本机 → 入口 → 最终出口。", LogKind.Success);
    }
    private ProxyNode? ReferencingChain(IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        return settings.Nodes.FirstOrDefault(x => x.IsChain && x.ChainNodeIds.Any(set.Contains));
    }
}
