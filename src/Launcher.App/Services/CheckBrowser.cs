using System.Globalization;
using System.Text.Encodings.Web;

namespace Launcher.App.Services;

public sealed record BrowserInstallation(string Id, string Name, string Path);
public sealed record BrowserCheckLaunch(string BrowserName, string Language, string ProfileDirectory, string LanguagePage);

public static class CheckBrowser
{
    public static IReadOnlyList<BrowserInstallation> Installed()
    {
        var result = new List<BrowserInstallation>();
        foreach (var entry in new[]
        {
            ("chrome", "Chrome", @"Google\Chrome\Application\chrome.exe"),
            ("edge", "Edge", @"Microsoft\Edge\Application\msedge.exe")
        })
        {
            foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                var path = Path.Combine(Environment.GetFolderPath(root), entry.Item3);
                if (File.Exists(path)) { result.Add(new(entry.Item1, entry.Item2, path)); break; }
            }
        }
        return result;
    }
    public static BrowserInstallation Select(string preference)
    {
        var installed = Installed();
        return (preference == "auto" ? installed.FirstOrDefault() : installed.FirstOrDefault(x => x.Id == preference))
            ?? throw new FileNotFoundException("未找到所选 Chrome / Edge，请安装浏览器或在环境设置改为自动选择。");
    }
    public static string NormalizeLanguage(string language)
    {
        if (language is not ("en-US" or "en-GB" or "zh-CN" or "ja-JP")) throw new ArgumentException("不支持的检查语言。");
        return CultureInfo.GetCultureInfo(language).Name;
    }
    public static string LanguageList(string language)
    {
        language = NormalizeLanguage(language);
        return language + "," + language.Split('-')[0];
    }
    public static void PrepareProfile(string profile, string language)
    {
        // Only callers' newly created, dedicated profiles are used. Normal browser profiles are never opened here.
        if (Directory.Exists(profile) && Directory.EnumerateFileSystemEntries(profile).Any())
            throw new IOException("检查浏览器配置目录已在使用，请重新打开专用窗口。");
        var defaults = Path.Combine(profile, "Default");
        Directory.CreateDirectory(defaults);
        var languages = LanguageList(language);
        AppPaths.WriteJson(Path.Combine(defaults, "Preferences"), new
        {
            intl = new { selected_languages = languages, accept_languages = languages },
            browser = new { check_default_browser = false }
        });
        AppPaths.WriteJson(Path.Combine(profile, "Local State"), new { intl = new { app_locale = language } });
    }
    public static ProcessStartInfo StartInfo(BrowserInstallation browser, string profile, string language, int? proxyPort, IEnumerable<string> targets)
    {
        var info = ProcessTools.Hidden(browser.Path);
        foreach (var arg in new[] { "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check",
            "--lang=" + NormalizeLanguage(language), "--accept-lang=" + LanguageList(language), "--disable-quic",
            "--force-webrtc-ip-handling-policy=disable_non_proxied_udp" }) info.ArgumentList.Add(arg);
        if (proxyPort is { } port) info.ArgumentList.Add($"--proxy-server=http://127.0.0.1:{port}");
        else info.ArgumentList.Add("--no-proxy-server");
        info.CreateNoWindow = false; info.WindowStyle = ProcessWindowStyle.Normal;
        foreach (var target in targets) info.ArgumentList.Add(target);
        return info;
    }
    public static BrowserCheckLaunch Open(string sessionDirectory, int port, string language, string preference)
    {
        var browser = Select(preference);
        var directory = Path.Combine(sessionDirectory, "browser-checks", Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(directory, "profile");
        PrepareProfile(profile, language);
        var page = Path.Combine(directory, "language-check.html");
        File.WriteAllText(page, LanguagePage(language), new UTF8Encoding(false));
        Process.Start(StartInfo(browser, profile, language, port, [new Uri(page).AbsoluteUri, NetworkServices.ClaudeLoginUrl, "https://ip.net.coffee/claude/"]));
        return new(browser.Name, language, profile, page);
    }
    public static string LanguagePage(string language)
    {
        var expected = JsonSerializer.Serialize(NormalizeLanguage(language));
        var list = HtmlEncoder.Default.Encode(LanguageList(language));
        return """
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>Claude Link · 语言核对</title>
<style>body{font:16px/1.7 "Segoe UI","Microsoft YaHei UI",sans-serif;background:#F4F7FB;color:#18283D;margin:40px auto;max-width:760px;padding:24px}main{background:white;border:1px solid #D8E1EC;border-radius:16px;padding:32px}h1{font-size:26px}pre{white-space:pre-wrap;background:#F8FAFC;padding:18px;border-radius:8px}a{color:#2563EB}#result{font-weight:bold}</style>
<main><h1>当前专用窗口的语言</h1><p>所选语言列表：EXPECTED_LIST</p>
<p id="result">正在读取浏览器设置…</p><pre id="browser-state"></pre>
<p>这里显示当前窗口实际提供给网页的语言与时区。网络核对页已在另一个标签打开。</p>
<a href="https://ip.net.coffee/claude/">打开网络核对页</a></main>
<script>
const expected=EXPECTED_JSON;
const state={language:navigator.language,languages:Array.from(navigator.languages),locale:Intl.DateTimeFormat().resolvedOptions().locale,timeZone:Intl.DateTimeFormat().resolvedOptions().timeZone};
document.getElementById('browser-state').textContent=JSON.stringify(state,null,2);
const matched=state.language.toLowerCase()===expected.toLowerCase()&&state.languages[0]?.toLowerCase()===expected.toLowerCase();
document.getElementById('result').textContent=matched?'语言匹配：'+state.language:'语言未匹配，请检查浏览器策略或重新打开专用窗口';
document.getElementById('result').style.color=matched?'#067647':'#B42318';
</script></html>
""".Replace("EXPECTED_JSON", expected).Replace("EXPECTED_LIST", list);
    }
}
