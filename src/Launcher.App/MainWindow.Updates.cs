using System.Windows;
using Launcher.App.Services;

namespace Launcher.App;

public partial class MainWindow
{
    private UpdateOffer? availableUpdate;
    private string? downloadedUpdate;
    private bool checkingUpdates, downloadingUpdate, installingUpdate;
    private bool retryUpdatesAfterConnection;
    private CancellationTokenSource? updateCancellation;

    public void FillSmokeUpdate()
    {
        if (!smoke) throw new InvalidOperationException("Only available in isolated UI smoke mode.");
        UpdateBanner.Visibility = Visibility.Visible;
        UpdateBannerText.Text = "发现新版本 1.2.0 · 更新提示预览，不会访问更新服务器或安装程序。";
        UpdateActionButton.Content = "立即更新";
    }

    private async Task CheckUpdatesAsync(bool automatic)
    {
        if (checkingUpdates || downloadingUpdate || installingUpdate) return;
        checkingUpdates = true; ManualUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "正在检查 GitHub 新版本…";
        using var cancellation = new CancellationTokenSource();
        updateCancellation = cancellation;
        using var client = ReleaseUpdater.CreateClient(session.Connected ? session.Port : null);
        try
        {
            var offer = await ReleaseUpdater.CheckAsync(client, cancellation.Token);
            if (offer == null)
            {
                UpdateStatusText.Text = $"当前版本 {ReleaseSettings.CurrentVersion}，已是最新正式版。";
                return;
            }
            availableUpdate = offer; downloadedUpdate = null;
            UpdateBanner.Visibility = Visibility.Visible;
            UpdateActionButton.Content = "立即更新";
            UpdateBannerText.Text = $"发现新版本 {offer.Manifest.Version}，点击下载更新（{offer.Manifest.Size / 1048576.0:F1} MB）。";
            UpdateStatusText.Text = $"发现新版本 {offer.Manifest.Version}；发布清单签名已验证。";
            if (!automatic) Log($"已检测到新版本 {offer.Manifest.Version}。");
        }
        catch (OperationCanceledException) { UpdateStatusText.Text = "更新检查已取消或超时，可稍后重试。"; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or CryptographicException or FormatException or InvalidOperationException)
        {
            UpdateStatusText.Text = UpdateError(ex);
        }
        finally
        {
            if (ReferenceEquals(updateCancellation, cancellation)) updateCancellation = null;
            checkingUpdates = false; ManualUpdateButton.IsEnabled = true;
            if (retryUpdatesAfterConnection)
            {
                retryUpdatesAfterConnection = false;
                if (session.Connected && settings.AutoCheckUpdates) _ = CheckUpdatesAsync(true);
            }
        }
    }

    private void CheckUpdatesAfterConnection()
    {
        if (!settings.AutoCheckUpdates || downloadingUpdate || installingUpdate) return;
        if (checkingUpdates) retryUpdatesAfterConnection = true;
        else _ = CheckUpdatesAsync(true);
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(false);
    private void CancelUpdate_Click(object sender, RoutedEventArgs e) => updateCancellation?.Cancel();
    private void OpenRepository_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo(ReleaseSettings.ProjectPage.AbsoluteUri) { UseShellExecute = true });

    private async void UpdateAction_Click(object sender, RoutedEventArgs e)
    {
        if (availableUpdate == null || checkingUpdates || downloadingUpdate || installingUpdate) return;
        if (downloadedUpdate != null)
        {
            if (busy || nodeTesting || ClaudeRunning) { UpdateStatusText.Text = "请先完成当前操作，或正常退出 Claude CLI 终端，再安装更新。"; return; }
            installingUpdate = true; UpdateActionButton.IsEnabled = false;
            await RunBusyAsync(async () =>
            {
                if (!await ReleaseUpdater.IsInstallerValidAsync(downloadedUpdate, availableUpdate.Manifest))
                    throw new IOException("下载文件已变化，请重新检查并下载更新。");
                SaveUISettings();
                await session.StopAsync(); desktopOpened = false;
                Process.Start(ReleaseUpdater.InstallerStartInfo(downloadedUpdate));
                allowClose = true;
            }, "正在恢复临时环境并安装更新，完成后自动重新打开…");
            if (allowClose) Close();
            else { installingUpdate = false; UpdateActionButton.IsEnabled = true; }
            return;
        }

        downloadingUpdate = true; UpdateActionButton.IsEnabled = false; ManualUpdateButton.IsEnabled = false;
        UpdateCancelButton.Visibility = UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.Value = 0;
        using var cancellation = new CancellationTokenSource();
        updateCancellation = cancellation;
        using var client = ReleaseUpdater.CreateClient(session.Connected ? session.Port : null);
        try
        {
            var offer = availableUpdate;
            downloadedUpdate = await ReleaseUpdater.DownloadAsync(client, offer, Path.Combine(AppPaths.Root, "updates", offer.Manifest.Version),
                new Progress<double>(value => { if (ReferenceEquals(updateCancellation, cancellation) && downloadingUpdate && downloadedUpdate == null) { UpdateProgress.Value = value; UpdateBannerText.Text = $"正在下载 {offer.Manifest.Version} · {value:F0}%"; } }), cancellation.Token);
            UpdateBannerText.Text = $"版本 {offer.Manifest.Version} 已下载并通过校验。安装时会恢复当前网络环境，保留节点和订阅。";
            UpdateActionButton.Content = "安装并重启";
            UpdateStatusText.Text = "下载校验完成，点击顶部「安装并重启」。Claude 桌面窗口和项目不会被关闭。";
        }
        catch (OperationCanceledException) { UpdateBannerText.Text = "下载已取消或超时，可以点击重试。"; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or CryptographicException or UnauthorizedAccessException)
        {
            UpdateBannerText.Text = UpdateError(ex); downloadedUpdate = null;
        }
        finally
        {
            if (ReferenceEquals(updateCancellation, cancellation)) updateCancellation = null;
            downloadingUpdate = false; UpdateActionButton.IsEnabled = true; ManualUpdateButton.IsEnabled = true;
            UpdateCancelButton.Visibility = UpdateProgress.Visibility = Visibility.Collapsed;
        }
    }

    private static string UpdateError(Exception error) => error switch
    {
        CryptographicException => "更新签名或安装包校验失败，已停止更新。",
        HttpRequestException http when http.StatusCode == HttpStatusCode.NotFound => "暂未找到正式发布版本，可以稍后重试或打开 GitHub 项目。",
        HttpRequestException http when http.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests => "GitHub 暂时拒绝请求或限制访问次数，可稍后重试，或点击「GitHub 项目与安装包」下载安装。",
        HttpRequestException => "无法连接 GitHub 更新服务，可在连接节点后重新检查。",
        IOException io when io.Message.Length < 120 && !io.Message.Contains("://") => io.Message,
        _ => "更新信息读取失败，当前程序和配置保持可用，可稍后重试。"
    };
}
