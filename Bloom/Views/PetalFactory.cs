using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;

namespace Bloom.Views;

internal static class PetalFactory
{
    internal static Border CreatePetalElement(PetalItem petal, LabelMode labelMode = LabelMode.Hidden, int index = -1)
    {
        Control iconContent;

        if (!string.IsNullOrEmpty(petal.BitmapIconBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(petal.BitmapIconBase64);
                var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                var iconSz = PetalLayoutEngine.IconSize;
                var image = new Image
                {
                    Source = bitmap,
                    Width = iconSz,
                    Height = iconSz
                };
                var clipBorder = new Border
                {
                    Width = iconSz,
                    Height = iconSz,
                    CornerRadius = new CornerRadius(iconSz / 2),
                    ClipToBounds = true,
                    Child = image
                };
                iconContent = clipBorder;
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Failed to decode bitmap icon for petal");
                iconContent = new Border { Width = PetalLayoutEngine.IconSize, Height = PetalLayoutEngine.IconSize };
            }
        }
        else if (!string.IsNullOrEmpty(petal.IconPath))
        {
            iconContent = CreateLucideIcon(
                petal.IconPath,
                SolidColorBrush.Parse(petal.IconColor),
                PetalLayoutEngine.IconSize);
        }
        else
        {
            iconContent = new TextBlock
            {
                Text = petal.Label.Length > 0 ? petal.Label[..1].ToUpper() : "?",
                FontSize = 16 * PetalLayoutEngine.ScaleFactor,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = SolidColorBrush.Parse(petal.IconColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        Control glassBorderChild;
        if (labelMode == LabelMode.Overlay && !string.IsNullOrEmpty(petal.Label))
        {
            var labelText = new TextBlock
            {
                Text = petal.Label,
                FontSize = 8 * PetalLayoutEngine.ScaleFactor,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = ThemeHelper.GetBrush("LabelTextColor"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 0, 2, 3)
            };
            var overlayGrid = new Grid();
            overlayGrid.Children.Add(iconContent);
            overlayGrid.Children.Add(labelText);
            glassBorderChild = overlayGrid;
        }
        else
        {
            glassBorderChild = iconContent;
        }

        var glassBorder = new Border
        {
            Width = PetalLayoutEngine.PetalSize,
            Height = PetalLayoutEngine.PetalSize,
            CornerRadius = new CornerRadius(PetalLayoutEngine.PetalSize / 2),
            BorderThickness = new Thickness(1),
            BorderBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ThemeHelper.GetColor("PetalBorderStart"), 0),
                    new GradientStop(ThemeHelper.GetColor("PetalBorderEnd"), 1)
                }
            },
            Child = glassBorderChild,
            ClipToBounds = labelMode == LabelMode.Overlay,
            Classes = { "circleVis" }
        };

        var button = new Button
        {
            Classes = { "fabCircle" },
            Width = PetalLayoutEngine.PetalSize,
            Height = PetalLayoutEngine.PetalSize,
            Content = glassBorder
        };
        button.Bind(Button.CommandProperty, new Binding("LaunchByNameCommand"));
        button.CommandParameter = petal;

        if (labelMode == LabelMode.Tooltip && !string.IsNullOrEmpty(petal.Label))
        {
            var tipBlock = new TextBlock
            {
                Text = petal.Label,
                FontSize = 11 * PetalLayoutEngine.ScaleFactor
            };
            ToolTip.SetTip(button, tipBlock);
            ToolTip.SetShowDelay(button, 200);
            ToolTip.SetPlacement(button, PlacementMode.Top);
            ToolTip.SetVerticalOffset(button, -4 * PetalLayoutEngine.ScaleFactor);
        }

        Control wrapperChild;
        if (labelMode == LabelMode.Below && !string.IsNullOrEmpty(petal.Label))
        {
            var belowLabel = new TextBlock
            {
                Text = petal.Label,
                FontSize = 9 * PetalLayoutEngine.ScaleFactor,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = ThemeHelper.GetBrush("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = PetalLayoutEngine.PetalSize + 16 * PetalLayoutEngine.ScaleFactor,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(button);
            stack.Children.Add(belowLabel);
            wrapperChild = stack;
        }
        else
        {
            wrapperChild = button;
        }

        var wrapper = new Border
        {
            Width = PetalLayoutEngine.PetalSize,
            Height = PetalLayoutEngine.PetalSize,
            Background = Brushes.Transparent,
            ClipToBounds = false,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Child = wrapperChild
        };

        if (index >= 0)
        {
            wrapper.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(wrapper).Properties.IsRightButtonPressed)
                {
                    e.Handled = true;
                    WeakReferenceMessenger.Default.Send(new EditItemRequestedMessage(index));
                }
            };
        }

        return wrapper;
    }

    internal static Viewbox CreateLucideIcon(string pathData, IBrush stroke, double size)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    internal static string Fmt(double v) =>
        v.ToString("F1", CultureInfo.InvariantCulture);
}
