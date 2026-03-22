namespace VoidCraftLauncher.Models;

public enum ToastSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public class ToastItem
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public ToastSeverity Severity { get; set; } = ToastSeverity.Info;
    public int DurationMs { get; set; } = 4000;

    public string IconGlyph => Severity switch
    {
        ToastSeverity.Success => "OK",
        ToastSeverity.Warning => "!",
        ToastSeverity.Error => "X",
        _ => "i"
    };

    public string BackgroundBrush => Severity switch
    {
        ToastSeverity.Success => "#1A221B",
        ToastSeverity.Warning => "#241F16",
        ToastSeverity.Error => "#261819",
        _ => "#171A23"
    };

    public string BorderBrush => Severity switch
    {
        ToastSeverity.Success => "#4FAE87",
        ToastSeverity.Warning => "#D8A24B",
        ToastSeverity.Error => "#D46A6A",
        _ => "#4C6FAF"
    };

    public string IconBrush => Severity switch
    {
        ToastSeverity.Success => "#7CE0B0",
        ToastSeverity.Warning => "#FFD17A",
        ToastSeverity.Error => "#FF9696",
        _ => "#9DC4FF"
    };
}
