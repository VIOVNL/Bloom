using System;
using System.Linq;
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

public partial class HotkeyWindow : Window
{
    private Border _petalsHotkeyBtn = null!;
    private TextBlock _petalsHotkeyBtnText = null!;
    private Border _bloomHotkeyBtn = null!;
    private TextBlock _bloomHotkeyBtnText = null!;

    private Border? _activeRecorderBtn;
    private TextBlock? _activeRecorderText;
    private Action<string?>? _activeRecorderSetter;
    private string? _activeRecorderItemId; // null for global hotkeys

    public HotkeyWindow()
    {
        InitializeComponent();

        // Close
        var closeBtn = this.FindControl<Border>("CloseBtn")!;
        closeBtn.PointerPressed += (_, _) => Close();
        KeyboardHelper.WireActivate(closeBtn, Close);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _activeRecorderBtn == null)
                Close();
        };

        // Global hotkey buttons
        _petalsHotkeyBtn = this.FindControl<Border>("PetalsHotkeyBtn")!;
        _petalsHotkeyBtnText = this.FindControl<TextBlock>("PetalsHotkeyBtnText")!;
        _bloomHotkeyBtn = this.FindControl<Border>("BloomHotkeyBtn")!;
        _bloomHotkeyBtnText = this.FindControl<TextBlock>("BloomHotkeyBtnText")!;

        WireSingleHotkeyBtn(_petalsHotkeyBtn, _petalsHotkeyBtnText, null,
            v => (DataContext as HotkeyViewModel)?.SetPetalsHotkey(v));
        WireSingleHotkeyBtn(_bloomHotkeyBtn, _bloomHotkeyBtnText, null,
            v => (DataContext as HotkeyViewModel)?.SetBloomHotkey(v));

        // Global clear buttons
        WireClearBtn(this.FindControl<Border>("PetalsClearBtn")!, _petalsHotkeyBtnText,
            () => (DataContext as HotkeyViewModel)?.SetPetalsHotkey(null));
        WireClearBtn(this.FindControl<Border>("BloomClearBtn")!, _bloomHotkeyBtnText,
            () => (DataContext as HotkeyViewModel)?.SetBloomHotkey(null));

        // Show Bloom at Cursor toggle
        var bloomAtCursorToggle = this.FindControl<Border>("BloomAtCursorToggle")!;
        bloomAtCursorToggle.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            (DataContext as HotkeyViewModel)?.ToggleShowBloomAtCursor();
        };
        KeyboardHelper.WireActivate(bloomAtCursorToggle, () =>
            (DataContext as HotkeyViewModel)?.ToggleShowBloomAtCursor());

        // Drag
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        DataContextChanged += (_, _) =>
        {
            if (DataContext is HotkeyViewModel vm)
            {
                UpdateGlobalHotkeyTexts();
                vm.PropertyChanged += OnVmPropertyChanged;
                Closed += (_, _) => vm.PropertyChanged -= OnVmPropertyChanged;
                LucideGeometryCache.EnsureBuilt();
                BuildItemTree();
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HotkeyViewModel.PetalsHotkeyDescription) or nameof(HotkeyViewModel.BloomHotkeyDescription))
            UpdateGlobalHotkeyTexts();
    }

    // ── Global hotkey button texts ────────────────────────

    private void UpdateGlobalHotkeyTexts()
    {
        if (_activeRecorderBtn != null) return;
        if (DataContext is not HotkeyViewModel vm) return;

        UpdateSingleHotkeyText(_petalsHotkeyBtnText, vm.PetalsHotkeyDescription);
        UpdateSingleHotkeyText(_bloomHotkeyBtnText, vm.BloomHotkeyDescription);
    }

    private static void UpdateSingleHotkeyText(TextBlock btnText, string desc)
    {
        var isSet = !string.IsNullOrEmpty(desc) && desc != "Not set";
        btnText.Text = isSet ? desc : "Not set";
        btnText.Foreground = new SolidColorBrush(
            ThemeHelper.GetColor(isSet ? "TextSecondary" : "TextTertiary"));
    }

    // ── Hotkey recorder (shared for global + item hotkeys) ──

    private void WireSingleHotkeyBtn(Border btn, TextBlock btnText, string? itemId, Action<string?> setter)
    {
        btn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (e.GetCurrentPoint(btn).Properties.IsRightButtonPressed)
            {
                setter(null);
                UpdateSingleHotkeyText(btnText, "Not set");
                StopRecording();
                return;
            }
            _activeRecorderItemId = itemId;
            StartRecording(btn, btnText, setter);
        };
        KeyboardHelper.WireActivate(btn, () =>
        {
            _activeRecorderItemId = itemId;
            StartRecording(btn, btnText, setter);
        });
    }

    private void WireClearBtn(Border clearBtn, TextBlock btnText, Action clear)
    {
        clearBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            StopRecording();
            clear();
            UpdateSingleHotkeyText(btnText, "Not set");
        };
        KeyboardHelper.WireActivate(clearBtn, () =>
        {
            StopRecording();
            clear();
            UpdateSingleHotkeyText(btnText, "Not set");
        });
    }

    private static Border CreateClearButton()
    {
        var xPath = new Avalonia.Controls.Shapes.Path
        {
            Stroke = new SolidColorBrush(ThemeHelper.GetColor("TextTertiary")),
            StrokeThickness = 2.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Data = Avalonia.Media.Geometry.Parse("M18 6 6 18 M6 6l12 12")
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(xPath);
        return new Border
        {
            Classes = { "clearBtn" },
            Focusable = true,
            Margin = new Thickness(4, 0, 0, 0),
            Child = new Viewbox { Width = 10, Height = 10, Child = canvas }
        };
    }

    private void StartRecording(Border btn, TextBlock btnText, Action<string?> setter)
    {
        StopRecording();
        _activeRecorderBtn = btn;
        _activeRecorderText = btnText;
        _activeRecorderSetter = setter;
        btn.Classes.Add("recording");
        btnText.Text = "Press keys...";
        btnText.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#339AF0"));
        KeyDown -= OnHotkeyKeyDown;
        KeyDown += OnHotkeyKeyDown;
    }

    private void StopRecording()
    {
        if (_activeRecorderBtn != null)
            _activeRecorderBtn.Classes.Remove("recording");
        _activeRecorderBtn = null;
        _activeRecorderText = null;
        _activeRecorderSetter = null;
        _activeRecorderItemId = null;
        KeyDown -= OnHotkeyKeyDown;
        UpdateGlobalHotkeyTexts();
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (_activeRecorderBtn == null) return;
        e.Handled = true;

        if (e.Key == Key.Escape) { StopRecording(); return; }

        // Ignore modifier-only presses
        if (e.Key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        uint mod = 0;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))     mod |= HotkeyService.MOD_ALT;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) mod |= HotkeyService.MOD_CTRL;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))   mod |= HotkeyService.MOD_SHIFT;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))    mod |= HotkeyService.MOD_WIN;
        if (mod == 0) return;

        uint vk = MapAvaloniaKeyToVk(e.Key);
        if (vk == 0) return;

        var storageString = HotkeyService.Format(mod, vk).Replace(" + ", "+");

        // Conflict check
        if (DataContext is HotkeyViewModel vm)
        {
            var conflict = vm.FindConflict(storageString, _activeRecorderItemId);
            if (conflict != null)
            {
                ShowConflict(conflict);
                return;
            }
        }

        // Update button text immediately before setter triggers hotkey registration
        // (registration can fire WM_HOTKEY while keys are held, disrupting the update chain)
        var displayText = HotkeyViewModel.FormatHotkeyDisplay(storageString);
        var itemId = _activeRecorderItemId;
        var btnText = _activeRecorderText;
        if (btnText != null) UpdateSingleHotkeyText(btnText, displayText);

        _activeRecorderSetter?.Invoke(storageString);
        StopRecording();
    }

    private async void ShowConflict(string conflictLabel)
    {
        if (_activeRecorderText == null) return;
        var btn = _activeRecorderBtn;
        var text = _activeRecorderText;
        StopRecording();

        text.Text = $"Used by: {conflictLabel}";
        text.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#FF6B6B"));

        await Task.Delay(1500);

        // Reset to current value
        if (btn == _petalsHotkeyBtn) UpdateGlobalHotkeyTexts();
        else if (btn == _bloomHotkeyBtn) UpdateGlobalHotkeyTexts();
        else
        {
            // Item button — find item and restore text
            if (DataContext is HotkeyViewModel vm)
            {
                var desc = HotkeyViewModel.FormatHotkeyDisplay(null);
                // Find the item whose button this was — scan ItemTreeHost
                UpdateSingleHotkeyText(text, text.Tag as string ?? "Not set");
            }
        }
    }

    private void UpdateItemHotkeyText(string itemId, TextBlock btnText)
    {
        if (DataContext is not HotkeyViewModel vm) return;
        var desc = vm.ItemHotkeyDescriptions.TryGetValue(itemId, out var d) ? d : "Not set";
        UpdateSingleHotkeyText(btnText, desc);
    }

    // ── Item tree ─────────────────────────────────────────

    private void BuildItemTree()
    {
        var host = this.FindControl<StackPanel>("ItemTreeHost")!;
        host.Children.Clear();
        if (DataContext is not HotkeyViewModel vm) return;

        var rootItems = vm.Items.Where(i => !i.IsInGroup).ToList();

        if (rootItems.Count == 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = "No items configured",
                FontSize = 11,
                Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPlaceholder")),
                Margin = new Thickness(10, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (var item in rootItems)
        {
            host.Children.Add(CreateItemHotkeyRow(item, vm, false));

            if (item.Type == ShortcutType.Group)
            {
                foreach (var childId in item.ChildIds)
                {
                    var child = vm.Items.FirstOrDefault(i => i.Id == childId);
                    if (child == null) continue;
                    host.Children.Add(CreateItemHotkeyRow(child, vm, true));
                }
            }
        }
    }

    private Border CreateItemHotkeyRow(BloomItem item, HotkeyViewModel vm, bool isChild)
    {
        var iconControl = CreateItemIcon(item);
        var typeLabel = item.Type == ShortcutType.Group ? "Group" : item.Type.ToString();

        var labelText = new TextBlock
        {
            Text = item.Label,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPrimary")),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        };

        var typeText = new TextBlock
        {
            Text = typeLabel,
            FontSize = 9,
            Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextTertiary")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
        labelStack.Children.Add(labelText);
        labelStack.Children.Add(typeText);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (iconControl != null) leftStack.Children.Add(iconControl);
        leftStack.Children.Add(labelStack);

        // Hotkey button
        var desc = vm.ItemHotkeyDescriptions.TryGetValue(item.Id, out var hkDesc) ? hkDesc : "Not set";
        var isSet = !string.IsNullOrEmpty(desc) && desc != "Not set";

        var btnText = new TextBlock
        {
            Text = isSet ? desc : "Not set",
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(ThemeHelper.GetColor(isSet ? "TextSecondary" : "TextTertiary")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = desc // store for conflict reset
        };

        var btn = new Border
        {
            Classes = { "hotkeyBtn" },
            Focusable = true,
            VerticalAlignment = VerticalAlignment.Center,
            Child = btnText
        };

        WireSingleHotkeyBtn(btn, btnText, item.Id, v => vm.SetItemHotkey(item.Id, v));

        // Clear button
        var clearBtn = CreateClearButton();
        WireClearBtn(clearBtn, btnText, () => vm.SetItemHotkey(item.Id, null));

        // Layout grid
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(btn, 1);
        Grid.SetColumn(clearBtn, 2);
        grid.Children.Add(leftStack);
        grid.Children.Add(btn);
        grid.Children.Add(clearBtn);

        var row = new Border
        {
            Classes = { "treeItem" },
            Child = grid,
            Margin = isChild ? new Thickness(20, 0, 0, 0) : new Thickness(0)
        };

        return row;
    }

    private static Control? CreateItemIcon(BloomItem item)
    {
        const double iconSize = 16;

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

        if (!string.IsNullOrEmpty(item.BuiltInIconKey))
        {
            var iconColor = string.IsNullOrEmpty(item.IconColor) ? "#FFFFFF" : item.IconColor;
            return LucideGeometryCache.CreateIcon(
                item.BuiltInIconKey,
                new SolidColorBrush(Color.Parse(iconColor)),
                iconSize);
        }

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

    // ── Key mapping ───────────────────────────────────────

    private static uint MapAvaloniaKeyToVk(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return (uint)('A' + (key - Key.A));

        if (key >= Key.D0 && key <= Key.D9)
            return (uint)('0' + (key - Key.D0));

        if (key >= Key.F1 && key <= Key.F12)
            return (uint)(0x70 + (key - Key.F1));

        return key switch
        {
            Key.Space     => 0x20,
            Key.Enter     => 0x0D,
            Key.Tab       => 0x09,
            Key.Back      => 0x08,
            Key.Delete    => 0x2E,
            Key.Insert    => 0x2D,
            Key.Home      => 0x24,
            Key.End       => 0x23,
            Key.PageUp    => 0x21,
            Key.PageDown  => 0x22,
            Key.Up        => 0x26,
            Key.Down      => 0x28,
            Key.Left      => 0x25,
            Key.Right     => 0x27,
            Key.OemPlus   => 0xBB,
            Key.OemMinus  => 0xBD,
            Key.OemComma  => 0xBC,
            Key.OemPeriod => 0xBE,
            _ => 0
        };
    }
}
