using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Launcher.App.Services;

public static class NodeLatencyProbe
{
    public static async Task RunAsync(IReadOnlyList<ProxyNode> nodes, IReadOnlyList<ProxyNode> available,
        Action<string,NodeLatencyResult> changed, CancellationToken token, string target = NodeLatency.Target, int timeoutMs = NodeLatency.TimeoutMs, Action<int,int>? started = null)
    {
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var secret=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var plan=NodeLatency.Build(nodes,available,port,secret);
        foreach(var id in plan.InvalidNodeIds) changed(id,new(plan.Fingerprints[id],LatencyState.Invalid));
        if(plan.Tags.Count==0)return;
        var folder=Path.Combine(AppPaths.Root,"latency",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        Process? core=null; CoreJob? job=null;
        try
        {

            var path=Path.Combine(folder,"config.json"); await File.WriteAllTextAsync(path,plan.Config.ToJsonString(),token);
            token.ThrowIfCancellationRequested();
            core=Process.Start(ProcessTools.Hidden(CoreAssets.Ensure(),"run","-c",path)) ?? throw new IOException("无法启动测速内核。");
            job=new CoreJob(core);
            using var client=new HttpClient(new SocketsHttpHandler { UseProxy=false }) { BaseAddress=new Uri($"http://127.0.0.1:{port}"), Timeout=TimeSpan.FromMilliseconds(NodeLatency.TimeoutMs+2000) };
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",secret);
            var ready=false;
            for(var i=0;i<60;i++)
            {
                token.ThrowIfCancellationRequested(); if(core.HasExited)break;
                try { using var response=await client.GetAsync("version",token); ready=response.IsSuccessStatusCode; } catch(HttpRequestException) { }
                if(ready)break; await Task.Delay(100,token);
            }
            if(!ready)throw new IOException("测速内核未能启动，请稍后重试。");
            File.Delete(path); started?.Invoke(port,core.Id);
            await Parallel.ForEachAsync(plan.Tags,new ParallelOptions { MaxDegreeOfParallelism=4,CancellationToken=token },async (item,ct)=>
            {
                changed(item.Key,new(plan.Fingerprints[item.Key],LatencyState.Running));
                var result=await MeasureAsync(client,item.Value,target,timeoutMs,ct);
                changed(item.Key,new(plan.Fingerprints[item.Key],result.State,result.Milliseconds));
            });
        }
        finally
        {
            job?.Dispose();
            if(core != null) { try { if(!core.HasExited)core.Kill(true); await core.WaitForExitAsync(); } catch(InvalidOperationException) { } finally { core.Dispose(); } }
            try { Directory.Delete(folder,true); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }
    internal static async Task<(LatencyState State,int? Milliseconds)> MeasureAsync(HttpClient client,string tag,string target,int timeout,CancellationToken token)
    {
        try
        {
            using var response=await client.GetAsync($"proxies/{Uri.EscapeDataString(tag)}/delay?timeout={timeout}&url={Uri.EscapeDataString(target)}",token);
            if(response.StatusCode==HttpStatusCode.GatewayTimeout)return(LatencyState.Timeout,null);
            if(!response.IsSuccessStatusCode)return(LatencyState.Failed,null);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            return json.RootElement.TryGetProperty("delay",out var delay) && delay.TryGetInt32(out var ms) && ms>0 && ms<=timeout
                ? (LatencyState.Success,ms) : (LatencyState.Failed,null);
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested) { return(LatencyState.Timeout,null); }
        catch(HttpRequestException) { return(LatencyState.Failed,null); }
        catch(JsonException) { return(LatencyState.Failed,null); }
    }
}
