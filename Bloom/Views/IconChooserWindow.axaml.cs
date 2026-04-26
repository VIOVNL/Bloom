using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Bloom.Helpers;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

public partial class IconChooserWindow : Window
{
    private Border? _selectedIconBorder;
    private Border? _selectedColorBorder;
    private int _loadToken;

    private const int IconsPerRow = 12;
    private StackPanel? _currentRow;
    private int _colIndex;

    public IconChooserWindow()
    {
        InitializeComponent();
        WireButtons();
        WireDrag();
        DataContextChanged += OnDataContextReady;
    }

    private void OnDataContextReady(object? sender, EventArgs e)
    {
        DataContextChanged -= OnDataContextReady;
        if (DataContext is not IconChooserViewModel vm) return;

        LucideGeometryCache.EnsureBuilt();
        WireIconSearch(vm);
        WireHexInput(vm);
        WirePageButtons(vm);
        BuildIconGrid(vm);
        BuildColorGrid(vm);
        UpdatePreview(vm);
        Closed += (_, _) => vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not IconChooserViewModel vm) return;
        if (e.PropertyName == nameof(IconChooserViewModel.IconSearchText))
            BuildIconGridFiltered(vm.IconSearchText, vm);
        else if (e.PropertyName == nameof(IconChooserViewModel.CurrentPage))
            RebuildCurrentPage(vm);
    }

    // ── Buttons ───────────────────────────────────────────
    private void WireButtons()
    {
        CloseBtn.PointerPressed += (_, e) => { e.Handled = true; Close(false); };
        KeyboardHelper.WireActivate(CloseBtn, () => Close(false));

        CancelBtn.PointerPressed += (_, e) => { e.Handled = true; Close(false); };
        KeyboardHelper.WireActivate(CancelBtn, () => Close(false));

        SelectBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ConfirmAndClose();
        };
        KeyboardHelper.WireActivate(SelectBtn, ConfirmAndClose);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(false);
        };
    }

    private void ConfirmAndClose()
    {
        if (DataContext is IconChooserViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            Close(true);
        }
    }

    // ── Window drag ───────────────────────────────────────
    private void WireDrag()
    {
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
    }

    private void ClearFocus() => FocusManager?.ClearFocus();

    // ── Page buttons ────────────────────────────────────────
    private void WirePageButtons(IconChooserViewModel vm)
    {
        var prevBtn = this.FindControl<Border>("PrevPageBtn")!;
        var nextBtn = this.FindControl<Border>("NextPageBtn")!;

        prevBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (vm.CanGoBack) vm.PreviousPageCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(prevBtn, () => { if (vm.CanGoBack) vm.PreviousPageCommand.Execute(null); });

        nextBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (vm.CanGoForward) vm.NextPageCommand.Execute(null);
        };
        KeyboardHelper.WireActivate(nextBtn, () => { if (vm.CanGoForward) vm.NextPageCommand.Execute(null); });
    }

    // ── Icon search ───────────────────────────────────────
    private void WireIconSearch(IconChooserViewModel vm)
    {
        IconSearchBox.TextChanged += (_, _) =>
        {
            vm.IconSearchText = IconSearchBox.Text?.Trim() ?? "";
        };

        vm.PropertyChanged += OnVmPropertyChanged;
    }

    // ── Hex color input ──────────────────────────────────
    private bool _updatingHex;
    private bool _updatingSpectrum;

    private void WireHexInput(IconChooserViewModel vm)
    {
        HexInput.Text = vm.SelectedColor;
        SyncSpectrum(vm.SelectedColor);

        HexInput.TextChanged += (_, _) => ApplyHexInput(vm);

        SpectrumPicker.ColorChanged += (_, e) =>
        {
            if (_updatingSpectrum) return;

            var color = e.NewColor;
            var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            vm.SelectColorCommand.Execute(hex);
            SyncHexInput(hex);

            if (_selectedColorBorder != null)
            {
                _selectedColorBorder.Classes.Remove("selected");
                _selectedColorBorder = null;
            }

            UpdateIconGridColor(hex);
            UpdatePreview(vm);
        };
    }

    private void ApplyHexInput(IconChooserViewModel vm)
    {
        if (_updatingHex) return;

        var hex = HexInput.Text?.Trim() ?? "";
        if (!hex.StartsWith('#')) hex = "#" + hex;
        if (hex.Length != 7) return;
        try { Color.Parse(hex); } catch { return; }

        vm.SelectColorCommand.Execute(hex);

        if (_selectedColorBorder != null)
        {
            _selectedColorBorder.Classes.Remove("selected");
            _selectedColorBorder = null;
        }

        foreach (var child in ColorGrid.Children)
        {
            if (child is Border b && b.Tag is string tag &&
                string.Equals(tag, hex, StringComparison.OrdinalIgnoreCase))
            {
                b.Classes.Add("selected");
                _selectedColorBorder = b;
                break;
            }
        }

        SyncSpectrum(hex);
        UpdateIconGridColor(hex);
        UpdatePreview(vm);
    }

    private void SyncHexInput(string hex)
    {
        _updatingHex = true;
        HexInput.Text = hex;
        _updatingHex = false;
    }

    private void SyncSpectrum(string hex)
    {
        _updatingSpectrum = true;
        try
        {
            var c = Color.Parse(hex);
            SpectrumPicker.HsvColor = new HsvColor(c);
        }
        catch { }
        _updatingSpectrum = false;
    }

    // ── Icon grid ─────────────────────────────────────────
    private void BuildIconGrid(IconChooserViewModel vm) => BuildIconGridFiltered("", vm);

    private void BuildIconGridFiltered(string search, IconChooserViewModel vm)
    {
        var pageIcons = vm.UpdateFilteredIcons(search);
        RenderPageIcons(pageIcons, vm);
    }

    private void RebuildCurrentPage(IconChooserViewModel vm)
    {
        var pageIcons = vm.GetCurrentPageIcons();
        RenderPageIcons(pageIcons, vm);
    }

    private void RenderPageIcons(List<LucideIcon> icons, IconChooserViewModel vm)
    {
        _loadToken++;
        IconGrid.Children.Clear();
        _selectedIconBorder = null;
        _currentRow = null;
        _colIndex = 0;

        if (icons.Count == 0)
        {
            IconGrid.Children.Add(new TextBlock
            {
                Text = "No icons found",
                FontSize = 11,
                Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextDisabled")),
                Margin = new Thickness(10),
            });
            return;
        }

        var iconColor = new SolidColorBrush(Color.Parse(vm.SelectedColor));
        foreach (var lucide in icons)
            AddIconToGrid(lucide, iconColor, vm);

        IconScrollViewer.Offset = new Vector(0, 0);
    }

    private void AddIconToGrid(LucideIcon lucide, IBrush iconColor, IconChooserViewModel vm)
    {
        if (!LucideGeometryCache.TryGet(lucide.Name, out var geometry) || geometry == null)
            return;

        var path = new Path
        {
            Data = geometry,
            Stroke = iconColor,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        var icon = new Viewbox { Width = 16, Height = 16, Child = canvas };

        var border = new Border
        {
            Classes = { "iconItem" },
            Child = icon,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(border, lucide.Label);
        border.Tag = lucide.Name;

        var capturedKey = lucide.Name;
        var capturedBorder = border;
        border.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ClearFocus();
            if (_selectedIconBorder != null)
                _selectedIconBorder.Classes.Remove("selected");
            capturedBorder.Classes.Add("selected");
            _selectedIconBorder = capturedBorder;
            vm.SelectIconCommand.Execute(capturedKey);
            UpdatePreview(vm);
        };

        if (lucide.Name == vm.SelectedIconKey)
        {
            border.Classes.Add("selected");
            _selectedIconBorder = border;
        }

        if (_colIndex % IconsPerRow == 0)
        {
            _currentRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            IconGrid.Children.Add(_currentRow);
        }
        _currentRow!.Children.Add(border);
        _colIndex++;
    }

    // ── Color grid ────────────────────────────────────────
    private void BuildColorGrid(IconChooserViewModel vm)
    {
        ColorGrid.Children.Clear();

        foreach (var hex in vm.AvailableColors)
        {
            var border = new Border
            {
                Classes = { "colorDot" },
                Background = new SolidColorBrush(Color.Parse(hex)),
                Tag = hex
            };

            var capturedHex = hex;
            var capturedBorder = border;
            border.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                ClearFocus();
                if (_selectedColorBorder != null)
                    _selectedColorBorder.Classes.Remove("selected");
                capturedBorder.Classes.Add("selected");
                _selectedColorBorder = capturedBorder;
                vm.SelectColorCommand.Execute(capturedHex);
                SyncHexInput(capturedHex);
                SyncSpectrum(capturedHex);
                UpdateIconGridColor(capturedHex);
                UpdatePreview(vm);
            };

            if (hex == vm.SelectedColor)
            {
                border.Classes.Add("selected");
                _selectedColorBorder = border;
            }

            ColorGrid.Children.Add(border);
        }
    }

    private void UpdateIconGridColor(string hex)
    {
        var brush = new SolidColorBrush(Color.Parse(hex));
        foreach (var row in IconGrid.Children)
        {
            if (row is not StackPanel sp) continue;
            foreach (var child in sp.Children)
            {
                if (child is Border b && b.Child is Viewbox vb
                    && vb.Child is Canvas c && c.Children.Count > 0
                    && c.Children[0] is Path p)
                    p.Stroke = brush;
            }
        }
    }

    // ── Preview ───────────────────────────────────────────
    private void UpdatePreview(IconChooserViewModel vm)
    {
        PreviewCircle.Background = ThemeHelper.GetBrush("PreviewCircleBg");
        var canvas = IconPreviewHelper.CreateLucidePreview(vm.SelectedIconKey, Color.Parse(vm.SelectedColor));
        if (canvas != null) PreviewIcon.Child = canvas;
    }
}
