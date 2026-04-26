using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Messages;
using Bloom.Models;

namespace Bloom.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private LabelMode _labelMode;

    [ObservableProperty]
    private string _themeDescription = "Dark";

    [ObservableProperty]
    private string _labelDescription = "";

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _autoUpdate;

    [ObservableProperty]
    private string _autoUpdateDescription = "Check for updates on launch";

    [ObservableProperty]
    private bool _unBloomOnFocusLoss;

    [ObservableProperty]
    private string _unBloomDescription = "";

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private string _alwaysOnTopDescription = "";

    [ObservableProperty]
    private AppScale _scale;

    [ObservableProperty]
    private string _scaleDescription = "";

    private static readonly string[] ScaleDescriptions =
    {
        "Compact size",
        "Default size",
        "Larger petals & button",
        "Maximum size"
    };

    private static readonly string[] LabelDescriptions =
    {
        "Show labels below each petal",
        "Show labels on hover as tooltips",
        "Show labels overlaid on petals",
        "No labels shown"
    };

    public SettingsViewModel(AppTheme theme, LabelMode labelMode, bool startWithWindows, bool autoUpdate, bool closeOnFocusLoss, AppScale scale, bool alwaysOnTop)
    {
        _isDarkTheme = theme == AppTheme.Dark;
        _labelMode = labelMode;
        _startWithWindows = startWithWindows;
        _autoUpdate = autoUpdate;
        _unBloomOnFocusLoss = closeOnFocusLoss;
        _alwaysOnTop = alwaysOnTop;
        _scale = scale;

        UpdateThemeDescription();
        UpdateLabelDescription();
        UpdateAutoUpdateDescription();
        UpdateUnBloomDescription();
        UpdateAlwaysOnTopDescription();
        UpdateScaleDescription();
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetThemeRequestedMessage(value));
        UpdateThemeDescription();
    }

    partial void OnLabelModeChanged(LabelMode value)
    {
        WeakReferenceMessenger.Default.Send(new SetLabelModeRequestedMessage(value));
        UpdateLabelDescription();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetStartupRequestedMessage(value));
    }

    partial void OnAutoUpdateChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetAutoUpdateRequestedMessage(value));
        UpdateAutoUpdateDescription();
    }

    partial void OnUnBloomOnFocusLossChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetUnBloomOnFocusLossRequestedMessage(value));
        UpdateUnBloomDescription();
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new SetAlwaysOnTopRequestedMessage(value));
        UpdateAlwaysOnTopDescription();
    }

    partial void OnScaleChanged(AppScale value)
    {
        WeakReferenceMessenger.Default.Send(new SetScaleRequestedMessage(value));
        UpdateScaleDescription();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    [RelayCommand]
    private void SelectLabelMode(string modeStr)
    {
        if (System.Enum.TryParse<LabelMode>(modeStr, out var mode))
            LabelMode = mode;
    }

    [RelayCommand]
    private void ToggleStartup() => StartWithWindows = !StartWithWindows;

    [RelayCommand]
    private void ToggleAutoUpdate() => AutoUpdate = !AutoUpdate;

    [RelayCommand]
    private void ToggleUnBloomOnFocusLoss() => UnBloomOnFocusLoss = !UnBloomOnFocusLoss;

    [RelayCommand]
    private void ToggleAlwaysOnTop() => AlwaysOnTop = !AlwaysOnTop;

    [RelayCommand]
    private void SelectScale(string scaleStr)
    {
        if (System.Enum.TryParse<AppScale>(scaleStr, out var scale))
            Scale = scale;
    }

    private void UpdateThemeDescription() =>
        ThemeDescription = IsDarkTheme ? "Dark" : "Light";

    private void UpdateLabelDescription() =>
        LabelDescription = LabelDescriptions[(int)LabelMode];

    private void UpdateAutoUpdateDescription() =>
        AutoUpdateDescription = AutoUpdate ? "Automatically install updates" : "Show update petal when available";

    private void UpdateUnBloomDescription() =>
        UnBloomDescription = UnBloomOnFocusLoss ? "UnBloom when focus is lost" : "Only UnBloom by clicking Bloom";

    private void UpdateAlwaysOnTopDescription() =>
        AlwaysOnTopDescription = AlwaysOnTop ? "Bloom stays above all windows" : "Bloom can go behind other windows";

    private void UpdateScaleDescription() =>
        ScaleDescription = ScaleDescriptions[(int)Scale];
}
