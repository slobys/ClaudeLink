using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launcher.Core;

public sealed record NodeRegion(string Code, string Name, string Names, string Short)
{
    private static readonly IReadOnlyList<NodeRegion> Definitions = JsonSerializer.Deserialize<List<NodeRegion>>(
        Assembly.GetExecutingAssembly().GetManifestResourceStream("Node.regions.json")!)!;
    public string Emoji => string.Concat(Code.Select(x => char.ConvertFromUtf32(0x1f1e6 + x - 'A')));
    public static NodeRegion? FromName(string name) => Definitions.FirstOrDefault(x => name.Contains(x.Emoji, StringComparison.Ordinal)
        || Regex.IsMatch(name, x.Names, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        || Regex.IsMatch(name, @"(?<![A-Za-z])(?:" + x.Short + @")(?=[^A-Za-z]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    public static NodeRegion? ForNode(ProxyNode node, IReadOnlyList<ProxyNode> nodes)
        => node.IsChain ? nodes.FirstOrDefault(x => x.Id == node.ChainNodeIds.LastOrDefault()) is { } exit ? FromName(exit.Name) : null : FromName(node.Name);
}
