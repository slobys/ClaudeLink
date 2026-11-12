using System.Text.Json.Nodes;

namespace Launcher.Core;

public static class ProxyChain
{
    public static bool SupportsExit(ProxyNode node) => !node.IsChain && node.Protocol is "http" or "socks" or "vless" or "vmess" or "trojan" or "shadowsocks" or "anytls";
    public static IReadOnlyList<ProxyNode> Resolve(ProxyNode node, IReadOnlyList<ProxyNode>? available)
    {
        if (!node.IsChain) return [node];
        if (node.ChainNodeIds.Count != 2) throw new FormatException("链式代理需要一个入口节点和一个出口节点。");
        if (node.ChainNodeIds.Distinct().Count() != 2 || node.ChainNodeIds.Contains(node.Id)) throw new FormatException("入口和出口不能是同一个节点。");
        var hops = node.ChainNodeIds.Select(id => available?.FirstOrDefault(x => x.Id == id)
            ?? throw new FormatException("链式代理中的节点已失效，请重新选择入口和出口。")).ToList();
        if (hops.Any(x => x.IsChain)) throw new FormatException("不能将另一条链式代理用作入口或出口。");
        if (!SupportsExit(hops[1])) throw new FormatException("出口暂支持 HTTP、SOCKS、VLESS、Trojan 等基于 TCP 传输的节点。");
        return hops;
    }
    public static ProxyNode Create(string name, string entry, string exit, IReadOnlyList<ProxyNode> nodes, string? id = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new FormatException("请输入 1—120 字的链式代理名称。");
        var chain = new ProxyNode { Name = name.Trim(), ChainNodeIds = [entry, exit] };
        if (id != null) chain.Id = id;
        Resolve(chain, nodes);
        return chain;
    }
    public static string Path(ProxyNode node, IReadOnlyList<ProxyNode> nodes)
    {
        if (!node.IsChain) return "";
        return string.Join(" → ", node.ChainNodeIds.Select(id => nodes.FirstOrDefault(x => x.Id == id)?.Name ?? "节点已失效"));
    }
    public static bool IsMissing(ProxyNode node, IReadOnlyList<ProxyNode> nodes)
    {
        try { Resolve(node, nodes); return false; } catch (FormatException) { return true; }
    }
}
