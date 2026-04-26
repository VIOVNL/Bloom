namespace Bloom.Models;

public enum AppTheme
{
    Dark,
    Light
}

public enum LabelMode
{
    Below,
    Tooltip,
    Overlay,
    Hidden
}

public enum AppScale
{
    Small,
    Medium,
    Large,
    ExtraLarge
}

public class BloomSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public LabelMode LabelMode { get; set; } = LabelMode.Tooltip;
    public bool StartWithWindows { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public bool UnBloomOnFocusLoss { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool FirstLaunch { get; set; } = true;
    public AppScale Scale { get; set; } = AppScale.Medium;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public string? LastSeenVersion { get; set; }
    public string? PetalsHotkey { get; set; }
    public string? BloomHotkey { get; set; }
    public bool ShowBloomAtCursor { get; set; }
}
