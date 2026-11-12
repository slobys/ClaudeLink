using System.Net.WebSockets;
using System.Reflection;

namespace Launcher.App.Services;

// Attaches only to the disposable profile just launched by this application. No screen capture APIs are used.
public static class BrowserPrivacy
{
    public const string CheckUrl = "https://ip.net.coffee/claude/";
    public static string Script(Uri target)
    {
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("Browser.privacy.js")!;
        using var reader = new StreamReader(input);
        return reader.ReadToEnd().Replace("__CLAUDELINK_ORIGIN__", JsonSerializer.Serialize(target.GetLeftPart(UriPartial.Authority)));
    }

    public static async Task<bool> ConfigureAsync(string profile, string target = CheckUrl)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var token = budget.Token;
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        string? port = null;
        while (port == null)
        {
            try
            {
                var lines = (await File.ReadAllTextAsync(Path.Combine(profile, "DevToolsActivePort"), token)).Split('\n');
                if (lines.Length > 1 && int.TryParse(lines[0].Trim(), out var value) && value is > 0 and < 65536
                    && lines[1].StartsWith("/devtools/browser/", StringComparison.Ordinal)) port = value.ToString();
            }
            catch (IOException) { }
            if (port == null) await Task.Delay(100, token);
        }
        Uri? address = null;
        while (address == null)
        {
            var pages = JsonNode.Parse(await client.GetStringAsync($"http://127.0.0.1:{port}/json/list", token))!.AsArray();
            var page = pages.FirstOrDefault(x => x?["type"]?.ToString() == "page" && x["url"]?.ToString() == "about:blank");
            if (page?["webSocketDebuggerUrl"] is { } websocket) address = new Uri(websocket.ToString());
            else await Task.Delay(100, token);
        }
        var socket = new ClientWebSocket();
        var retained = false;
        try
        {
            await socket.ConnectAsync(address, token);
            var id = 0;
            async Task<JsonNode?> Send(string method, object parameters)
            {
                var requestId = ++id;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id = requestId, method, @params = parameters });
                await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, token);
                while (true)
                {
                    using var message = new MemoryStream();
                    var buffer = new byte[8192];
                    ValueWebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(buffer.AsMemory(), token);
                        if (received.MessageType == WebSocketMessageType.Close) throw new IOException("核对窗口已关闭。");
                        message.Write(buffer, 0, received.Count);
                        if (message.Length > 1048576) throw new IOException("核对窗口响应过大。");
                    } while (!received.EndOfMessage);
                    var response = JsonNode.Parse(message.ToArray())!;
                    if (response["id"]?.GetValue<int>() != requestId) continue;
                    if (response["error"] != null)
                    {
                        if (method == "Runtime.evaluate") return null; // The page may be between navigation contexts.
                        throw new IOException("无法设置核对窗口的隐私显示。");
                    }
                    return response["result"];
                }
            }
            await Send("Page.enable", new { });
            await Send("Page.addScriptToEvaluateOnNewDocument", new { source = Script(new Uri(target)) });
            await Send("Page.navigate", new { url = target });
            while (true)
            {
                var result = await Send("Runtime.evaluate", new { expression = "document.documentElement.dataset.claudeLinkIpHidden === 'true' && document.getElementById('ipMaskToggle')?.checked === true", returnByValue = true });
                if (result?["result"]?["value"]?.GetValue<bool>() == true)
                {
                    retained = true;
                    _ = HoldUntilClosedAsync(socket);
                    return true;
                }
                await Task.Delay(200, token);
            }
        }
        finally { if (!retained) socket.Dispose(); }
    }

    private static async Task HoldUntilClosedAsync(ClientWebSocket socket)
    {
        // Chrome removes init scripts when their DevTools session detaches; keep this owned session alive for refreshes.
        try
        {
            var buffer = new byte[8192];
            while (socket.State == WebSocketState.Open)
                if ((await socket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None)).MessageType == WebSocketMessageType.Close) break;
        }
        catch (WebSocketException) { }
        finally { socket.Dispose(); }
    }
}
