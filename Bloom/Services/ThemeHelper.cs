using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Bloom.Services;

public static class ThemeHelper
{
    public static Color GetColor(string resourceKey)
    {
        var app = Application.Current;
        if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var value)
            && value is Color color)
            return color;
        return Colors.Transparent;
    }

    public static SolidColorBrush GetBrush(string resourceKey)
    {
        return new SolidColorBrush(GetColor(resourceKey));
    }
}
