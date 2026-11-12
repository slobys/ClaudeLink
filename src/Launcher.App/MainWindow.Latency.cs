using System.Windows;
using Launcher.App.Services;

namespace Launcher.App;
public partial class MainWindow
{
    private readonly Dictionary<string,NodeLatencyResult> nodeLatencies = new();
    private bool nodeTesting;
    private CancellationTokenSource? nodeTestCancellation;
    private Task? nodeTestTask;
    private NodeLatencyResult? CurrentLatency(ProxyNode node) => nodeLatencies.TryGetValue(node.Id,out var result)
        && result.Fingerprint == NodeLatency.Fingerprint(node,settings.Nodes) ? result : null;
    private void UpdateLatencyControls()
    {
        NodeTestButton.IsEnabled=nodeTesting || (!busy && !session.Connected && !ClaudeRunning);
        NodeTestButton.Content=nodeTesting ? "取消测速" : "一键测速";
        NodeActions.IsEnabled=SubscriptionsPage.IsEnabled=SettingsPage.IsEnabled=NodesGrid.IsEnabled=!nodeTesting;
    }
    private async void TestNodes_Click(object sender,RoutedEventArgs e)
    {
        if(nodeTesting) { nodeTestCancellation?.Cancel(); return; }
        if(busy || session.Connected || ClaudeRunning) { StatusText.Text="请先断开当前会话，再测速。"; return; }
        var nodes=NodesGrid.Items.OfType<NodeRow>().Select(x=>x.Node).ToList();
        if(nodes.Count==0) { NodeTestProgress.Text="当前没有可测速的节点"; return; }
        using var cancellation=new CancellationTokenSource(); nodeTestCancellation=cancellation; nodeTesting=true;
        foreach(var node in nodes) nodeLatencies[node.Id]=new(NodeLatency.Fingerprint(node,settings.Nodes),LatencyState.Pending);
        var completed=0; NodeTestProgress.Text=$"测速 0 / {nodes.Count}"; UpdateState(); RefreshNodeCards();
        // Await UI dispatch so no stale queued callback can overwrite a subsequent batch.
        nodeTestTask=NodeLatencyProbe.RunAsync(nodes,settings.Nodes,(id,result)=>Dispatcher.Invoke(()=>
        {
            nodeLatencies[id]=result;
            if(result.State!=LatencyState.Running)completed++;
            NodeTestProgress.Text=$"测速 {completed} / {nodes.Count}"; RefreshNodeCards();
        }),cancellation.Token);
        try { await nodeTestTask; StatusText.Text=$"测速完成：{completed} 个节点；可按延迟排序。"; Log("节点 HTTP 延迟测试完成。",LogKind.Success); }
        catch(OperationCanceledException) { StatusText.Text="测速已取消，已完成的结果已保留。"; }
        catch(Exception) { StatusText.Text="测速未完成，请检查网络或稍后重试。"; Log("节点测速未完成。",LogKind.Warning); }
        finally
        {
            var cancelled=cancellation.IsCancellationRequested;
            foreach(var node in nodes)
                if(nodeLatencies[node.Id].State is LatencyState.Pending or LatencyState.Running)
                    nodeLatencies[node.Id]=new(NodeLatency.Fingerprint(node,settings.Nodes),cancelled ? LatencyState.Cancelled : LatencyState.Failed);
            NodeTestProgress.Text=cancelled ? $"已取消 · 完成 {completed} / {nodes.Count}" : $"已完成 {completed} / {nodes.Count}";
            nodeTesting=false; nodeTestCancellation=null; nodeTestTask=null; RefreshNodeCards(); UpdateState();
        }
    }
    private async Task StopNodeTestAsync()
    {
        nodeTestCancellation?.Cancel();
        if(nodeTestTask is { } task) { try { await task; } catch(Exception) { } }
        while(nodeTesting) await Task.Delay(20);
    }
    private async void awaitNodeTestClose() { await StopNodeTestAsync(); Close(); }
    internal void FillLatencyPreview()
    {
        var states=new[] { LatencyState.Success,LatencyState.Success,LatencyState.Success,LatencyState.Timeout,LatencyState.Failed,LatencyState.Running,LatencyState.Cancelled,LatencyState.Pending };
        var index=0;
        foreach(var node in settings.Nodes)
        {
            var state=states[index%states.Length]; var ms=state==LatencyState.Success ? index%3==0 ? 86 : index%3==1 ? 430 : 1280 : (int?)null;
            nodeLatencies[node.Id]=new(NodeLatency.Fingerprint(node,settings.Nodes),state,ms);index++;
        }
        RefreshNodeCards(); NodeTestProgress.Text="界面示例 · 非实测数据";
    }
}
