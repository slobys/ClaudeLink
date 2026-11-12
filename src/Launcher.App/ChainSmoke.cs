using System.Net.Sockets;
using Launcher.App.Services;

namespace Launcher.App;

public static partial class IntegrationSmoke
{
    private static async Task CheckProxyChainAsync(Action<bool,string> assert)
    {
        await using var exit = new HttpFixture("chain-exit-ok");
        await using var entry = new ConnectRelay(exit.Port);
        var first = NodeImporter.Parse($"http://127.0.0.1:{entry.Port}#Entry").Nodes.Single();
        var last = NodeImporter.Parse($"http://exit-user:exit-secret@exit.fixture.invalid:{exit.Port}#Exit").Nodes.Single();
        var chain = ProxyChain.Create("Fixture chain",first.Id,last.Id,[first,last]);
        var settings = new LauncherSettings { Nodes=[first,last,chain], SelectedNodeId=chain.Id, ProxyPort=FreePort(), TunEnabled=false, StrictChecks=false, DisableIPv6=false, MatchTimeZone=false };
        var restored = new SettingsStore(); restored.Save(settings);
        assert(restored.Load().Nodes.Single(x=>x.Id==chain.Id).ChainNodeIds.SequenceEqual(chain.ChainNodeIds),"Encrypted settings preserve chain entry and exit IDs");
        var controller = new SessionController();
        try
        {
        await controller.StartAsync(settings,chain);
        using (var client = NetworkServices.ProxyClient(controller.Port,8))
            assert(await client.GetStringAsync("http://target.fixture.invalid/chain") == "chain-exit-ok","Real hosted core reaches HTTP destination through the two-hop chain");
        assert(entry.LastRequest.StartsWith($"CONNECT exit.fixture.invalid:{exit.Port} ",StringComparison.OrdinalIgnoreCase),"Entry receives a CONNECT tunnel to the configured exit hostname");
        assert(exit.LastRequest.StartsWith("GET /chain ",StringComparison.Ordinal) && exit.LastRequest.Contains("Host: target.fixture.invalid",StringComparison.OrdinalIgnoreCase)
            && exit.LastTunnelRequest.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("exit-user:exit-secret")),StringComparison.Ordinal),"Final exit receives destination request and its own credentials");
        await entry.DisposeAsync();
        var count=exit.Requests; var rejected=false;
        using (var client = NetworkServices.ProxyClient(controller.Port,5))
        {
            try { await client.GetStringAsync("http://target.fixture.invalid/after-entry-down"); }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { rejected=true; }
        }
        assert(rejected && exit.Requests == count,"Unavailable entry fails the chain without falling back to a direct exit");
        await controller.StopAsync();
        assert(await IsPortClosedAsync(settings.ProxyPort),"Stopping chain session closes its local proxy port");
        }
        finally { await controller.StopAsync(); }
    }
    private sealed class ConnectRelay : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource cancellation = new();
        private readonly int exitPort;
        private readonly Task loop;
        private bool disposed;
        public int Port { get; }
        public string LastRequest { get; private set; } = "";
        public ConnectRelay(int exitPort) { this.exitPort=exitPort;listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;loop=ListenAsync(); }
        private async Task ListenAsync()
        {
            try { while (!cancellation.IsCancellationRequested) { var client=await listener.AcceptTcpClientAsync(cancellation.Token); _=RelayAsync(client); } }
            catch (OperationCanceledException) { } catch (SocketException) when (cancellation.IsCancellationRequested) { }
        }
        private async Task RelayAsync(TcpClient accepted)
        {
            using var client=accepted;
            try
            {
                var input=client.GetStream(); using var header=new MemoryStream(); var next=new byte[1];
                while (header.Length < 8192 && await input.ReadAsync(next,cancellation.Token) > 0)
                {
                    header.WriteByte(next[0]);
                    if (header.Length >= 4 && Encoding.ASCII.GetString(header.GetBuffer(),(int)header.Length-4,4) == "\r\n\r\n") break;
                }
                LastRequest=Encoding.ASCII.GetString(header.ToArray());
                if (!LastRequest.StartsWith($"CONNECT exit.fixture.invalid:{exitPort} ",StringComparison.OrdinalIgnoreCase)) return;
                using var upstream=new TcpClient(); await upstream.ConnectAsync(IPAddress.Loopback,exitPort,cancellation.Token);
                await input.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"),cancellation.Token);
                var output=upstream.GetStream();
                await Task.WhenAny(input.CopyToAsync(output,cancellation.Token),output.CopyToAsync(input,cancellation.Token));
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
        }
        public async ValueTask DisposeAsync() { if(disposed)return;disposed=true;cancellation.Cancel();listener.Stop();await loop;cancellation.Dispose(); }
    }
}
