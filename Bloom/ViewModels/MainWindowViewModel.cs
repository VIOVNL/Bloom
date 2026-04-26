using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;

namespace Bloom.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IProcessLauncher _process;
    private readonly IStartupService _startup;
    private readonly IApplicationService _app;

    [ObservableProperty]
    private bool _isMenuOpen;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private LabelMode _labelMode = LabelMode.Tooltip;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _unBloomOnFocusLoss = true;

    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private bool _showBloomAtCursor;

    [ObservableProperty]
    private string? _petalsHotkey;

    [ObservableProperty]
    private string? _bloomHotkey;

    [ObservableProperty]
    private AppScale _scale = AppScale.Medium;

    public int? WindowX { get; set; }
    public int? WindowY { get; set; }

    public ObservableCollection<BloomItem> Items { get; } = new();

    public IUpdateService UpdateService { get; }

    public MainWindowViewModel()
        : this(ServiceLocator.Config, ServiceLocator.Update, ServiceLocator.Process, ServiceLocator.Startup, ServiceLocator.Application) { }

    public MainWindowViewModel(IConfigService config, IUpdateService update, IProcessLauncher process, IStartupService startup, IApplicationService app)
    {
        _config = config;
        UpdateService = update;
        _process = process;
        _startup = startup;
        _app = app;

        var cfg = _config.Load();
        foreach (var item in cfg.Items)
            Items.Add(item);
        RecalculateIsInGroup();

        _theme = cfg.Settings.Theme;
        _labelMode = cfg.Settings.LabelMode;
        _startWithWindows = cfg.Settings.StartWithWindows;
        _autoUpdate = cfg.Settings.AutoUpdate;
        _unBloomOnFocusLoss = cfg.Settings.UnBloomOnFocusLoss;
        _alwaysOnTop = cfg.Settings.AlwaysOnTop;
        _showBloomAtCursor = cfg.Settings.ShowBloomAtCursor;
        _petalsHotkey = cfg.Settings.PetalsHotkey;
        _bloomHotkey = cfg.Settings.BloomHotkey;
        _scale = cfg.Settings.Scale;
        WindowX = cfg.Settings.WindowX;
        WindowY = cfg.Settings.WindowY;

        _startup.SetStartWithWindows(_startWithWindows);

        // Register for cross-VM messages
        WeakReferenceMessenger.Default.Register<SetThemeRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetThemeRequested(m));
        WeakReferenceMessenger.Default.Register<SetLabelModeRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetLabelModeRequested(m));
        WeakReferenceMessenger.Default.Register<SetStartupRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetStartupRequested(m));
        WeakReferenceMessenger.Default.Register<SetAutoUpdateRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetAutoUpdateRequested(m));
        WeakReferenceMessenger.Default.Register<SetUnBloomOnFocusLossRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetUnBloomOnFocusLossRequested(m));
        WeakReferenceMessenger.Default.Register<SetAlwaysOnTopRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetAlwaysOnTopRequested(m));
        WeakReferenceMessenger.Default.Register<SetShowBloomAtCursorRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetShowBloomAtCursorRequested(m));
        WeakReferenceMessenger.Default.Register<SetScaleRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetScaleRequested(m));
        WeakReferenceMessenger.Default.Register<SetPetalsHotkeyRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetPetalsHotkeyRequested(m));
        WeakReferenceMessenger.Default.Register<SetBloomHotkeyRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetBloomHotkeyRequested(m));
        WeakReferenceMessenger.Default.Register<SetItemHotkeyRequestedMessage>(this, (r, m) => ((MainWindowViewModel)r).OnSetItemHotkeyRequested(m));
        WeakReferenceMessenger.Default.Register<UpdateAvailableNotification>(this, (r, _) => ((MainWindowViewModel)r).IsUpdateAvailable = true);
    }

    public async Task CheckForUpdatesOnMenuOpenAsync()
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;
        try
        {
            var hasUpdate = await UpdateService.CheckForUpdatesAsync();
            if (hasUpdate)
            {
                if (AutoUpdate)
                {
                    // Auto-update: silently download and install
                    await UpdateService.DownloadAsync();
                    UpdateService.ApplyAndRestart();
                }
                else
                {
                    // Manual mode: show the update petal
                    IsUpdateAvailable = true;
                }
            }
        }
        catch { /* silently fail in dev mode or on network errors */ }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private void LaunchByName(PetalItem? petal)
    {
        if (petal == null) return;

        // Group navigation
        if (petal.SourceGroup != null)
        {
            WeakReferenceMessenger.Default.Send(new NavigateIntoGroupMessage(petal.SourceGroup));
            return;
        }

        if (string.IsNullOrEmpty(petal.ProcessName)) return;

        if (petal.ProcessName.StartsWith("@@"))
        {
            HandleInternalAction(petal.ProcessName);
            return;
        }

        _process.Launch(petal.ProcessName,
            string.IsNullOrEmpty(petal.Arguments) ? null : petal.Arguments,
            string.IsNullOrEmpty(petal.WorkingDirectory) ? null : petal.WorkingDirectory);
        if (UnBloomOnFocusLoss)
            IsMenuOpen = false;
    }

    private void HandleInternalAction(string action)
    {
        switch (action)
        {
            case "@@add":
                WeakReferenceMessenger.Default.Send(new AddItemRequestedMessage());
                return;
            case "@@creategroup":
                WeakReferenceMessenger.Default.Send(new CreateGroupRequestedMessage());
                return;
            case "@@bloomsettings":
                WeakReferenceMessenger.Default.Send(new SettingsRequestedMessage());
                return;
            case "@@about":
                WeakReferenceMessenger.Default.Send(new AboutRequestedMessage());
                return;
            case "@@update":
                WeakReferenceMessenger.Default.Send(new UpdateRequestedMessage());
                return;
            case "@@reportbug":
                _process.Launch("https://github.com/VIOVNL/Bloom/issues/new?labels=bug");
                if (UnBloomOnFocusLoss)
                    IsMenuOpen = false;
                return;
            case "@@featurerequest":
                _process.Launch("https://github.com/VIOVNL/Bloom/issues/new?labels=enhancement");
                if (UnBloomOnFocusLoss)
                    IsMenuOpen = false;
                return;
            case "@@docs":
                _process.Launch("https://github.com/VIOVNL/Bloom#readme");
                if (UnBloomOnFocusLoss)
                    IsMenuOpen = false;
                return;
            case "@@changelog":
                WeakReferenceMessenger.Default.Send(new ChangelogRequestedMessage());
                return;
            case "@@back":
                WeakReferenceMessenger.Default.Send(new NavigateBackRequestedMessage());
                return;
            case "@@exit":
                ExitApp();
                return;
#if DEBUG
            case "@@debug_add5":
                AddRandomTestItems(5);
                return;
#endif
        }

        if (InternalActionDispatcher.TryDispatch(action) && UnBloomOnFocusLoss)
            IsMenuOpen = false;
    }

#if DEBUG
    private void AddRandomTestItems(int count)
    {
        var rng = new System.Random();
        var names = new[] { "Firefox", "Spotify", "Discord", "Slack", "Terminal", "Notes", "Camera", "Maps", "Clock", "Mail" };
        var colors = new[] { "#FF6B6B", "#51CF66", "#339AF0", "#FFA94D", "#CC5DE8", "#22B8CF", "#FFD43B", "#5C7CFA", "#F06595", "#20C997" };
        var icons = new[] { LucideIcon.Globe, LucideIcon.Music, LucideIcon.MessageCircle, LucideIcon.Hash, LucideIcon.SquareTerminal, LucideIcon.StickyNote, LucideIcon.Camera, LucideIcon.Map, LucideIcon.Clock, LucideIcon.Mail };

        for (int i = 0; i < count; i++)
        {
            int idx = rng.Next(names.Length);
            Items.Add(new BloomItem
            {
                Label = $"{names[idx]} {Items.Count + 1}",
                IconColor = colors[idx],
                IconSource = IconSource.BuiltIn,
                BuiltInIconKey = icons[idx].Name,
                Path = "explorer.exe",
                Type = ShortcutType.Software
            });
        }
        SaveConfig();
    }
#endif

    public void AddBloomItem(BloomItem item)
    {
        Items.Add(item);
        RecalculateIsInGroup();
        SaveConfig();
    }

    public void RemoveBloomItem(BloomItem item)
    {
        Items.Remove(item);
        RecalculateIsInGroup();
        SaveConfig();
    }

    public void RemoveGroupWithChildren(BloomItem group)
    {
        var toRemove = new List<BloomItem> { group };
        CollectChildren(group, toRemove);

        foreach (var item in toRemove)
            Items.Remove(item);

        RecalculateIsInGroup();
        SaveConfig();
    }

    private void CollectChildren(BloomItem group, List<BloomItem> result)
    {
        foreach (var childId in group.ChildIds)
        {
            var child = Items.FirstOrDefault(i => i.Id == childId);
            if (child == null) continue;
            result.Add(child);
            if (child.Type == ShortcutType.Group)
                CollectChildren(child, result);
        }
    }

    public void UpdateBloomItem(int index, BloomItem updated)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items[index] = updated;
            RecalculateIsInGroup();
            SaveConfig();
        }
    }

    public void RecalculateIsInGroup()
    {
        var groupedIds = new HashSet<string>(
            Items.Where(i => i.Type == ShortcutType.Group)
                 .SelectMany(g => g.ChildIds));

        foreach (var item in Items)
            item.IsInGroup = groupedIds.Contains(item.Id);
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _app.SetThemeVariant(value == AppTheme.Dark);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        Theme = Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        SaveConfig();
    }

    [RelayCommand]
    private void ToggleStartWithWindows()
    {
        StartWithWindows = !StartWithWindows;
        _startup.SetStartWithWindows(StartWithWindows);
        SaveConfig();
    }

    [RelayCommand]
    private void ToggleAutoUpdate()
    {
        AutoUpdate = !AutoUpdate;
        SaveConfig();
    }

    [RelayCommand]
    private void SetLabelMode(LabelMode mode)
    {
        if (LabelMode == mode) return;
        LabelMode = mode;
        SaveConfig();
    }

    private void ExitApp()
    {
        _app.Shutdown();
    }

    // ── Cross-VM message handlers ───────────────────────

    private void OnSetThemeRequested(SetThemeRequestedMessage m)
    {
        var desired = m.IsDark ? AppTheme.Dark : AppTheme.Light;
        if (Theme != desired)
            ToggleTheme();
    }

    private void OnSetLabelModeRequested(SetLabelModeRequestedMessage m)
    {
        SetLabelMode(m.Mode);
    }

    private void OnSetStartupRequested(SetStartupRequestedMessage m)
    {
        if (StartWithWindows != m.Enabled)
            ToggleStartWithWindows();
    }

    private void OnSetAutoUpdateRequested(SetAutoUpdateRequestedMessage m)
    {
        if (AutoUpdate != m.Enabled)
            ToggleAutoUpdate();
    }

    private void OnSetUnBloomOnFocusLossRequested(SetUnBloomOnFocusLossRequestedMessage m)
    {
        if (UnBloomOnFocusLoss != m.Enabled)
        {
            UnBloomOnFocusLoss = m.Enabled;
            SaveConfig();
        }
    }

    private void OnSetAlwaysOnTopRequested(SetAlwaysOnTopRequestedMessage m)
    {
        if (AlwaysOnTop != m.Enabled)
        {
            AlwaysOnTop = m.Enabled;
            SaveConfig();
        }
    }

    private void OnSetShowBloomAtCursorRequested(SetShowBloomAtCursorRequestedMessage m)
    {
        if (ShowBloomAtCursor != m.Enabled)
        {
            ShowBloomAtCursor = m.Enabled;
            SaveConfig();
        }
    }

    private void OnSetScaleRequested(SetScaleRequestedMessage m)
    {
        if (Scale != m.Scale)
        {
            Scale = m.Scale;
            SaveConfig();
        }
    }

    private void OnSetPetalsHotkeyRequested(SetPetalsHotkeyRequestedMessage m)
    {
        if (PetalsHotkey != m.Hotkey)
        {
            PetalsHotkey = m.Hotkey;
            SaveConfig();
        }
    }

    private void OnSetBloomHotkeyRequested(SetBloomHotkeyRequestedMessage m)
    {
        if (BloomHotkey != m.Hotkey)
        {
            BloomHotkey = m.Hotkey;
            SaveConfig();
        }
    }

    private void OnSetItemHotkeyRequested(SetItemHotkeyRequestedMessage m)
    {
        var item = Items.FirstOrDefault(i => i.Id == m.ItemId);
        if (item == null) return;
        item.Hotkey = m.Hotkey;
        SaveConfig();
        WeakReferenceMessenger.Default.Send(new ItemHotkeysChangedMessage());
    }

    public void SavePosition()
    {
        var config = _config.Load();
        config.Settings.WindowX = WindowX;
        config.Settings.WindowY = WindowY;
        _config.Save(config);
    }

    internal void SaveConfig()
    {
        var config = _config.Load();
        config.Items = Items.ToList();
        config.Settings.Theme = Theme;
        config.Settings.LabelMode = LabelMode;
        config.Settings.StartWithWindows = StartWithWindows;
        config.Settings.AutoUpdate = AutoUpdate;
        config.Settings.UnBloomOnFocusLoss = UnBloomOnFocusLoss;
        config.Settings.AlwaysOnTop = AlwaysOnTop;
        config.Settings.ShowBloomAtCursor = ShowBloomAtCursor;
        config.Settings.PetalsHotkey = PetalsHotkey;
        config.Settings.BloomHotkey = BloomHotkey;
        config.Settings.Scale = Scale;
        config.Settings.WindowX = WindowX;
        config.Settings.WindowY = WindowY;
        _config.Save(config);
    }
}
