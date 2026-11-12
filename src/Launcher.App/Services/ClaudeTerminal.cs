namespace Launcher.App.Services;

public static class ClaudeTerminal
{
    public static string? FindClaude(string custom = "")
    {
        if (!string.IsNullOrWhiteSpace(custom)) return File.Exists(custom) && ValidExtension(custom) ? Path.GetFullPath(custom) : null;
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd")
        };
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path)) continue;
            candidates.Add(Path.Combine(path, "claude.exe"));
            candidates.Add(Path.Combine(path, "claude.cmd"));
        }
        return candidates.FirstOrDefault(File.Exists);
    }
    private static bool ValidExtension(string path) => new[] { ".exe", ".cmd", ".bat" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static Process Start(string claude, string project, string sessionDirectory, int port, string language)
    {
        if (!Directory.Exists(project)) throw new DirectoryNotFoundException("项目目录不存在。");
        if (language is not ("en-US" or "zh-CN" or "en-GB" or "ja-JP")) throw new ArgumentException("不支持的进程语言。");
        var script = BuildScript(claude, project, sessionDirectory, port, language);
        var file = Path.Combine(sessionDirectory, "start-claude.ps1");
        File.WriteAllText(file, script, new UTF8Encoding(true));
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = true,
            WorkingDirectory = project,
            WindowStyle = ProcessWindowStyle.Normal
        };
        info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-ExecutionPolicy"); info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File"); info.ArgumentList.Add(file);
        return Process.Start(info) ?? throw new IOException("无法打开 Claude Code 终端。");
    }
    public static string BuildScript(string claude, string project, string sessionDirectory, int port, string language)
    {
        var proxy = ProcessTools.PsLiteral($"http://127.0.0.1:{port}");
        var lang = ProcessTools.PsLiteral(language.Replace('-', '_') + ".UTF-8");
        var completed = ProcessTools.PsLiteral(Path.Combine(sessionDirectory, "claude-complete.json"));
        return "$ErrorActionPreference='Stop'\r\n" +
            "try{$Host.UI.RawUI.WindowTitle='Claude Code · Claude Network Launcher'}catch{}\r\n" +
            $"Set-Location -LiteralPath {ProcessTools.PsLiteral(project)}\r\n" +
            "[Environment]::CurrentDirectory=(Get-Location).ProviderPath\r\n" +
            $"$env:HTTP_PROXY={proxy};$env:HTTPS_PROXY={proxy};$env:http_proxy={proxy};$env:https_proxy={proxy}\r\n" +
            "$env:NO_PROXY='localhost,127.0.0.1,::1';$env:no_proxy=$env:NO_PROXY;$env:ALL_PROXY=$null;$env:all_proxy=$null\r\n" +
            $"$env:LANG={lang};$env:LC_ALL=$env:LANG;$env:LANGUAGE={ProcessTools.PsLiteral(language.Replace('-', '_'))}\r\n" +
            "Remove-Item Env:\\CLAUDE_NETWORK_LAUNCH_ARGS_B64 -ErrorAction SilentlyContinue\r\n" +
            "$launcherExitCode=0\r\n" +
            $"try {{ & {ProcessTools.PsLiteral(claude)}; if($null -ne $LASTEXITCODE){{$launcherExitCode=$LASTEXITCODE}} }}\r\n" +
            "catch {$launcherExitCode=1;Write-Host 'Claude Code 启动失败，请检查 CLI 安装和 Git Bash。' -ForegroundColor Red}\r\n" +
            $"finally {{ @{{exitCode=$launcherExitCode}} | ConvertTo-Json | Set-Content -LiteralPath {completed} -Encoding UTF8 }}\r\n" +
            "if($launcherExitCode -ne 0){[void](Read-Host '按回车关闭窗口')}\r\n" +
            "exit $launcherExitCode\r\n";
    }
    public static Task<BrowserCheckLaunch> OpenBrowserCheckAsync(string sessionDirectory, int port, string language, string preference = "auto")
        => CheckBrowser.OpenAsync(sessionDirectory, port, language, preference);
}
