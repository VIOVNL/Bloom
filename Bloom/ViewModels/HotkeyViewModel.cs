using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;

namespace Bloom.ViewModels;

public partial class HotkeyViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _petalsHotkey;

    [ObservableProperty]
    private string _petalsHotkeyDescription = "Not set";

    [ObservableProperty]
    private string? _bloomHotkey;

    [ObservableProperty]
    private string _bloomHotkeyDescription = "Not set";

    [ObservableProperty]
    private bool _showBloomAtCursor;

    [ObservableProperty]
    private string _showBloomAtCursorDescription = "";

    public ObservableCollection<BloomItem> Items { get; } = new();

    public Dictionary<string, string> ItemHotkeyDescriptions { get; } = new();

    public HotkeyViewModel(string? petalsHotkey, string? bloomHotkey, bool showBloomAtCursor, IEnumerable<BloomItem> items)
    {
        _petalsHotkey = petalsHotkey;
        _bloomHotkey = bloomHotkey;
        _showBloomAtCursor = showBloomAtCursor;

        foreach (var item in items)
        {
            Items.Add(item);
            ItemHotkeyDescriptions[item.Id] = FormatHotkeyDisplay(item.Hotkey);
        }

        UpdatePetalsHotkeyDescription();
        UpdateBloomHotkeyDescription();
        UpdateShowBloomAtCursorDescription();
    }

    partial void OnPetalsHotkeyChanged(string? value)
    {
        WeakReferenceMessenger.Default.Send(new SetPetalsHotkeyRequestedMessage(value));
        UpdatePetalsHotkeyDescription();
    }

    partial void OnBloomHotkeyChanged(string? value)
    {
        WeakReferenceMessenger.Default.Send(new SetBloomHotkeyRequestedMessage(value));
        UpdateBloomHotkeyDescription();
    }

    partial void OnShowBloomAtCursorChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetShowBloomAtCursorRequestedMessage(value));
        UpdateShowBloomAtCursorDescription();
    }

    public void ToggleShowBloomAtCursor() => ShowBloomAtCursor = !ShowBloomAtCursor;

    public void SetPetalsHotkey(string? hotkey) => PetalsHotkey = hotkey;
    public void SetBloomHotkey(string? hotkey) => BloomHotkey = hotkey;

    public void SetItemHotkey(string itemId, string? hotkey)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;
        item.Hotkey = hotkey;
        ItemHotkeyDescriptions[itemId] = FormatHotkeyDisplay(hotkey);
        WeakReferenceMessenger.Default.Send(new SetItemHotkeyRequestedMessage(itemId, hotkey));
    }

    public string? FindConflict(string? combo, string? excludeItemId)
    {
        if (string.IsNullOrWhiteSpace(combo)) return null;
        var normalized = NormalizeCombo(combo);
        if (normalized == NormalizeCombo(PetalsHotkey)) return "Toggle Petals";
        if (normalized == NormalizeCombo(BloomHotkey)) return "Toggle Bloom";
        foreach (var item in Items)
        {
            if (item.Id == excludeItemId) continue;
            if (normalized == NormalizeCombo(item.Hotkey))
                return item.Label;
        }
        return null;
    }

    private static string? NormalizeCombo(string? combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return null;
        if (!HotkeyService.Parse(combo, out var mod, out var vk)) return combo;
        return $"{mod:X}+{vk:X}";
    }

    private void UpdateShowBloomAtCursorDescription() =>
        ShowBloomAtCursorDescription = ShowBloomAtCursor ? "Bloom appears at mouse cursor" : "Bloom stays at its fixed position";

    private void UpdatePetalsHotkeyDescription() =>
        PetalsHotkeyDescription = FormatHotkeyDisplay(PetalsHotkey);

    private void UpdateBloomHotkeyDescription() =>
        BloomHotkeyDescription = FormatHotkeyDisplay(BloomHotkey);

    internal static string FormatHotkeyDisplay(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return "Not set";
        return HotkeyService.Parse(hotkey, out var mod, out var vk)
            ? HotkeyService.Format(mod, vk)
            : hotkey;
    }
}
