using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Bloom.Models;

namespace Bloom.Services;

/// <summary>
/// Shared geometry cache for Lucide icons. Parse once, reuse everywhere.
/// </summary>
public static class LucideGeometryCache
{
    private static readonly Dictionary<string, Geometry?> _cache = new();
    private static readonly object _lock = new();
    private static bool _built;

    public static void EnsureBuilt()
    {
        if (_built) return;
        lock (_lock)
        {
            if (_built) return;
            foreach (var icon in LucideIcon.List)
            {
                try { _cache[icon.Name] = Geometry.Parse(icon.PathData); }
                catch { _cache[icon.Name] = null; }
            }
            _built = true;
        }
    }

    public static bool TryGet(string name, out Geometry? geometry)
        => _cache.TryGetValue(name, out geometry);

    public static Viewbox CreateIcon(string iconName, IBrush stroke, double size)
    {
        EnsureBuilt();
        if (!_cache.TryGetValue(iconName, out var geometry) || geometry == null)
            geometry = Geometry.Parse(LucideIcon.FromName(iconName).PathData);

        var path = new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }
}
