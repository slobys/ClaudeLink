using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

// .NET Framework installer: Windows includes the runtime, so setup has no prerequisites.
internal static class Program
{
    internal const string ProductId = "ClaudeLink-Desktop-1";
    internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
    internal static readonly string DefaultTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ClaudeLink");
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            // A shortcut starts the launcher inside its install directory. The updater
            // inherits that directory and would lock its own Directory.Move even after
            // the owning launcher has exited. Old launchers also need this safeguard.
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.System));
            if (args.Length == 3 && args[0] == "/cleanup") return Cleanup(args[1], int.Parse(args[2]));
            string target = DefaultTarget;
            string testRoot = Environment.GetEnvironmentVariable("CLAUDELINK_SETUP_TEST_ROOT");
            bool test = !string.IsNullOrEmpty(testRoot);
            if (test) { ValidateTestRoot(testRoot); target = Path.GetFullPath(testRoot); }
            bool uninstall = args.Contains("/uninstall");
            if (uninstall)
            {
                target = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                ValidateInstalledTarget(target);
                if (!args.Contains("/silent") && MessageBox.Show("卸载 Claude Link？\n\n节点、订阅和恢复记录将保留在当前用户的数据目录。", "卸载 Claude Link", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return 0;
                EnsureStopped(target);
                string temp = Path.Combine(Path.GetTempPath(), "ClaudeLink-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, temp);
                Process.Start(new ProcessStartInfo(temp, "/cleanup " + Quote(target) + " " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                return 0;
            }
#if UNINSTALLER
            MessageBox.Show("请通过 Windows 已安装的应用卸载 Claude Link。"); return 1;
#else
            if (args.Contains("/update"))
            {
                int index = Array.IndexOf(args, "/update"), launcherPid;
                long launcherStarted;
                if (index + 2 >= args.Length || !int.TryParse(args[index + 1], out launcherPid) || !long.TryParse(args[index + 2], out launcherStarted) || launcherPid <= 0)
                    throw new IOException("更新启动参数无效。");
                WaitForLauncherExit(launcherPid, launcherStarted);
                bool desktop = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Claude Link.lnk"));
                Install(target, desktop, args.Contains("/restart"), delegate(int progress) { });
                return 0;
            }
            if (args.Contains("/silent")) { Install(target, false, false, delegate(int progress) { }); return 0; }
            Application.Run(new SetupForm(target)); return 0;
#endif
        }
        catch (Exception ex)
        {
            if (!args.Contains("/silent")) MessageBox.Show("操作未完成：\n" + ex.Message, "Claude Link 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error);
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ClaudeLink-setup-error.txt"), ex.ToString(), Encoding.UTF8); } catch { }
            return 1;
        }
    }
    internal static string Quote(string text) { return "\"" + text.Replace("\"", "") + "\""; }
    private static void WaitForLauncherExit(int pid, long started)
    {
        if (pid == Process.GetCurrentProcess().Id) throw new IOException("无效的更新等待进程。");
        try
        {
            using (var launcher = Process.GetProcessById(pid))
            {
                if (launcher.StartTime.ToUniversalTime().Ticks != started) return;
                if (!launcher.WaitForExit(45000)) throw new IOException("启动器尚未退出，更新已取消。请先从托盘退出后重新安装。");
            }
        }
        catch (ArgumentException) { } // The exact owning process has already exited; never terminate another process.
    }
    private static void LaunchInstalled(string target)
    {
        var info = new ProcessStartInfo(Path.Combine(target, "ClaudeNetworkLauncher.exe")) { UseShellExecute = false };
        if (IsTestTarget(target)) info.Arguments = "--smoke-test " + Quote(target + ".restart-proof");
        Process.Start(info);
    }
    private static string RegistryKeyFor(string target)
    {
        return @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClaudeLink" + (IsTestTarget(target) ? ".Test." + Path.GetFileName(target).Substring("ClaudeLink-SetupTest-".Length) : "");
    }
    private static bool IsTestTarget(string target)
    {
        try { ValidateTestRoot(target); return true; } catch { return false; }
    }
    private static void ValidateTestRoot(string target)
    {
        string full = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string name = Path.GetFileName(full);
        Guid guid;
        if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith("ClaudeLink-SetupTest-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name.Substring("ClaudeLink-SetupTest-".Length), "N", out guid))
            throw new IOException("无效的隔离测试安装目录。");
    }
    private static void ValidateInstalledTarget(string target)
    {
        string full = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(full, DefaultTarget, StringComparison.OrdinalIgnoreCase)) ValidateTestRoot(full);
        string markerPath = Path.Combine(full, "claudelink-install.json");
        if (!File.Exists(markerPath)) throw new IOException("安装标记缺失，未删除任何文件。");
        var marker = Json.Deserialize<InstallMarker>(File.ReadAllText(markerPath));
        if (marker.ProductId != ProductId || !string.Equals(marker.Location, full, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装标记不匹配，未删除任何文件。");
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("安装目录为链接，已取消操作。");
    }
    internal static void EnsureStopped(string target)
    {
        foreach (var process in Process.GetProcessesByName("ClaudeNetworkLauncher"))
        {
            using (process)
            {
                bool matches;
                try
                {
                    string executable = ProcessImagePath(process.Id);
                    if (executable == null && process.HasExited) continue;
                    matches = executable == null || string.Equals(executable, Path.Combine(target, "ClaudeNetworkLauncher.exe"), StringComparison.OrdinalIgnoreCase);
                }
                catch { matches = true; }
                if (matches) throw new IOException("启动器或会话仍在运行，请从系统托盘退出后重试。");
            }
        }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    private static string ProcessImagePath(int pid)
    {
        IntPtr handle = OpenProcess(0x1000, false, pid); // Limited query also identifies an elevated helper without module access.
        if (handle == IntPtr.Zero) return null;
        try
        {
            int size = 32768; var path = new StringBuilder(size);
            return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : null;
        }
        finally { CloseHandle(handle); }
    }
    internal static void Install(string target, bool desktopShortcut, bool launch, Action<int> progress)
    {
        target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(target, DefaultTarget, StringComparison.OrdinalIgnoreCase)) ValidateTestRoot(target);
        EnsureStopped(target);
        bool test = IsTestTarget(target);
        string parent = Path.GetDirectoryName(target);
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, Path.GetFileName(target) + ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        string backup = null;
        try
        {
            Dictionary<string, string> manifest;
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.manifest.json"))
            using (var reader = new StreamReader(resource)) manifest = Json.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            {
                int count = 0;
                foreach (var pair in manifest)
                {
                    var entry = archive.GetEntry(pair.Key);
                    if (entry == null) throw new IOException("安装包文件缺失。");
                    string path = ChildPath(stage, pair.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open())
                    using (var output = File.Create(path)) input.CopyTo(output);
                    using (var input = File.OpenRead(path))
                    using (var hash = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                        if (actual != pair.Value) throw new IOException("安装包完整性校验失败。");
                    }
                    progress(++count * 80 / manifest.Count);
                }
            }
            var marker = new InstallMarker { ProductId = ProductId, Location = target, Files = manifest.Keys.ToArray() };
            File.WriteAllText(Path.Combine(stage, "claudelink-install.json"), Json.Serialize(marker), Encoding.UTF8);
            if (Directory.Exists(target))
            {
                ValidateInstalledTarget(target);
                backup = target + ".previous-" + Guid.NewGuid().ToString("N");
                Directory.Move(target, backup);
            }
            Directory.Move(stage, target);
            try
            {
                if (!test) CreateShortcuts(target, desktopShortcut);
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyFor(target)))
                {
                    key.SetValue("DisplayName", "Claude Link 网络启动器");
                    key.SetValue("DisplayVersion", "1.3.2");
                    key.SetValue("Publisher", "Claude Link");
                    key.SetValue("InstallLocation", target);
                    key.SetValue("DisplayIcon", Path.Combine(target, "ClaudeNetworkLauncher.exe"));
                    key.SetValue("UninstallString", Quote(Path.Combine(target, "Uninstall.exe")) + " /uninstall");
                    key.SetValue("QuietUninstallString", Quote(Path.Combine(target, "Uninstall.exe")) + " /uninstall /silent");
                    key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
                }
            }
            catch
            {
                // Keep the new valid installation; the user can rerun setup to repair shortcuts/registration.
                throw new IOException("程序文件已安装，但快捷方式或卸载注册未完成。请重新运行安装程序。");
            }
            if (backup != null) DeleteOwnedFiles(backup, false);
            progress(100);
            if (launch) LaunchInstalled(target);
        }
        catch
        {
            if (backup != null && !Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
        finally
        {
            // Stage is an exclusively created path below the already validated installation parent.
            if (Directory.Exists(stage))
            {
                foreach (var file in Directory.GetFiles(stage, "*", SearchOption.AllDirectories)) File.Delete(file);
                foreach (var directory in Directory.GetDirectories(stage, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length)) if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
            }
        }
    }
    private static string ChildPath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(":")) throw new IOException("安装包路径无效。");
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包包含越界路径。");
        return path;
    }
    private static void CreateShortcuts(string target, bool desktop)
    {
        Type type = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(type);
        string start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Claude Link");
        Directory.CreateDirectory(start);
        foreach (string path in new[] { Path.Combine(start, "Claude Link.lnk"), desktop ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Claude Link.lnk") : null })
        {
            if (path == null) continue;
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = Path.Combine(target, "ClaudeNetworkLauncher.exe"); link.WorkingDirectory = target;
            link.Description = "节点、订阅、TUN 与 Claude Code 启动"; link.IconLocation = Path.Combine(target, "ClaudeNetworkLauncher.exe"); link.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
            SHChangeNotify(0x2000, 0x0005, path, IntPtr.Zero);
        }
        SHChangeNotify(0x2000, 0x0005, Path.Combine(target, "ClaudeNetworkLauncher.exe"), IntPtr.Zero);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string item, IntPtr unused);
    private static int Cleanup(string target, int ownerPid)
    {
        ValidateInstalledTarget(target);
        try { using (var owner = Process.GetProcessById(ownerPid)) owner.WaitForExit(15000); } catch (ArgumentException) { }
        EnsureStopped(target);
        bool test = IsTestTarget(target);
        DeleteOwnedFiles(target, true);
        Registry.CurrentUser.DeleteSubKeyTree(RegistryKeyFor(target), false);
        if (!test)
        {
            string start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Claude Link");
            foreach (string path in new[] { Path.Combine(start, "Claude Link.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Claude Link.lnk") })
                if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(start) && !Directory.EnumerateFileSystemEntries(start).Any()) Directory.Delete(start);
        }
        if (test) File.WriteAllText(target + ".uninstalled", "ok", Encoding.ASCII);
        return 0;
    }
    private static void DeleteOwnedFiles(string target, bool validated)
    {
        // Upgrade backups are also siblings of the validated target, created by this process.
        string full = Path.GetFullPath(target);
        if (!validated)
        {
            string parent = Path.GetDirectoryName(full);
            string expectedParent = Path.GetDirectoryName(DefaultTarget);
            if (!string.Equals(parent, expectedParent, StringComparison.OrdinalIgnoreCase) && !string.Equals(parent, Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("备份目录不在预期位置。");
            if (!Path.GetFileName(full).Contains(".previous-")) throw new IOException("备份目录名称无效。");
        }
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("拒绝删除链接目录。");
        var marker = Json.Deserialize<InstallMarker>(File.ReadAllText(Path.Combine(full, "claudelink-install.json")));
        if (marker.ProductId != ProductId) throw new IOException("安装标记不匹配。");
        foreach (string relative in marker.Files)
        {
            string file = ChildPath(full, relative);
            string directory = Path.GetDirectoryName(file);
            while (!string.Equals(directory, full, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("安装子目录为链接，未继续删除。");
                directory = Path.GetDirectoryName(directory);
            }
            if (File.Exists(file)) File.Delete(file);
        }
        File.Delete(Path.Combine(full, "claudelink-install.json"));
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string relative in marker.Files)
        {
            string directory = Path.GetDirectoryName(ChildPath(full, relative));
            while (!string.Equals(directory, full, StringComparison.OrdinalIgnoreCase))
            {
                directories.Add(directory); directory = Path.GetDirectoryName(directory);
            }
        }
        foreach (string directory in directories.OrderByDescending(x => x.Length))
            if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0 && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        // Unknown user-added files are preserved.
        if (!Directory.EnumerateFileSystemEntries(full).Any()) Directory.Delete(full);
    }
    public sealed class InstallMarker
    {
        public string ProductId { get; set; }
        public string Location { get; set; }
        public string[] Files { get; set; }
    }
}

internal sealed class SetupForm : Form
{
    private readonly string target;
    private readonly Button install = new Button();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label status = new Label();
    private readonly CheckBox shortcut = new CheckBox();
    private readonly CheckBox launch = new CheckBox();
    private bool working, complete;
    public SetupForm(string destination)
    {
        target = destination; Text = "安装 Claude Link"; ClientSize = new Size(600, 410);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10); BackColor = Color.FromArgb(245, 245, 240);
        var title = new Label { Text = "让 Claude Code 准备就绪", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = false, Bounds = new Rectangle(30, 27, 540, 42) };
        var description = new Label { Text = "节点与订阅  ·  内置代理 / TUN  ·  环境准备与恢复", Bounds = new Rectangle(33, 83, 540, 30), ForeColor = Color.FromArgb(88, 97, 98) };
        var label = new Label { Text = "安装到当前 Windows 用户，无需管理员权限：", Bounds = new Rectangle(33, 135, 530, 25) };
        var path = new TextBox { Text = target, ReadOnly = true, Bounds = new Rectangle(33, 167, 534, 30) };
        shortcut.Text = "创建桌面快捷方式"; shortcut.Checked = true; shortcut.Bounds = new Rectangle(33, 215, 260, 28);
        launch.Text = "安装完成后打开"; launch.Checked = true; launch.Bounds = new Rectangle(305, 215, 260, 28);
        status.Text = "自带运行环境与代理内核，安装后即可添加节点。"; status.Bounds = new Rectangle(33, 265, 534, 35);
        progress.Bounds = new Rectangle(33, 307, 534, 5); progress.Visible = false;
        install.Text = "安装"; install.Bounds = new Rectangle(422, 337, 145, 44); install.BackColor = Color.FromArgb(173, 77, 53); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat; install.FlatAppearance.BorderSize = 0;
        var cancel = new Button { Text = "关闭", Bounds = new Rectangle(300, 337, 110, 44) }; cancel.Click += delegate { if (!working) Close(); };
        install.Click += InstallClick;
        Controls.AddRange(new Control[] { title, description, label, path, shortcut, launch, status, progress, install, cancel });
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (working) e.Cancel = true; };
    }
    private async void InstallClick(object sender, EventArgs e)
    {
        if (complete) { Close(); return; }
        working = true; install.Enabled = false; shortcut.Enabled = false; launch.Enabled = false; progress.Visible = true; status.Text = "正在解压并校验程序文件…";
        bool createShortcut = shortcut.Checked; bool open = launch.Checked;
        try
        {
            await Task.Run(delegate { Program.Install(target, createShortcut, false, delegate(int percent) { BeginInvoke((Action)delegate { progress.Value = percent; }); }); });
            complete = true; status.Text = "安装完成。可从开始菜单打开 Claude Link。"; install.Text = "完成";
            if (open) Process.Start(new ProcessStartInfo(Path.Combine(target, "ClaudeNetworkLauncher.exe")) { UseShellExecute = true });
        }
        catch (Exception ex) { status.Text = "安装未完成。"; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        finally { working = false; install.Enabled = true; }
    }
}
