using System.Windows;
using Launcher.App.Services;

namespace Launcher.App;

public partial class App : Application
{
    private Mutex? instance;
    private bool ownsMutex;
    private TemporaryData.TestWorkspace? smokeWorkspace;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            if (e.Args.Length==2 && e.Args[0]=="--smoke-test")
            {
                AppPaths.WriteJson(Path.Combine(e.Args[1],"ui-failure.json"),new { Error=args.Exception.ToString() });
                args.Handled=true; Shutdown(1); return;
            }
            AppDialog.Notify(Current?.MainWindow, "操作未完成，请检查输入或文件权限后重试。", "Claude Link", "Error");
            args.Handled = true;
        };
        try
        {
            if (Guid.TryParseExact(Environment.GetEnvironmentVariable("CLAUDELINK_TEST_ID"), "N", out var testId))
                AppPaths.Root = Path.Combine(TemporaryData.TestParent, testId.ToString("N"));
            if (e.Args.Length == 3 && e.Args[0] == "--clean-browser")
            {
                var clean = false;
                for (var attempt = 0; attempt < 4 && !clean; attempt++)
                {
                    clean = await TemporaryData.CleanBrowserAsync(e.Args[1], e.Args[2]);
                    if (!clean && attempt < 3) await Task.Delay(500);
                }
                Shutdown(clean ? 0 : 1); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--clean-temporary")
            {
                AppPaths.WriteJson(e.Args[1], new { Removed = await TemporaryData.SweepAsync(AppPaths.Root) });
                Shutdown(); return;
            }
            if (e.Args.Length == 2 && e.Args[0] is "--engine-host" or "--recover")
            {
                Shutdown(await EngineHost.RunAsync(e.Args[1], e.Args[0] == "--recover")); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--latency-test") { Shutdown(await IntegrationSmoke.RunLatencyAsync(e.Args[1])); return; }
            if (e.Args.Length == 2 && e.Args[0] == "--integration-test")
            {
                Shutdown(await IntegrationSmoke.RunAsync(e.Args[1])); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--verify-release")
            {
                Shutdown(await ReleaseProbe.RunAsync(e.Args[1])); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--export-brand")
            {
                BrandAssets.Export(e.Args[1]); Shutdown(); return;
            }
            var smoke = e.Args.Length == 2 && e.Args[0] == "--smoke-test";
            if(smoke)System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            if (smoke)
                smokeWorkspace = new TemporaryData.TestWorkspace();
            if (!smoke)
            {
                var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
                instance = new Mutex(true, @"Local\ClaudeNetworkLauncher_" + sid, out ownsMutex);
                if (!ownsMutex) { AppDialog.Notify(null, "启动器已经运行，请从任务栏或系统托盘打开。"); Shutdown(); return; }
            }
            Directory.CreateDirectory(AppPaths.Root);
            SessionController.CleanupStaleSessions();
            if (!smoke) _ = Task.Run(async () =>
            {
                try { await TemporaryData.SweepAsync(AppPaths.Root); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            });
            var settings = smoke ? UiSmoke.DemoSettings() : new SettingsStore().Load();
            var window = new MainWindow(settings, smoke);
            MainWindow = window; ShutdownMode = ShutdownMode.OnMainWindowClose; window.Show();
            if (smoke) await UiSmoke.CaptureAsync(window, e.Args[1]);
        }
        catch (Exception ex)
        {
            if (e.Args.Length==2 && e.Args[0]=="--smoke-test")
            {
                Directory.CreateDirectory(e.Args[1]);
                AppPaths.WriteJson(Path.Combine(e.Args[1],"ui-failure.json"),new { Error=ex.ToString() });
                Shutdown(1); return;
            }
            if (e.Args.FirstOrDefault() is "--engine-host" or "--recover" or "--clean-browser" or "--clean-temporary") { Shutdown(1); return; }
            var message = ex is CryptographicException
                ? "无法解密本地配置。请使用原 Windows 用户登录；原配置未被覆盖。"
                : "启动器无法完成初始化，请检查安装文件与当前用户目录的访问权限。";
            AppDialog.Notify(null, message, "Claude Link", "Error");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        smokeWorkspace?.Dispose();
        if (ownsMutex) instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
