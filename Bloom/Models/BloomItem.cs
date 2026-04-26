using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bloom.Models;

public class BloomItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = "";
    public ShortcutType Type { get; set; } = ShortcutType.Software;
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public IconSource IconSource { get; set; } = IconSource.BuiltIn;
    public string BuiltInIconKey { get; set; } = "";
    public string? AutoIconData { get; set; }
    public string? FileIconData { get; set; }
    public string IconColor { get; set; } = "#FFFFFF";
    public List<string> ChildIds { get; set; } = new();
    public string? Hotkey { get; set; }

    [JsonIgnore]
    public bool IsInGroup { get; set; }
}
