using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Bloom.Helpers;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

public partial class AddItemWindow : Window
{
    private Border? _selectedActionBorder;

    public AddItemWindow()
    {
        InitializeComponent();
        WireSegmentedControl();
        WireIconSourceToggle();
        WireChooseIconButton();
        WireActionButtons();
        WireWorkingDirBrowse();
        WirePathTextChanged();
        WireShortcutCapture();
        WireDrag();
        DataContextChanged += OnDataContextReady;
    }

    private void OnDataContextReady(object? sender, EventArgs e)
    {
        DataContextChanged -= OnDataContextReady;
        if (DataContext is not AddItemViewModel vm) return;

        LucideGeometryCache.EnsureBuilt();
        BuildActionGrid();

        var state = vm.RestoreTab(vm.SelectedType);
        ApplyTabVisuals(vm.SelectedType, state, vm);

        // React to ViewModel property changes for View-specific updates
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AddItemViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(AddItemViewModel.HasAutoIcon):
                UpdateIconSourceGrid(vm.HasAutoIcon);
                if (!vm.HasAutoIcon && vm.IconSource == IconSource.Auto)
                    vm.IconSource = IconSource.BuiltIn;
                break;
            case nameof(AddItemViewModel.SelectedBuiltInIconKey):
            case nameof(AddItemViewModel.SelectedColor):
            case nameof(AddItemViewModel.IconSource):
            case nameof(AddItemViewModel.AutoIconData):
            case nameof(AddItemViewModel.FileIconData):
                UpdatePreview();
                break;
        }
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

    private void ClearFocus() => FocusManager?.ClearFocus();

    // ── Segmented control ────────────────────────────────
    private void WireSegmentedControl()
    {
        SegSoftware.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetType(ShortcutType.Software); };
        SegFolder.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetType(ShortcutType.Folder); };
        SegCommand.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetType(ShortcutType.Command); };
        SegAction.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetType(ShortcutType.Action); };
        SegShortcut.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetType(ShortcutType.Shortcut); };

        KeyboardHelper.WireActivate(SegSoftware, () => SetType(ShortcutType.Software));
        KeyboardHelper.WireActivate(SegFolder, () => SetType(ShortcutType.Folder));
        KeyboardHelper.WireActivate(SegCommand, () => SetType(ShortcutType.Command));
        KeyboardHelper.WireActivate(SegAction, () => SetType(ShortcutType.Action));
        KeyboardHelper.WireActivate(SegShortcut, () => SetType(ShortcutType.Shortcut));
    }

    private void SetType(ShortcutType type)
    {
        if (DataContext is not AddItemViewModel vm) return;
        if (type == vm.SelectedType) return;

        var state = vm.SwitchTab(type);
        ApplyTabVisuals(type, state, vm);
    }

    // ── Apply View-only visual state after tab switch ────
    private void ApplyTabVisuals(ShortcutType type, AddItemViewModel.TabStateData state, AddItemViewModel vm)
    {
        // Icon source grid columns (pure layout, can't be bound)
        if (type is ShortcutType.Action or ShortcutType.Shortcut)
        {
            if (type == ShortcutType.Action)
                SyncActionSelection(state.SelectedActionKey);
            if (type == ShortcutType.Shortcut)
                RestoreShortcutDisplay(state.Path);
        }
        else
        {
            bool hasAutoIcon = type == ShortcutType.Software && state.AutoIconData != null;
            UpdateIconSourceGrid(hasAutoIcon);
        }

        // Clear validation CSS classes (binding handles new state)
        PathBox.Classes.Remove("invalid");
        CmdBox.Classes.Remove("invalid");

        UpdateIconMiniPreview();
        UpdatePreview();
    }

    // ── Text change watchers ─────────────────────────────
    private void WirePathTextChanged()
    {
        // VM handles business logic via OnPathChanged partial method.
        // View only needs to update visual preview on text changes.
        PathBox.TextChanged += (_, _) => UpdatePreview();
        CmdBox.TextChanged += (_, _) => { /* validation handled by VM binding */ };
    }

    // ── Icon source toggle ───────────────────────────────
    private void UpdateIconSourceGrid(bool showAppIcon)
    {
        IconSrcAuto.IsVisible = showAppIcon;
        if (showAppIcon)
        {
            IconSourceGrid.ColumnDefinitions.Clear();
            IconSourceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            IconSourceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            IconSourceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(IconSrcAuto, 0);
            Grid.SetColumn(IconSrcBuiltIn, 1);
            Grid.SetColumn(IconSrcFile, 2);
        }
        else
        {
            IconSourceGrid.ColumnDefinitions.Clear();
            IconSourceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            IconSourceGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(IconSrcBuiltIn, 0);
            Grid.SetColumn(IconSrcFile, 1);
        }
    }

    private void WireIconSourceToggle()
    {
        IconSrcAuto.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetIconSource(IconSource.Auto); };
        IconSrcBuiltIn.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); SetIconSource(IconSource.BuiltIn); };
        IconSrcFile.PointerPressed += (_, e) => { e.Handled = true; ClearFocus(); OnSelectIconFileAsync().FireAndForget(); };

        KeyboardHelper.WireActivate(IconSrcAuto, () => SetIconSource(IconSource.Auto));
        KeyboardHelper.WireActivate(IconSrcBuiltIn, () => SetIconSource(IconSource.BuiltIn));
        KeyboardHelper.WireActivate(IconSrcFile, () => OnSelectIconFileAsync().FireAndForget());
    }

    private void SetIconSource(IconSource src)
    {
        if (DataContext is not AddItemViewModel vm) return;
        vm.IconSource = src;
        UpdatePreview();
    }

    private async Task OnSelectIconFileAsync()
    {
        if (DataContext is not AddItemViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select Icon Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
        if (files.Count == 0) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(files[0].Path.LocalPath);
            vm.SetFileIconData(Convert.ToBase64String(bytes));
            UpdatePreview();
        }
        catch { /* Failed to read file */ }
    }

    // ── Icon chooser dialog ──────────────────────────────
    private void WireChooseIconButton()
    {
        ChooseIconBtn.PointerPressed += (_, e) => { e.Handled = true; OnChooseIconAsync().FireAndForget(); };
        KeyboardHelper.WireActivate(ChooseIconBtn, () => OnChooseIconAsync().FireAndForget());
    }

    private async Task OnChooseIconAsync()
    {
        if (DataContext is not AddItemViewModel vm) return;

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
            vm.IconSource = IconSource.BuiltIn;
            UpdateIconMiniPreview();
            UpdatePreview();
        }
    }

    private void UpdateIconMiniPreview()
    {
        if (DataContext is not AddItemViewModel vm) return;

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
            if (DataContext is AddItemViewModel vm)
            {
                vm.DeleteCommand.Execute(null);
                Close(true);
            }
        };
        KeyboardHelper.WireActivate(DeleteBtn, () =>
        {
            if (DataContext is AddItemViewModel vm)
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

        BrowseBtn.PointerPressed += (_, e) => { e.Handled = true; OnBrowseAsync().FireAndForget(); };
        KeyboardHelper.WireActivate(BrowseBtn, () => OnBrowseAsync().FireAndForget());

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(false);
        };
    }

    private void ConfirmAndClose()
    {
        if (DataContext is AddItemViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
            if (vm.Confirmed) Close(true);
        }
    }

    // ── Browse ───────────────────────────────────────────
    private async Task OnBrowseAsync()
    {
        if (DataContext is not AddItemViewModel vm) return;

        if (vm.SelectedType == ShortcutType.Folder)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "Select Folder", AllowMultiple = false });
            if (folders.Count == 0) return;

            vm.ApplyBrowseResult(folders[0].Path.LocalPath);
            UpdateIconSourceGrid(false);
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select Application",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Applications") { Patterns = new[] { "*.exe" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });
            if (files.Count == 0) return;

            vm.ApplyBrowseResult(files[0].Path.LocalPath);
            UpdateIconSourceGrid(vm.HasAutoIcon);
        }

        UpdatePreview();
    }

    // ── Working directory browse ────────────────────────────
    private void WireWorkingDirBrowse()
    {
        WorkingDirBrowseBtn.PointerPressed += (_, e) => { e.Handled = true; OnBrowseWorkingDirAsync().FireAndForget(); };
        KeyboardHelper.WireActivate(WorkingDirBrowseBtn, () => OnBrowseWorkingDirAsync().FireAndForget());
    }

    private async Task OnBrowseWorkingDirAsync()
    {
        if (DataContext is not AddItemViewModel vm) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select Working Directory", AllowMultiple = false });
        if (folders.Count == 0) return;

        vm.WorkingDirectory = folders[0].Path.LocalPath;
    }

    // ── Action grid ──────────────────────────────────────
    private void BuildActionGrid()
    {
        ActionGrid.Children.Clear();

        foreach (var action in WindowsActions.All)
        {
            var icon = LucideGeometryCache.CreateIcon(
                action.Icon.Name,
                new SolidColorBrush(Color.Parse(action.DefaultColor)),
                14);

            var label = new TextBlock
            {
                Text = action.Label,
                FontSize = 11,
                Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextSecondary")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { icon, label }
            };

            var border = new Border { Classes = { "actionItem" }, Child = sp };
            var capturedAction = action;
            var capturedBorder = border;
            border.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                ClearFocus();
                OnActionSelected(capturedAction, capturedBorder);
            };

            ActionGrid.Children.Add(border);
        }
    }

    private void OnActionSelected(ActionDefinition action, Border border)
    {
        if (DataContext is not AddItemViewModel vm) return;

        if (_selectedActionBorder != null)
            _selectedActionBorder.Classes.Remove("selected");
        border.Classes.Add("selected");
        _selectedActionBorder = border;

        vm.SelectAction(action);

        UpdateIconMiniPreview();
        UpdatePreview();
    }

    private void SyncActionSelection(string key)
    {
        if (_selectedActionBorder != null)
            _selectedActionBorder.Classes.Remove("selected");
        _selectedActionBorder = null;

        for (int i = 0; i < WindowsActions.All.Length && i < ActionGrid.Children.Count; i++)
        {
            if (ActionGrid.Children[i] is Border b)
            {
                if (WindowsActions.All[i].Key == key)
                {
                    b.Classes.Add("selected");
                    _selectedActionBorder = b;
                }
                else
                {
                    b.Classes.Remove("selected");
                }
            }
        }
    }

    // ── Shortcut capture ─────────────────────────────────
    private void WireShortcutCapture()
    {
        ShortcutCapture.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ShortcutCapture.Focus();
        };

        ShortcutCapture.GotFocus += (_, _) =>
        {
            ShortcutCapture.Classes.Add("recording");
            if (DataContext is AddItemViewModel vm && string.IsNullOrWhiteSpace(vm.Path))
            {
                ShortcutDisplay.Text = "Press keys...";
                ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextTertiary"));
            }
        };

        ShortcutCapture.LostFocus += (_, _) =>
        {
            ShortcutCapture.Classes.Remove("recording");
            if (DataContext is AddItemViewModel vm && string.IsNullOrWhiteSpace(vm.Path))
            {
                ShortcutDisplay.Text = "Click to record shortcut...";
                ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPlaceholder"));
            }
        };

        ShortcutCapture.KeyDown += OnShortcutKeyDown;

        ShortcutClearBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ClearShortcut();
        };
        KeyboardHelper.WireActivate(ShortcutClearBtn, ClearShortcut);
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not AddItemViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            FocusManager?.ClearFocus();
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin
            or Key.None or Key.DeadCharProcessed)
            return;

        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(KeyFormatter.Format(e.Key));

        vm.Path = string.Join("+", parts);
        ShortcutDisplay.Text = string.Join(" + ", parts);
        ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("InputFg"));

        FocusManager?.ClearFocus();
    }

    private void ClearShortcut()
    {
        if (DataContext is not AddItemViewModel vm) return;
        vm.Path = "";
        ShortcutDisplay.Text = "Click to record shortcut...";
        ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPlaceholder"));
        ShortcutCapture.Classes.Remove("recording");
    }

    private void RestoreShortcutDisplay(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            ShortcutDisplay.Text = path.Replace("+", " + ");
            ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("InputFg"));
        }
        else
        {
            ShortcutDisplay.Text = "Click to record shortcut...";
            ShortcutDisplay.Foreground = new SolidColorBrush(ThemeHelper.GetColor("TextPlaceholder"));
        }
    }

    // ── Live preview ─────────────────────────────────────
    private void UpdatePreview()
    {
        if (DataContext is not AddItemViewModel vm) return;
        PreviewCircle.Background = ThemeHelper.GetBrush("PreviewCircleBg");

        if (vm.IconSource == IconSource.Auto && !string.IsNullOrEmpty(vm.AutoIconData))
        {
            var img = IconPreviewHelper.CreateBitmapPreview(vm.AutoIconData);
            if (img != null) { PreviewIcon.Child = img; return; }
        }

        if (vm.IconSource == IconSource.File && !string.IsNullOrEmpty(vm.FileIconData))
        {
            var img = IconPreviewHelper.CreateBitmapPreview(vm.FileIconData);
            if (img != null) { PreviewIcon.Child = img; return; }
        }

        var canvas = IconPreviewHelper.CreateLucidePreview(vm.SelectedBuiltInIconKey, Color.Parse(vm.SelectedColor));
        if (canvas != null) PreviewIcon.Child = canvas;
    }
}
