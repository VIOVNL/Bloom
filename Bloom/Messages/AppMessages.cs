using Bloom.Models;

namespace Bloom.Messages;

// Navigation requests (ViewModel → View) — empty messages, no payload
public sealed class AddItemRequestedMessage;
public sealed class CreateGroupRequestedMessage;
public sealed class SettingsRequestedMessage;
public sealed class AboutRequestedMessage;
public sealed class UpdateRequestedMessage;
public sealed class ChangelogRequestedMessage;

// Settings change requests (SettingsVM → MainVM)
public sealed class SetThemeRequestedMessage(bool IsDark)
{
    public bool IsDark { get; } = IsDark;
}

public sealed class SetLabelModeRequestedMessage(LabelMode Mode)
{
    public LabelMode Mode { get; } = Mode;
}

public sealed class SetStartupRequestedMessage(bool Enabled)
{
    public bool Enabled { get; } = Enabled;
}

public sealed class SetAutoUpdateRequestedMessage(bool Enabled)
{
    public bool Enabled { get; } = Enabled;
}

public sealed class SetUnBloomOnFocusLossRequestedMessage(bool Enabled)
{
    public bool Enabled { get; } = Enabled;
}

public sealed class SetAlwaysOnTopRequestedMessage(bool Enabled)
{
    public bool Enabled { get; } = Enabled;
}

public sealed class SetShowBloomAtCursorRequestedMessage(bool Enabled)
{
    public bool Enabled { get; } = Enabled;
}

public sealed class SetScaleRequestedMessage(AppScale Scale)
{
    public AppScale Scale { get; } = Scale;
}

// Edit item request (PetalFactory → MainWindow)
public sealed class EditItemRequestedMessage(int Index)
{
    public int Index { get; } = Index;
}

// Group navigation (ViewModel → MainWindow)
public sealed class NavigateIntoGroupMessage(BloomItem Group)
{
    public BloomItem Group { get; } = Group;
}

public sealed class NavigateBackRequestedMessage;

// Hotkey changes (SettingsVM → MainVM)
public sealed class SetPetalsHotkeyRequestedMessage(string? Hotkey)
{
    public string? Hotkey { get; } = Hotkey;
}

public sealed class SetBloomHotkeyRequestedMessage(string? Hotkey)
{
    public string? Hotkey { get; } = Hotkey;
}

// Hotkey window navigation
public sealed class HotkeyWindowRequestedMessage;

// Item hotkey changed (HotkeyVM → MainVM)
public sealed class SetItemHotkeyRequestedMessage(string ItemId, string? Hotkey)
{
    public string ItemId { get; } = ItemId;
    public string? Hotkey { get; } = Hotkey;
}

// Re-register all item hotkeys
public sealed class ItemHotkeysChangedMessage;

// Update notification (AboutVM → MainVM)
public sealed class UpdateAvailableNotification;
