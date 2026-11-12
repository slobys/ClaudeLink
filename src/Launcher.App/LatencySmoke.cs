using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Launcher.App.Services;

namespace Launcher.App;
public static partial class IntegrationSmoke
{
    private static async Task CheckNodeLatencyAsync(Action<bool,string> assert)
    {
        await using var fixture=new LatencyFixture();
        await using var fast=new DelayRelay(fixture.Port,40);
        await using var slow=new DelayRelay(fixture.Port,280);
        await using var timeout=new DelayRelay(fixture.Port,2200);
        await using var entry=new DelayRelay(fast.Port,20);
        ProxyNode Node(int port,string auth="user:secret")=>NodeImporter.Parse($"http://{auth}@127.0.0.1:{port}#Fixture").Nodes.Single();
        var a=Node(fast.Port); var b=Node(slow.Port);var t=Node(timeout.Port);var bad=Node(fast.Port,"wrong:wrong");var dead=Node(FreePort());var e=Node(entry.Port);
        var chain=ProxyChain.Create("Fixture chain",e.Id,a.Id,[e,a]);
        var broken=new ProxyNode { ChainNodeIds=["missing-a","missing-b"] };
        List<ProxyNode> nodes=[a,b,t,bad,dead,chain,broken]; var all=nodes.Append(e).ToList();
        var results=new ConcurrentDictionary<string,NodeLatencyResult>(); var pid=0;var port=0;var authRequired=false;
        var target=$"https://127.0.0.1:{fixture.Port}/latency";
        await NodeLatencyProbe.RunAsync(nodes,all,(id,result)=>results[id]=result,CancellationToken.None,target,900,fixture.Pem,(p,c)=>
        {
            pid=c;port=p;
            using var client=new HttpClient(new SocketsHttpHandler { UseProxy=false });
            using var response=client.GetAsync($"http://127.0.0.1:{p}/version").GetAwaiter().GetResult();authRequired=response.StatusCode==HttpStatusCode.Unauthorized;
        });
        assert(results[a.Id].HasDelay && results[b.Id].HasDelay && results[a.Id].Milliseconds < results[b.Id].Milliseconds,"Latency uses real TLS/HEAD requests through individually selected authenticated proxy nodes");
        assert(results[t.Id].State==LatencyState.Timeout && results[bad.Id].State==LatencyState.Failed && results[dead.Id].State==LatencyState.Failed,"Latency distinguishes API timeout, rejected credentials and unreachable nodes without zero-ms success");
        assert(results[chain.Id].HasDelay && entry.Requests>0 && fast.Requests>=2 && fixture.Requests==3,"Chain latency traverses both hops; failed nodes never bypass their proxy to the target");
        assert(results[broken.Id].State==LatencyState.Invalid && authRequired,"Invalid chains are isolated and the loopback latency API rejects unauthenticated access");
        assert(!ProcessTools.IsAlive(pid,GetStarted(pid)) && await IsPortClosedAsync(port),"Finishing a latency batch releases its isolated core and controller port");
        using var cancel=new CancellationTokenSource(); var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var cancelled=false;
        var pending=NodeLatencyProbe.RunAsync([t],all,(_,result)=>{if(result.State==LatencyState.Running)entered.TrySetResult();},cancel.Token,target,8000,fixture.Pem,(p,c)=>{port=p;pid=c;});
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancel.Cancel();
        try { await pending; } catch(OperationCanceledException) { cancelled=true; }
        assert(cancelled && await IsPortClosedAsync(port) && !Directory.EnumerateFiles(Path.Combine(AppPaths.Root,"latency"),"config.json",SearchOption.AllDirectories).Any(),"Cancelling active latency requests terminates the owned core and removes credential-bearing configs");
        assert(!SessionController.PendingRecovery().Any(),"Unprivileged latency does not create environment recovery records or change network settings");
    }
    private static long GetStarted(int pid) { try { using var p=Process.GetProcessById(pid);return p.StartTime.ToUniversalTime().Ticks; } catch(ArgumentException) { return 0; } }
    private sealed class LatencyFixture:IAsyncDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);private readonly CancellationTokenSource stop=new();private readonly Task loop;
        private readonly X509Certificate2 certificate;private int requests;
        public int Port {get;} public int Requests=>requests;public string Pem=>certificate.ExportCertificatePem();
        public LatencyFixture()
        {
            using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(names.Build());
            certificate=request.CreateSelfSigned(DateTimeOffset.Now.AddMinutes(-1),DateTimeOffset.Now.AddHours(1));
            listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;loop=Listen();
        }
        private async Task Listen() { try { while(!stop.IsCancellationRequested) {var client=await listener.AcceptTcpClientAsync(stop.Token);_=Serve(client);} } catch(OperationCanceledException) { } }
        private async Task Serve(TcpClient client)
        {
            using(client)try { using var tls=new SslStream(client.GetStream());await tls.AuthenticateAsServerAsync(certificate);var header=await ReadHeader(tls,stop.Token);
                if(header.StartsWith("HEAD /latency "))Interlocked.Increment(ref requests);await Task.Delay(30,stop.Token);await tls.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n"),stop.Token);
            } catch(Exception ex) when(ex is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel();listener.Stop();await loop;certificate.Dispose();stop.Dispose(); }
    }
    private static async Task<string> ReadHeader(Stream stream,CancellationToken token)
    {
        using var bytes=new MemoryStream();var one=new byte[1];
        while(bytes.Length<8192 && await stream.ReadAsync(one,token)>0) {bytes.WriteByte(one[0]);if(bytes.Length>=4 && Encoding.ASCII.GetString(bytes.GetBuffer(),(int)bytes.Length-4,4)=="\r\n\r\n")break;}
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
    private sealed class DelayRelay:IAsyncDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);private readonly CancellationTokenSource stop=new();private readonly Task loop;private readonly int target,delay;private int requests;
        public int Port {get;} public int Requests=>requests;
        public DelayRelay(int target,int delay) {this.target=target;this.delay=delay;listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;loop=Listen();}
        private async Task Listen() {try { while(!stop.IsCancellationRequested) {var client=await listener.AcceptTcpClientAsync(stop.Token);_=Serve(client);} } catch(OperationCanceledException) { } }
        private async Task Serve(TcpClient client)
        {
            using(client)try {
                var stream=client.GetStream();var header=await ReadHeader(stream,stop.Token);
                if(!header.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret")))) {await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\n\r\n"),stop.Token);return;}
                Interlocked.Increment(ref requests);await Task.Delay(delay,stop.Token);using var upstream=new TcpClient();await upstream.ConnectAsync(IPAddress.Loopback,target,stop.Token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"),stop.Token);
                await Task.WhenAny(stream.CopyToAsync(upstream.GetStream(),stop.Token),upstream.GetStream().CopyToAsync(stream,stop.Token));
            } catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) { }
        }
        public async ValueTask DisposeAsync() {stop.Cancel();listener.Stop();await loop;stop.Dispose();}
    }
}
