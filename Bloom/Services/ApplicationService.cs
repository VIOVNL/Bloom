using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;

namespace Bloom.Services;

internal sealed class ApplicationService : IApplicationService
{
    public void SetThemeVariant(bool isDark)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
