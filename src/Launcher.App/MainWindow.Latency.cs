using System.Windows;
using System.Collections.Concurrent;
using System.Windows.Threading;
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
        var updates=new ConcurrentQueue<(string Id,NodeLatencyResult Result)>();
        void Flush()
        {
            var changed=false;
            while(updates.TryDequeue(out var update))
            {
                ApplyNodeLatency(update.Id,update.Result);
                if(update.Result.State!=LatencyState.Running)completed++;
                changed=true;
            }
            if(changed) { NodeTestProgress.Text=$"测速 {completed} / {nodes.Count}"; SortLiveNodeRows(); }
        }
        // Network workers never wait for WPF layout. Only changed rows are notified, at most 10 batches/second.
        var timer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(100) };
        timer.Tick+=(_,_)=>Flush();timer.Start();
        nodeTestTask=NodeLatencyProbe.RunAsync(nodes,settings.Nodes,(id,result)=>updates.Enqueue((id,result)),cancellation.Token);
        try { await nodeTestTask; Flush(); StatusText.Text=$"测速完成：{completed} 个节点；可按延迟排序。"; Log("节点 HTTP 延迟测试完成。",LogKind.Success); }
        catch(OperationCanceledException) { StatusText.Text="测速已取消，已完成的结果已保留。"; }
        catch(Exception) { StatusText.Text="测速未完成，请检查网络或稍后重试。"; Log("节点测速未完成。",LogKind.Warning); }
        finally
        {
            timer.Stop();Flush();
            var cancelled=cancellation.IsCancellationRequested;
            foreach(var node in nodes)
                if(nodeLatencies[node.Id].State is LatencyState.Pending or LatencyState.Running)
                    nodeLatencies[node.Id]=new(NodeLatency.Fingerprint(node,settings.Nodes),cancelled ? LatencyState.Cancelled : LatencyState.Failed);
            NodeTestProgress.Text=cancelled ? $"已取消 · 完成 {completed} / {nodes.Count}" : $"已完成 {completed} / {nodes.Count}";
            foreach(var node in nodes)if(visibleNodeRows.TryGetValue(node.Id,out var row))row.UpdateLatency(nodeLatencies[node.Id]);
            SortLiveNodeRows();nodeTesting=false; nodeTestCancellation=null; nodeTestTask=null; UpdateState();
        }
    }
    private void ApplyNodeLatency(string id,NodeLatencyResult result)
    {
        nodeLatencies[id]=result;
        if(visibleNodeRows.TryGetValue(id,out var row))row.UpdateLatency(result);
    }
    internal void PreviewIncrementalLatency(string id,int milliseconds)
    {
        if(!smoke)throw new InvalidOperationException("Preview requires isolated UI smoke mode.");
        var node=settings.Nodes.Single(x=>x.Id==id);
        ApplyNodeLatency(id,new(NodeLatency.Fingerprint(node,settings.Nodes),LatencyState.Success,milliseconds));SortLiveNodeRows();
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
    internal bool PreviewNodeTest(bool running)
    {
        if(!smoke)throw new InvalidOperationException("Preview requires isolated UI smoke mode.");
        var cancelled=nodeTestCancellation?.IsCancellationRequested == true;
        nodeTestCancellation?.Dispose(); nodeTestCancellation=running ? new CancellationTokenSource() : null;
        nodeTesting=running; UpdateState(); NodeTestProgress.Text=running ? "测速 3 / 10 · 界面示例" : "界面示例 · 非实测数据";
        return cancelled;
    }
}
