using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

internal sealed class DialogHandler
{
    private readonly Window _owner;
    private readonly Func<MainWindowViewModel?> _getVm;
    private readonly Func<Task> _closeActiveBloom;
    private readonly Action _cancelPendingDeactivationClose;
    private readonly Func<BloomContext> _getAppBloom;
    private readonly Func<BloomItem?> _getCurrentGroup;

    public DialogHandler(
        Window owner,
        Func<MainWindowViewModel?> getVm,
        Func<Task> closeActiveBloom,
        Action cancelPendingDeactivationClose,
        Func<BloomContext> getAppBloom,
        Func<BloomItem?> getCurrentGroup)
    {
        _owner = owner;
        _getVm = getVm;
        _closeActiveBloom = closeActiveBloom;
        _cancelPendingDeactivationClose = cancelPendingDeactivationClose;
        _getAppBloom = getAppBloom;
        _getCurrentGroup = getCurrentGroup;
    }

    public async Task ShowTutorialIfFirstLaunch()
    {
        var config = ConfigService.Load();
        if (!config.Settings.FirstLaunch) return;

        await new TutorialWindow().ShowDialog<bool?>(_owner);

        config.Settings.FirstLaunch = false;
        config.Settings.LastSeenVersion = ServiceLocator.Update.CurrentVersion;
        ConfigService.Save(config);
    }

    public async Task ShowChangelogIfUpdated()
    {
        var config = ConfigService.Load();

        if (config.Settings.FirstLaunch) return;

        var currentVersion = ServiceLocator.Update.CurrentVersion;
        if (config.Settings.LastSeenVersion == currentVersion) return;

        config.Settings.LastSeenVersion = currentVersion;
        ConfigService.Save(config);

        await Task.Delay(500);

        var changelogVm = new ChangelogViewModel();
        var dialog = new ChangelogWindow { DataContext = changelogVm };
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnChangelogRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var changelogVm = new ChangelogViewModel();
        var dialog = new ChangelogWindow { DataContext = changelogVm };
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnAddItemRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var addVm = new AddItemViewModel();
        var dialog = new AddItemWindow { DataContext = addVm };

        var result = await dialog.ShowDialog<bool?>(_owner);

        if (result == true && addVm.Confirmed)
        {
            vm.AddBloomItem(addVm.ToBloomItem());
        }
    }

    public async Task OnCreateGroupRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var groupVm = new AddGroupViewModel();
        groupVm.LoadAvailableItems(vm.Items.Where(i => !i.IsInGroup).ToList());

        var dialog = new AddGroupWindow { DataContext = groupVm };
        var result = await dialog.ShowDialog<bool?>(_owner);

        if (result == true && groupVm.Confirmed)
        {
            var newItem = groupVm.ToBloomItem();
            newItem.ChildIds = groupVm.GetSelectedItemIds().ToList();
            vm.AddBloomItem(newItem);
        }
    }

    public async Task OnSettingsRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var settingsVm = new SettingsViewModel(vm.Theme, vm.LabelMode, vm.StartWithWindows, vm.AutoUpdate, vm.UnBloomOnFocusLoss, vm.Scale, vm.AlwaysOnTop);
        var dialog = new SettingsWindow { DataContext = settingsVm };
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnHotkeyWindowRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var hotkeyVm = new HotkeyViewModel(vm.PetalsHotkey, vm.BloomHotkey, vm.ShowBloomAtCursor, vm.Items);
        var dialog = new HotkeyWindow { DataContext = hotkeyVm };
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnAboutRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var aboutVm = new AboutViewModel(vm.UpdateService, ServiceLocator.Process);
        var dialog = new AboutWindow { DataContext = aboutVm };
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnUpdateRequestedAsync()
    {
        _cancelPendingDeactivationClose();
        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        var dialog = new UpdateWindow(vm.UpdateService);
        await dialog.ShowDialog<object?>(_owner);
    }

    public async Task OnEditItemRequestedAsync(EditItemRequestedMessage msg)
    {
        _cancelPendingDeactivationClose();

        var targetGroup = _getCurrentGroup();
        var sourcePetals = _getAppBloom().SourceItems;

        await _closeActiveBloom();
        await Task.Delay(100);

        var vm = _getVm();
        if (vm == null) return;

        if (targetGroup != null)
        {
            int childIndex = msg.Index - 1;
            if (childIndex < 0 || childIndex >= targetGroup.ChildIds.Count) return;

            var childId = targetGroup.ChildIds[childIndex];
            var item = vm.Items.FirstOrDefault(i => i.Id == childId);
            if (item == null) return;

            if (item.Type == ShortcutType.Group)
            {
                await EditGroupItemAsync(item, vm,
                    onDelete: () => { targetGroup.ChildIds.RemoveAt(childIndex); vm.RemoveGroupWithChildren(item); });
            }
            else
            {
                await EditNonGroupItemAsync(item, vm,
                    onDelete: () => { targetGroup.ChildIds.RemoveAt(childIndex); vm.RemoveBloomItem(item); });
            }
        }
        else
        {
            if (msg.Index < 0 || msg.Index >= sourcePetals.Length) return;
            var sourceId = sourcePetals[msg.Index].SourceItemId;
            if (sourceId == null) return;

            var item = vm.Items.FirstOrDefault(i => i.Id == sourceId);
            if (item == null) return;

            if (item.Type == ShortcutType.Group)
            {
                await EditGroupItemAsync(item, vm,
                    onDelete: () => vm.RemoveGroupWithChildren(item));
            }
            else
            {
                await EditNonGroupItemAsync(item, vm,
                    onDelete: () => vm.RemoveBloomItem(item));
            }
        }
    }

    private async Task EditGroupItemAsync(BloomItem item, MainWindowViewModel vm, Action onDelete)
    {
        var groupVm = AddGroupViewModel.CreateForEdit(item);
        var available = GetAvailableItemsForGroup(item, vm);
        groupVm.LoadAvailableItems(available, new HashSet<string>(item.ChildIds));

        var dialog = new AddGroupWindow { DataContext = groupVm };
        var result = await dialog.ShowDialog<bool?>(_owner);

        if (groupVm.IsDeleted)
        {
            onDelete();
        }
        else if (result == true && groupVm.Confirmed)
        {
            var updated = groupVm.ToBloomItem();
            updated.Id = item.Id;
            updated.ChildIds = groupVm.GetSelectedItemIds().ToList();
            var idx = vm.Items.IndexOf(item);
            if (idx >= 0) vm.UpdateBloomItem(idx, updated);
        }
    }

    private async Task EditNonGroupItemAsync(BloomItem item, MainWindowViewModel vm, Action onDelete)
    {
        var editVm = AddItemViewModel.CreateForEdit(item);
        var dialog = new AddItemWindow { DataContext = editVm };
        var result = await dialog.ShowDialog<bool?>(_owner);

        if (editVm.IsDeleted)
        {
            onDelete();
        }
        else if (result == true && editVm.Confirmed)
        {
            var updated = editVm.ToBloomItem();
            updated.Id = item.Id;
            var idx = vm.Items.IndexOf(item);
            if (idx >= 0) vm.UpdateBloomItem(idx, updated);
        }
    }

    // ── Cycle detection for group nesting ────────────────

    private static bool IsDescendantOf(string targetId, BloomItem group, IDictionary<string, BloomItem> lookup)
    {
        foreach (var childId in group.ChildIds)
        {
            if (childId == targetId) return true;
            if (lookup.TryGetValue(childId, out var child) && child.Type == ShortcutType.Group)
            {
                if (IsDescendantOf(targetId, child, lookup)) return true;
            }
        }
        return false;
    }

    private static List<BloomItem> GetAvailableItemsForGroup(BloomItem group, MainWindowViewModel vm)
    {
        var currentChildIds = new HashSet<string>(group.ChildIds);
        var lookup = vm.Items.ToDictionary(i => i.Id);
        return vm.Items
            .Where(i => i.Id != group.Id
                && (!i.IsInGroup || currentChildIds.Contains(i.Id))
                && !(i.Type == ShortcutType.Group && IsDescendantOf(group.Id, i, lookup)))
            .ToList();
    }
}
