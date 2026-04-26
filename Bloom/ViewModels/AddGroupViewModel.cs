using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bloom.Models;

namespace Bloom.ViewModels;

public partial class AddGroupViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private string _selectedBuiltInIconKey = "layers";

    [ObservableProperty]
    private string _selectedColor = "#FFFFFF";

    [ObservableProperty]
    private bool _confirmed;

    [ObservableProperty]
    private bool _canConfirm;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isDeleted;

    public string WindowTitle => IsEditMode ? "Edit Group" : "Create Group";
    public string ConfirmButtonText => IsEditMode ? "Save" : "Create";

    // ── Group item checklist ────────────────────────────

    private List<SelectableBloomItem> _availableItems = new();
    public List<SelectableBloomItem> AvailableItems => _availableItems;

    public void LoadAvailableItems(List<BloomItem> items, HashSet<string>? selectedIds = null)
    {
        _availableItems = items
            .Select(i => new SelectableBloomItem
            {
                Item = i,
                IsSelected = selectedIds?.Contains(i.Id) ?? false
            })
            .ToList();
    }

    public HashSet<string> GetSelectedItemIds()
    {
        return new HashSet<string>(_availableItems.Where(a => a.IsSelected).Select(a => a.Item.Id));
    }

    // ── Validation ─────────────────────────────────────

    partial void OnLabelChanged(string value) => UpdateCanConfirm();

    public void UpdateCanConfirm()
    {
        CanConfirm = !string.IsNullOrWhiteSpace(Label);
    }

    // ── Commands ────────────────────────────────────────

    [RelayCommand]
    private void Confirm()
    {
        if (!CanConfirm) return;
        Confirmed = true;
    }

    [RelayCommand]
    private void Delete()
    {
        IsDeleted = true;
    }

    // ── Build result ───────────────────────────────────

    public BloomItem ToBloomItem()
    {
        return new BloomItem
        {
            Label = Label,
            Type = ShortcutType.Group,
            Path = "",
            Arguments = "",
            IconSource = IconSource.BuiltIn,
            BuiltInIconKey = SelectedBuiltInIconKey,
            IconColor = SelectedColor
        };
    }

    // ── Edit mode factory ───────────────────────────────

    public static AddGroupViewModel CreateForEdit(BloomItem group)
    {
        var vm = new AddGroupViewModel
        {
            IsEditMode = true,
            Label = group.Label,
            SelectedBuiltInIconKey = string.IsNullOrEmpty(group.BuiltInIconKey) ? "layers" : group.BuiltInIconKey,
            SelectedColor = group.IconColor
        };
        vm.UpdateCanConfirm();
        return vm;
    }

    // ── Selectable item for group checklist ──────────

    public sealed class SelectableBloomItem
    {
        public BloomItem Item { get; set; } = null!;
        public bool IsSelected { get; set; }
    }
}
