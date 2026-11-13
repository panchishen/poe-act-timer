namespace PoeActTimer.Core;

public sealed class AppConfig
{
    public string? LogPath { get; set; }
    public int AfkSeconds { get; set; } = 20;
    public string HotkeyPause { get; set; } = "Ctrl+Alt+P";
    public string HotkeyToggle { get; set; } = "Ctrl+Alt+E";
    public string HotkeyReset { get; set; } = "Ctrl+Alt+R";
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }
    public double Opacity { get; set; } = 0.68;
    public string? Language { get; set; }
    public bool Expanded { get; set; }
}
