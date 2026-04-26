using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Helpers;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

public partial class SettingsWindow : Window
{
    private Border _previewHost = null!;

    private static readonly LabelMode[] LabelModes =
        { LabelMode.Below, LabelMode.Tooltip, LabelMode.Overlay, LabelMode.Hidden };

    private static readonly AppScale[] ScaleModes =
        { AppScale.Small, AppScale.Medium, AppScale.Large, AppScale.ExtraLarge };

    public SettingsWindow()
    {
        InitializeComponent();

        _previewHost = this.FindControl<Border>("LabelPreviewHost")!;

        // Close
        var closeBtn = this.FindControl<Border>("CloseBtn")!;
        closeBtn.PointerPressed += (_, _) => Close();
        KeyboardHelper.WireActivate(closeBtn, Close);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };

        // Theme toggle
        var themeToggle = this.FindControl<Border>("ThemeToggle")!;
        themeToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as SettingsViewModel)?.ToggleThemeCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(themeToggle, () =>
            (DataContext as SettingsViewModel)?.ToggleThemeCommand.Execute(null));

        // Label mode segments
        var labelSegments = new[]
        {
            this.FindControl<Border>("LabelBelow")!,
            this.FindControl<Border>("LabelTooltip")!,
            this.FindControl<Border>("LabelOverlay")!,
            this.FindControl<Border>("LabelHidden")!,
        };
        for (int i = 0; i < labelSegments.Length; i++)
        {
            int idx = i;
            labelSegments[i].PointerPressed += (_, e) =>
            {
                e.Handled = true;
                if (DataContext is SettingsViewModel vm)
                    vm.SelectLabelModeCommand.Execute(LabelModes[idx].ToString());
            };
            KeyboardHelper.WireActivate(labelSegments[i], () =>
            {
                if (DataContext is SettingsViewModel vm)
                    vm.SelectLabelModeCommand.Execute(LabelModes[idx].ToString());
            });
        }

        // Startup toggle
        var startupToggle = this.FindControl<Border>("StartupToggle")!;
        startupToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as SettingsViewModel)?.ToggleStartupCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(startupToggle, () =>
            (DataContext as SettingsViewModel)?.ToggleStartupCommand.Execute(null));

        // Auto update toggle
        var autoUpdateToggle = this.FindControl<Border>("AutoUpdateToggle")!;
        autoUpdateToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as SettingsViewModel)?.ToggleAutoUpdateCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(autoUpdateToggle, () =>
            (DataContext as SettingsViewModel)?.ToggleAutoUpdateCommand.Execute(null));

        // UnBloom on focus loss toggle
        var unBloomToggle = this.FindControl<Border>("UnBloomToggle")!;
        unBloomToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as SettingsViewModel)?.ToggleUnBloomOnFocusLossCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(unBloomToggle, () =>
            (DataContext as SettingsViewModel)?.ToggleUnBloomOnFocusLossCommand.Execute(null));

        // Always on Top toggle
        var alwaysOnTopToggle = this.FindControl<Border>("AlwaysOnTopToggle")!;
        alwaysOnTopToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as SettingsViewModel)?.ToggleAlwaysOnTopCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(alwaysOnTopToggle, () =>
            (DataContext as SettingsViewModel)?.ToggleAlwaysOnTopCommand.Execute(null));

        // Scale segments
        var scaleSegments = new[]
        {
            this.FindControl<Border>("ScaleSmall")!,
            this.FindControl<Border>("ScaleMedium")!,
            this.FindControl<Border>("ScaleLarge")!,
            this.FindControl<Border>("ScaleExtraLarge")!,
        };
        for (int i = 0; i < scaleSegments.Length; i++)
        {
            int idx = i;
            scaleSegments[i].PointerPressed += (_, e) =>
            {
                e.Handled = true;
                if (DataContext is SettingsViewModel vm)
                    vm.SelectScaleCommand.Execute(ScaleModes[idx].ToString());
            };
            KeyboardHelper.WireActivate(scaleSegments[i], () =>
            {
                if (DataContext is SettingsViewModel vm)
                    vm.SelectScaleCommand.Execute(ScaleModes[idx].ToString());
            });
        }

        // Manage Hotkeys button
        var manageHotkeysBtn = this.FindControl<Border>("ManageHotkeysBtn")!;
        manageHotkeysBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Close();
            WeakReferenceMessenger.Default.Send(new HotkeyWindowRequestedMessage());
        };
        KeyboardHelper.WireActivate(manageHotkeysBtn, () =>
        {
            Close();
            WeakReferenceMessenger.Default.Send(new HotkeyWindowRequestedMessage());
        });

        // Drag
        if (closeBtn.Parent is Grid grid && grid.Parent is StackPanel stack && stack.Parent is Border border)
            border.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

        // Update label preview when LabelMode changes or DataContext is set
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                UpdateLabelPreview((int)vm.LabelMode);
                vm.PropertyChanged += OnSettingsVmPropertyChanged;
                Closed += (_, _) => vm.PropertyChanged -= OnSettingsVmPropertyChanged;
            }
        };
    }

    private void OnSettingsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.LabelMode) && sender is SettingsViewModel vm)
            UpdateLabelPreview((int)vm.LabelMode);
    }

    // --- Label mode preview (pure visual) ---

    private static readonly string[] PreviewLabels = { "App", "Music", "Web" };
    private static readonly string[] PreviewColors = { "#339AF0", "#51CF66", "#FF6B6B" };

    private void UpdateLabelPreview(int modeIdx)
    {
        _previewHost.Child = null;
        var mode = LabelModes[modeIdx < 0 || modeIdx >= LabelModes.Length ? 0 : modeIdx];

        var container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 20
        };

        for (int i = 0; i < 3; i++)
            container.Children.Add(CreateMockPetal(PreviewLabels[i], PreviewColors[i], mode));

        _previewHost.Child = container;
    }

    private static Control CreateMockPetal(string label, string color, LabelMode mode)
    {
        var glassBg = new SolidColorBrush(ThemeHelper.GetColor("PetalGlassBg"));
        var glassBorder = new SolidColorBrush(ThemeHelper.GetColor("PetalBorderStart"));
        var labelFg = new SolidColorBrush(ThemeHelper.GetColor("LabelTextColor"));
        var labelBg = new SolidColorBrush(ThemeHelper.GetColor("LabelBgColor"));
        var labelBorderBrush = new SolidColorBrush(ThemeHelper.GetColor("LabelBorderColor"));

        Border MakeCircle(Control? child = null) => new()
        {
            Width = 30, Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = glassBg,
            BorderThickness = new Thickness(1),
            BorderBrush = glassBorder,
            Child = child ?? new Border
            {
                Width = 11, Height = 11,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(color)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        TextBlock MakeLabel(double size = 8.5) => new()
        {
            Text = label, FontSize = size,
            Foreground = labelFg,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        switch (mode)
        {
            case LabelMode.Below:
            {
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 3 };
                stack.Children.Add(MakeCircle());
                stack.Children.Add(MakeLabel());
                return stack;
            }
            case LabelMode.Tooltip:
            {
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
                var pill = new Border
                {
                    Background = labelBg, CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2), BorderThickness = new Thickness(1),
                    BorderBrush = labelBorderBrush, HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = label, FontSize = 8, Foreground = labelFg,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                stack.Children.Add(pill);
                stack.Children.Add(MakeCircle());
                return stack;
            }
            case LabelMode.Overlay:
            {
                var content = new Grid();
                content.Children.Add(new Border
                {
                    Width = 11, Height = 11, CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.Parse(color)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                content.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#80000000")),
                    VerticalAlignment = VerticalAlignment.Bottom, Height = 12,
                    Child = new TextBlock
                    {
                        Text = label, FontSize = 7, Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
                return MakeCircle(content);
            }
            default:
                return MakeCircle();
        }
    }
}
