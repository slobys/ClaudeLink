using System.Text.RegularExpressions;

namespace Launcher.App.Services;

public sealed class SessionDiagnostics
{
    private readonly object sync = new();
    private readonly Queue<string> coreOutput = new();
    private readonly HashSet<string> privateValues = new(StringComparer.Ordinal);
    private readonly string directory;
    public SessionDiagnostics(string directory) => this.directory = directory;
    public void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
            foreach (var field in obj)
            {
                if (field.Value is JsonValue && field.Key.ToLowerInvariant() is "password" or "username" or "uuid" or "server" or "secret" or "token" or "private_key")
                    privateValues.Add(field.Value.ToString());
                else if (field.Value is not null) RedactNode(field.Value);
            }
        else if (node is JsonArray array)
            foreach (var child in array) if (child is not null) RedactNode(child);
    }
    public string Sanitize(string message)
    {
        message = Regex.Replace(message, @"\x1B\[[0-?]*[ -/]*[@-~]", "");
        foreach (var value in privateValues.OrderByDescending(x => x.Length))
            if (value.Length > 0) message = message.Replace(value, "[已隐藏]", StringComparison.Ordinal);
        return Regex.Replace(message, @"(?i)(?:https?|vless|vmess|trojan|ss|hy2|hysteria2|tuic)://\S+", "[地址已隐藏]");
    }
    public async Task CaptureCoreAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                lock (sync)
                {
                    coreOutput.Enqueue(Sanitize(line.Length > 1200 ? line[..1200] : line));
                    while (coreOutput.Count > 40) coreOutput.Dequeue();
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }
    public void RecordFailure(string stage, Exception exception, int? exitCode)
    {
        string[] output; lock (sync) output = coreOutput.ToArray();
        AppPaths.WriteJson(Path.Combine(directory, "diagnostic.json"), new
        {
            Stage = stage, ExceptionType = exception.GetType().Name,
            NativeErrorCode = exception is Win32Exception win ? win.NativeErrorCode : (int?)null,
            CoreExitCode = exitCode, Message = Sanitize(exception.Message),
            CoreOutput = output, StackTrace = exception.StackTrace
        });
    }
    public static string StageLabel(string stage) => stage switch
    {
        "read-session" => "读取会话",
        "validate-config" => "生成配置",
        "extract-core" => "校验内核",
        "check-config" => "检查内核配置",
        "apply-ipv6" => "读取或设置 IPv6",
        "start-core" => "启动内核",
        "attach-job" => "监护内核进程",
        "wait-listener" => "等待代理端口",
        "running" => "运行内核",
        _ => stage
    };
}
