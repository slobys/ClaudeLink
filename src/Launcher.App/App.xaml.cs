using System.Windows;
using Launcher.App.Services;

namespace Launcher.App;

public partial class App : Application
{
    private Mutex? instance;
    private bool ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("操作未完成，请检查输入或文件权限后重试。", "Claude Link", MessageBoxButton.OK, MessageBoxImage.Information);
            args.Handled = true;
        };
        try
        {
            if (Guid.TryParseExact(Environment.GetEnvironmentVariable("CLAUDELINK_TEST_ID"), "N", out var testId))
                AppPaths.Root = Path.Combine(AppPaths.Root, "tests", testId.ToString("N"));
            if (e.Args.Length == 2 && e.Args[0] is "--engine-host" or "--recover")
            {
                Shutdown(await EngineHost.RunAsync(e.Args[1], e.Args[0] == "--recover")); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--integration-test")
            {
                Shutdown(await IntegrationSmoke.RunAsync(e.Args[1])); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--verify-release")
            {
                Shutdown(await ReleaseProbe.RunAsync(e.Args[1])); return;
            }
            var smoke = e.Args.Length == 2 && e.Args[0] == "--smoke-test";
            if (smoke)
                AppPaths.Root = Path.Combine(AppPaths.Root, "ui-tests", Guid.NewGuid().ToString("N"));
            if (!smoke)
            {
                var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
                instance = new Mutex(true, @"Local\ClaudeNetworkLauncher_" + sid, out ownsMutex);
                if (!ownsMutex) { MessageBox.Show("启动器已经运行，请从任务栏或系统托盘打开。", "Claude Link"); Shutdown(); return; }
            }
            Directory.CreateDirectory(AppPaths.Root);
            SessionController.CleanupStaleSessions();
            var settings = smoke ? UiSmoke.DemoSettings() : new SettingsStore().Load();
            var window = new MainWindow(settings, smoke);
            MainWindow = window; ShutdownMode = ShutdownMode.OnMainWindowClose; window.Show();
            if (smoke) await UiSmoke.CaptureAsync(window, e.Args[1]);
        }
        catch (Exception ex)
        {
            if (e.Args.FirstOrDefault() is "--engine-host" or "--recover") { Shutdown(1); return; }
            var message = ex is CryptographicException
                ? "无法解密本地配置。请使用原 Windows 用户登录；原配置未被覆盖。"
                : "启动器无法完成初始化，请检查安装文件与当前用户目录的访问权限。";
            MessageBox.Show(message, "Claude Link", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
