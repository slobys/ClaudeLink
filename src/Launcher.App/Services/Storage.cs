namespace Launcher.App.Services;

public static class AppPaths
{
    public static string Root { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeNetworkLauncher");
    public static string Sessions => Path.Combine(Root, "sessions");
    public static string Session(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("无效会话编号。");
        return Path.Combine(Sessions, id);
    }
    public static void WriteJson<T>(string path, T value) => AtomicWrite(path, JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Pretty));
    public static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        // Windows PowerShell 5.1 Set-Content -Encoding UTF8 writes a BOM.
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch (IOException) { return default; }
        catch (JsonException) { return default; }
    }
    public static void AtomicWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public static class SecretStore
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob data, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob result);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob data, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob result);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr ptr);

    public static byte[] Protect(byte[] bytes) => Transform(bytes, true);
    public static byte[] Unprotect(byte[] bytes) => Transform(bytes, false);
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var source = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        var result = new Blob();
        try
        {
            Marshal.Copy(bytes, 0, source.Data, bytes.Length);
            var ok = protect
                ? CryptProtectData(ref source, "ClaudeNetworkLauncher", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result)
                : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out result);
            if (!ok) throw new CryptographicException("无法使用当前 Windows 用户加密/解密配置。");
            var output = new byte[result.Size];
            Marshal.Copy(result.Data, output, 0, result.Size);
            return output;
        }
        finally
        {
            for (var i = 0; i < source.Size; i++) Marshal.WriteByte(source.Data, i, 0);
            Marshal.FreeHGlobal(source.Data);
            if (result.Data != IntPtr.Zero)
            {
                for (var i = 0; i < result.Size; i++) Marshal.WriteByte(result.Data, i, 0);
                LocalFree(result.Data);
            }
        }
    }
    public static void Save<T>(string path, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try { AppPaths.AtomicWrite(path, Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static T Load<T>(string path)
    {
        var bytes = Unprotect(File.ReadAllBytes(path));
        try { return JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("配置为空。"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

public sealed class SettingsStore
{
    private string FilePath => Path.Combine(AppPaths.Root, "settings.dat");
    public LauncherSettings Load() => File.Exists(FilePath) ? SecretStore.Load<LauncherSettings>(FilePath) : new();
    public void Save(LauncherSettings settings) => SecretStore.Save(FilePath, settings);
}
