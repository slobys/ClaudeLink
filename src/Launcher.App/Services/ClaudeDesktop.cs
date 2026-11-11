namespace Launcher.App.Services;

public sealed record DesktopInstallation(string Executable, bool Packaged);

public static class ClaudeDesktop
{
    public static bool IsGuiExecutable(string path)
    {
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            if (reader.BaseStream.Length < 96 || reader.ReadUInt16() != 0x5a4d) return false;
            reader.BaseStream.Position = 0x3c;
            var offset = reader.ReadInt32();
            if (offset < 64 || offset > reader.BaseStream.Length - 94) return false;
            reader.BaseStream.Position = offset;
            if (reader.ReadUInt32() != 0x00004550) return false;
            reader.BaseStream.Position = offset + 24 + 68;
            return reader.ReadUInt16() == 2; // IMAGE_SUBSYSTEM_WINDOWS_GUI; the CLI is subsystem 3.
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static async Task<DesktopInstallation?> FindAsync(string custom = "")
    {
        if (!string.IsNullOrWhiteSpace(custom))
        {
            var path = custom.Trim().Trim('"');
            if (Directory.Exists(path)) path = Path.Combine(path, "Claude.exe");
            if (!IsGuiExecutable(path)) throw new IOException("请选择 Claude 桌面客户端的 EXE；.local\\bin\\claude.exe 是命令行程序。也可以留空自动查找。");
            return new(Path.GetFullPath(path), path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase));
        }
        foreach (var relative in new[] { @"AnthropicClaude\Claude.exe", @"Programs\Claude\Claude.exe" })
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relative);
            if (IsGuiExecutable(path)) return new(path, false);
            var root = Path.GetDirectoryName(path)!;
            if (Directory.Exists(root))
                foreach (var versioned in Directory.EnumerateDirectories(root, "app-*").OrderByDescending(Directory.GetLastWriteTimeUtc))
                {
                    var executable = Path.Combine(versioned, "Claude.exe");
                    if (IsGuiExecutable(executable)) return new(executable, false);
                }
        }
        // MSIX paths change on every Claude upgrade; query the current registered package instead of pinning a version.
        var result = await ProcessTools.PowerShellAsync("$package=Get-AppxPackage -Name 'Claude' | Sort-Object Version -Descending | Select-Object -First 1; if($null -ne $package){$package.InstallLocation | ConvertTo-Json -Compress}", 12);
        if (result.Code == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            var location = JsonSerializer.Deserialize<string>(result.Output.Trim());
            if (!string.IsNullOrWhiteSpace(location))
            {
                var path = Path.Combine(location, "app", "Claude.exe");
                if (IsGuiExecutable(path)) return new(path, true);
            }
        }
        return null;
    }

    public static string CodeLink(string project)
    {
        if (!Directory.Exists(project)) throw new DirectoryNotFoundException("项目目录不存在。");
        return "claude://code/new?folder=" + Uri.EscapeDataString(Path.GetFullPath(project));
    }

    public static ProcessStartInfo StartInfo(DesktopInstallation installation, string project)
    {
        var link = CodeLink(project);
        if (installation.Packaged) return new ProcessStartInfo(link) { UseShellExecute = true };
        var info = new ProcessStartInfo(installation.Executable) { UseShellExecute = false, WorkingDirectory = project };
        info.ArgumentList.Add(link);
        return info;
    }

    public static void Open(DesktopInstallation installation, string project) => Process.Start(StartInfo(installation, project));
}
