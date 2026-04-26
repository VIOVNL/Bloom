using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Bloom.Helpers;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

public partial class AddGroupWindow : Window
{
    private string _searchText = "";

    public AddGroupWindow()
    {
        InitializeComponent();
        WireChooseIconButton();
        WireActionButtons();
        WireDrag();
        WireSearch();
        DataContextChanged += OnDataContextReady;
    }

    private void OnDataContextReady(object? sender, EventArgs e)
    {
        DataContextChanged -= OnDataContextReady;
        if (DataContext is not AddGroupViewModel vm) return;

        LucideGeometryCache.EnsureBuilt();
        RebuildLists();
        UpdateIconMiniPreview();
        UpdatePreview();
    }

    private void WireSearch()
    {
        SearchBox.TextChanged += (_, _) =>
        {
            _searchText = SearchBox.Text?.Trim() ?? "";
            RebuildLists();
        };
    }

    // ── Window drag ──────────────────────────────────────
    private void WireDrag()
    {
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
    }

    // ── Icon chooser dialog ──────────────────────────────
    private void WireChooseIconButton()
    {
        ChooseIconBtn.PointerPressed += (_, e) => { e.Handled = true; OnChooseIconAsync().FireAndForget(); };
        KeyboardHelper.WireActivate(ChooseIconBtn, () => OnChooseIconAsync().FireAndForget());
    }

    private async Task OnChooseIconAsync()
    {
        if (DataContext is not AddGroupViewModel vm) return;

        var chooserVm = new IconChooserViewModel
        {
            SelectedIconKey = vm.SelectedBuiltInIconKey,
            SelectedColor = vm.SelectedColor,
        };

        var chooser = new IconChooserWindow { DataContext = chooserVm };
        await chooser.ShowDialog<bool?>(this);

        if (chooserVm.Confirmed)
        {
            vm.SelectedBuiltInIconKey = chooserVm.SelectedIconKey;
            vm.SelectedColor = chooserVm.SelectedColor;
            UpdateIconMiniPreview();
            UpdatePreview();
        }
    }

    private void UpdateIconMiniPreview()
    {
        if (DataContext is not AddGroupViewModel vm) return;

        LucideGeometryCache.EnsureBuilt();
        var canvas = IconPreviewHelper.CreateLucidePreview(vm.SelectedBuiltInIconKey, Color.Parse(vm.SelectedColor));
        if (canvas != null) IconMiniPreviewIcon.Child = canvas;

        IconMiniLabel.Text = LucideIcon.TryFromName(vm.SelectedBuiltInIconKey, out var labelIcon)
            ? labelIcon.Label
            : vm.SelectedBuiltInIconKey;
        IconMiniPreview.Background = ThemeHelper.GetBrush("PreviewCircleBg");
    }

    // ── Action buttons ───────────────────────────────────
    private void WireActionButtons()
    {
        CloseBtn.PointerPressed += (_, e) => { e.Handled = true; Close(false); };
        KeyboardHelper.WireActivate(CloseBtn, () => Close(false));

        CancelBtn.PointerPressed += (_, e) => { e.Handled = true; Close(false); };
        KeyboardHelper.WireActivate(CancelBtn, () => Close(false));

        DeleteBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (DataContext is AddGroupViewModel vm)
            {
                vm.DeleteCommand.Execute(null);
                Close(true);
            }
        };
        KeyboardHelper.WireActivate(DeleteBtn, () =>
        {
            if (DataContext is AddGroupViewModel vm)
            {
                vm.DeleteCommand.Execute(null);
                Close(true);
            }
        });

        AddBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ConfirmAndClose();
        };
        KeyboardHelper.WireActivate(AddBtn, ConfirmAndClose);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(false);
        };
    }

    private void ConfirmAndClose()
    {
        if (DataContext is AddGroupViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            if (vm.Confirmed) Close(true);
        }
    }

    // ── Dual item lists ──────────────────────────────────

    private void RebuildLists()
    {
        AvailableList.Children.Clear();
        SelectedList.Children.Clear();

        if (DataContext is not AddGroupViewModel vm) return;

        bool hasFilter = !string.IsNullOrEmpty(_searchText);

        foreach (var selectable in vm.AvailableItems)
        {
            if (hasFilter && selectable.Item.Label.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var row = CreateItemRow(selectable.Item);
            var capturedSelectable = selectable;

            row.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                capturedSelectable.IsSelected = !capturedSelectable.IsSelected;
                RebuildLists();
            };

            if (selectable.IsSelected)
                SelectedList.Children.Add(row);
            else
                AvailableList.Children.Add(row);
        }

        if (AvailableList.Children.Count == 0)
            AvailableList.Children.Add(CreateEmptyLabel(hasFilter ? "No matches" : "All items added"));

        if (SelectedList.Children.Count == 0)
            SelectedList.Children.Add(CreateEmptyLabel(hasFilter ? "No matches" : "No items yet"));
    }

    private static TextBlock CreateEmptyLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPlaceholder")),
        Margin = new Thickness(10, 12),
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private static Border CreateItemRow(BloomItem item)
    {
        var iconControl = CreateItemIcon(item);

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 12,
            Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextSecondary")),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (iconControl != null) row.Children.Add(iconControl);
        row.Children.Add(label);

        return new Border { Classes = { "listItem" }, Child = row };
    }

    private static Control? CreateItemIcon(BloomItem item)
    {
        const double iconSize = 16;

        // Bitmap icon (auto-extracted from exe)
        if (item.IconSource == IconSource.Auto && !string.IsNullOrEmpty(item.AutoIconData))
        {
            var img = IconPreviewHelper.CreateBitmapPreview(item.AutoIconData, iconSize);
            if (img != null)
                return new Viewbox
                {
                    Width = iconSize, Height = iconSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = img
                };
        }

        // User-selected file icon
        if (item.IconSource == IconSource.File && !string.IsNullOrEmpty(item.FileIconData))
        {
            var img = IconPreviewHelper.CreateBitmapPreview(item.FileIconData, iconSize);
            if (img != null)
                return new Viewbox
                {
                    Width = iconSize, Height = iconSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = img
                };
        }

        // Built-in Lucide icon
        if (!string.IsNullOrEmpty(item.BuiltInIconKey))
        {
            var iconColor = string.IsNullOrEmpty(item.IconColor) ? "#FFFFFF" : item.IconColor;
            return LucideGeometryCache.CreateIcon(
                item.BuiltInIconKey,
                new SolidColorBrush(Color.Parse(iconColor)),
                iconSize);
        }

        // Fallback: first letter
        if (!string.IsNullOrEmpty(item.Label))
        {
            return new TextBlock
            {
                Text = item.Label[..1].ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(
                    string.IsNullOrEmpty(item.IconColor) ? "#FFFFFF" : item.IconColor)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = iconSize,
                TextAlignment = TextAlignment.Center
            };
        }

        return null;
    }

    // ── Live preview ─────────────────────────────────────
    private void UpdatePreview()
    {
        if (DataContext is not AddGroupViewModel vm) return;
        PreviewCircle.Background = ThemeHelper.GetBrush("PreviewCircleBg");

        var canvas = IconPreviewHelper.CreateLucidePreview(vm.SelectedBuiltInIconKey, Color.Parse(vm.SelectedColor));
        if (canvas != null) PreviewIcon.Child = canvas;
    }
}
