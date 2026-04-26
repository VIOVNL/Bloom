namespace Bloom.Models;

public class PetalItem
{
    public string Label { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string IconColor { get; set; } = "#FFFFFF";
    public string ProcessName { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public ShortcutType ShortcutType { get; set; } = ShortcutType.Software;
    public string? BitmapIconBase64 { get; set; }
    public BloomItem? SourceGroup { get; set; }
    public string? SourceItemId { get; set; }
}
