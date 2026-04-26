using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Bloom.Services;

internal sealed class ToastService : IDisposable
{
    private readonly Window _owner;
    private Window? _toastWindow;
    private CancellationTokenSource? _toastCts;

    public ToastService(Window owner)
    {
        _owner = owner;
    }

    public void Show(string message)
    {
        _toastCts?.Cancel();
        try { _toastWindow?.Close(); } catch { }

        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = ThemeHelper.GetBrush("LabelTextColor"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.3, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.7, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ThemeHelper.GetColor("BloomButtonBgStart"), 0),
                    new GradientStop(ThemeHelper.GetColor("BloomButtonBgEnd"), 1)
                }
            },
            BorderThickness = new Thickness(1),
            BorderBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ThemeHelper.GetColor("BloomButtonBorderStart"), 0),
                    new GradientStop(ThemeHelper.GetColor("BloomButtonBorderEnd"), 1)
                }
            },
            Child = textBlock
        };

        var toastWin = new Window
        {
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = Brushes.Transparent,
            Topmost = true,
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = border,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        var screen = _owner.Screens.ScreenFromVisual(_owner) ?? _owner.Screens.Primary;
        double scaling = screen?.Scaling ?? 1.0;

        toastWin.Opened += (_, _) =>
        {
            var toastWidth = toastWin.Bounds.Width * scaling;
            var bloomCenter = _owner.Position.X + (int)(_owner.Width * scaling / 2);
            toastWin.Position = new PixelPoint(
                (int)(bloomCenter - toastWidth / 2),
                _owner.Position.Y - (int)(40 * scaling));
        };

        _toastWindow = toastWin;
        toastWin.Show();

        Task.Delay(2000, token).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try { toastWin.Close(); } catch { }
                if (_toastWindow == toastWin)
                    _toastWindow = null;
            });
        }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    public void Dispose()
    {
        _toastCts?.Cancel();
        try { _toastWindow?.Close(); } catch { }
    }
}
