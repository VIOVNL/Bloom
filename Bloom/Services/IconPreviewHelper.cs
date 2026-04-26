using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bloom.Models;

namespace Bloom.Services;

internal static class IconPreviewHelper
{
    /// <summary>Creates a Lucide icon Canvas with stroke rendering.</summary>
    internal static Canvas? CreateLucidePreview(string iconKey, Color color, double strokeThickness = 2)
    {
        if (!LucideIcon.TryFromName(iconKey, out var icon)) return null;
        if (!LucideGeometryCache.TryGet(icon.Name, out var geometry) || geometry == null) return null;

        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = strokeThickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return canvas;
    }

    /// <summary>Creates a bitmap preview Image from base64 data.</summary>
    internal static Image? CreateBitmapPreview(string base64, double size = 30)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            return new Image
            {
                Source = bitmap,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Failed to create bitmap preview from base64");
            return null;
        }
    }
}
