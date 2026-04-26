using System.Collections.Generic;

namespace Bloom.Models;

public class BloomConfig
{
    public List<BloomItem> Items { get; set; } = new();
    public BloomSettings Settings { get; set; } = new();
}
