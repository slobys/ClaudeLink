namespace Launcher.App.Services;
public enum LogKind { Info, Success, Warning, Error }
public sealed record LogRecord(DateTime Time, string Message, LogKind Kind)
{
    public string Label => Kind switch { LogKind.Success => "完成", LogKind.Warning => "注意", LogKind.Error => "错误", _ => "信息" };
    public string BrushKey => Kind switch { LogKind.Success => "Success", LogKind.Warning => "Warning", LogKind.Error => "Error", _ => "Info" };
}
