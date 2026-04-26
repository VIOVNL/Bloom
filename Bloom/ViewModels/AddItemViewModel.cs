using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bloom.Models;
using Bloom.Services;
using Bloom.Helpers;

namespace Bloom.ViewModels;

public partial class AddItemViewModel : ViewModelBase
{
    private readonly IIconExtractorService _iconExtractor;
    private readonly IFileSystemService _fileSystem;

    public AddItemViewModel()
        : this(ServiceLocator.IconExtractor, ServiceLocator.FileSystem) { }

    public AddItemViewModel(IIconExtractorService iconExtractor, IFileSystemService fileSystem)
    {
        _iconExtractor = iconExtractor;
        _fileSystem = fileSystem;
    }

    // ── Tab state store ────────────────────────────────
    private readonly Dictionary<ShortcutType, TabStateData> _tabStates = new()
    {
        [ShortcutType.Software] = new(),
        [ShortcutType.Folder] = new(),
        [ShortcutType.Command] = new() { DetailsRevealed = true },
        [ShortcutType.Action] = new() { DetailsRevealed = true },
        [ShortcutType.Shortcut] = new() { DetailsRevealed = true, SelectedBuiltInIconKey = "keyboard" },
    };

    private string? _lastExtractedPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPathSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsCommandSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsActionSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsShortcutSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsSoftwareArgsSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsWorkingDirectorySectionVisible))]
    [NotifyPropertyChangedFor(nameof(PathLabelText))]
    [NotifyPropertyChangedFor(nameof(IsIconSourceSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsIconPreviewSectionVisible))]
    [NotifyPropertyChangedFor(nameof(HasAutoIcon))]
    private ShortcutType _selectedType = ShortcutType.Software;

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private string _arguments = "";

    [ObservableProperty]
    private string _workingDirectory = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIconPreviewSectionVisible))]
    private IconSource _iconSource = IconSource.BuiltIn;

    [ObservableProperty]
    private string _selectedBuiltInIconKey = "rocket";

    [ObservableProperty]
    private string _selectedColor = "#FFFFFF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoIcon))]
    private string? _autoIconData;

    [ObservableProperty]
    private string? _fileIconData;

    [ObservableProperty]
    private string _selectedActionKey = "";

    [ObservableProperty]
    private bool _confirmed;

    [ObservableProperty]
    private bool _canConfirm;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isDeleted;

    public string WindowTitle => IsEditMode ? "Edit Item" : "Add Item";
    public string ConfirmButtonText => IsEditMode ? "Save" : "Add";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoftwareArgsSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsWorkingDirectorySectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsIconSourceSectionVisible))]
    private bool _detailsRevealed;

    [ObservableProperty]
    private bool _isPathInvalid;

    // ── Computed visibility properties ──────────────────

    public bool IsPathSectionVisible => SelectedType is ShortcutType.Software or ShortcutType.Folder;
    public bool IsCommandSectionVisible => SelectedType == ShortcutType.Command;
    public bool IsActionSectionVisible => SelectedType == ShortcutType.Action;
    public bool IsShortcutSectionVisible => SelectedType == ShortcutType.Shortcut;
    public bool IsSoftwareArgsSectionVisible => SelectedType == ShortcutType.Software && DetailsRevealed;
    public bool IsWorkingDirectorySectionVisible => SelectedType is ShortcutType.Software or ShortcutType.Command && DetailsRevealed;
    public string PathLabelText => SelectedType == ShortcutType.Folder ? "FOLDER PATH" : "APPLICATION PATH";
    public bool IsIconSourceSectionVisible => DetailsRevealed && SelectedType is not ShortcutType.Action and not ShortcutType.Shortcut;
    public bool IsIconPreviewSectionVisible => SelectedType is ShortcutType.Action or ShortcutType.Shortcut || IconSource == IconSource.BuiltIn;
    public bool HasAutoIcon => SelectedType == ShortcutType.Software && AutoIconData != null;

    // ── Validation ─────────────────────────────────────

    partial void OnPathChanged(string value)
    {
        UpdateCanConfirm();

        if (SelectedType is ShortcutType.Software or ShortcutType.Folder)
        {
            if (!string.IsNullOrWhiteSpace(value) && !DetailsRevealed)
                RevealDetails();
        }

        if (SelectedType == ShortcutType.Software)
            TryExtractIcon();

        UpdatePathValidation();
    }

    partial void OnLabelChanged(string value) => UpdateCanConfirm();
    partial void OnSelectedActionKeyChanged(string value) => UpdateCanConfirm();

    public void UpdateCanConfirm()
    {
        bool hasPath = !string.IsNullOrWhiteSpace(Path);
        bool hasLabel = !string.IsNullOrWhiteSpace(Label);

        CanConfirm = SelectedType switch
        {
            ShortcutType.Software => hasPath && hasLabel,
            ShortcutType.Folder => hasPath && hasLabel,
            ShortcutType.Command => hasPath && hasLabel,
            ShortcutType.Action => !string.IsNullOrWhiteSpace(SelectedActionKey) && hasLabel,
            ShortcutType.Shortcut => hasPath && hasLabel,
            _ => false
        };
    }

    private void UpdatePathValidation()
    {
        var path = Path?.Trim() ?? "";
        if (string.IsNullOrEmpty(path))
        {
            IsPathInvalid = false;
            return;
        }

        IsPathInvalid = SelectedType switch
        {
            ShortcutType.Software => !_fileSystem.FileExists(path),
            ShortcutType.Folder => !_fileSystem.DirectoryExists(path),
            _ => false
        };
    }

    // ── Browse result handling ──────────────────────────

    public void ApplyBrowseResult(string path)
    {
        Path = path;
        if (string.IsNullOrWhiteSpace(Label))
        {
            Label = SelectedType == ShortcutType.Folder
                ? System.IO.Path.GetFileName(path)
                : System.IO.Path.GetFileNameWithoutExtension(path);
        }
        RevealDetails();
    }

    public void SetFileIconData(string base64Data)
    {
        FileIconData = base64Data;
        IconSource = IconSource.File;
    }

    // ── Tab state management ───────────────────────────

    public void SaveCurrentTab()
    {
        var s = _tabStates[SelectedType];
        s.Path = Path;
        s.Label = Label;
        s.Arguments = Arguments;
        s.WorkingDirectory = WorkingDirectory;
        s.AutoIconData = AutoIconData;
        s.FileIconData = FileIconData;
        s.IconSource = IconSource;
        s.SelectedBuiltInIconKey = SelectedBuiltInIconKey;
        s.SelectedColor = SelectedColor;
        s.SelectedActionKey = SelectedActionKey;
        s.DetailsRevealed = DetailsRevealed;
    }

    public TabStateData SwitchTab(ShortcutType newType)
    {
        SaveCurrentTab();
        SelectedType = newType;
        return RestoreTab(newType);
    }

    public TabStateData RestoreTab(ShortcutType type)
    {
        var s = _tabStates[type];
        Path = s.Path;
        Label = s.Label;
        Arguments = s.Arguments;
        WorkingDirectory = s.WorkingDirectory;
        AutoIconData = s.AutoIconData;
        FileIconData = s.FileIconData;
        IconSource = s.IconSource;
        SelectedBuiltInIconKey = s.SelectedBuiltInIconKey;
        SelectedColor = s.SelectedColor;
        SelectedActionKey = s.SelectedActionKey;
        DetailsRevealed = s.DetailsRevealed;
        UpdateCanConfirm();
        return s;
    }

    public TabStateData GetCurrentTabState() => _tabStates[SelectedType];

    public void RevealDetails()
    {
        DetailsRevealed = true;
        _tabStates[SelectedType].DetailsRevealed = true;
    }

    // ── Icon extraction (business logic) ───────────────

    public bool TryExtractIcon()
    {
        var path = Path?.Trim() ?? "";

        if (!_fileSystem.FileExists(path) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            if (AutoIconData != null)
            {
                AutoIconData = null;
                _lastExtractedPath = null;
                return true;
            }
            return false;
        }

        if (AutoIconData != null && _lastExtractedPath == path) return false;

        var iconData = _iconExtractor.ExtractIconAsBase64(path);

        if (iconData != null)
        {
            _lastExtractedPath = path;
            AutoIconData = iconData;

            if (string.IsNullOrWhiteSpace(Label))
                Label = System.IO.Path.GetFileNameWithoutExtension(path);

            return true;
        }

        if (string.IsNullOrWhiteSpace(Label))
            Label = System.IO.Path.GetFileNameWithoutExtension(path);

        return false;
    }

    // ── Action selection (business logic) ──────────────

    public void SelectAction(ActionDefinition action)
    {
        var previousLabel = SelectedActionKey is { Length: > 0 } prevKey
            && WindowsActions.All.FirstOrDefault(a => a.Key == prevKey) is { } prev
                ? prev.Label
                : null;

        SelectedActionKey = action.Key;

        if (string.IsNullOrWhiteSpace(Label) || Label == previousLabel)
            Label = action.Label;

        SelectedBuiltInIconKey = action.Icon.Name;
        SelectedColor = action.DefaultColor;
        IconSource = IconSource.BuiltIn;
    }

    // ── Confirm ────────────────────────────────────────

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
            Type = SelectedType,
            Path = SelectedType switch
            {
                ShortcutType.Action => SelectedActionKey,
                ShortcutType.Shortcut => "@@keys:" + Path,
                _ => Path
            },
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            IconSource = IconSource,
            BuiltInIconKey = SelectedBuiltInIconKey,
            AutoIconData = AutoIconData,
            FileIconData = FileIconData,
            IconColor = SelectedColor
        };
    }

    // ── Edit mode factory ───────────────────────────────

    public static AddItemViewModel CreateForEdit(BloomItem item)
    {
        var vm = new AddItemViewModel();

        // Determine the path to display
        string displayPath = item.Type == ShortcutType.Shortcut && item.Path.StartsWith("@@keys:")
            ? item.Path["@@keys:".Length..]
            : item.Path;

        string iconKey = string.IsNullOrEmpty(item.BuiltInIconKey) ? "rocket" : item.BuiltInIconKey;

        // Save to tab state first so RestoreTab picks it up
        var state = vm._tabStates[item.Type];
        state.Path = displayPath;
        state.Label = item.Label;
        state.Arguments = item.Arguments;
        state.WorkingDirectory = item.WorkingDirectory;
        state.IconSource = item.IconSource;
        state.SelectedBuiltInIconKey = iconKey;
        state.SelectedColor = item.IconColor;
        state.AutoIconData = item.AutoIconData;
        state.FileIconData = item.FileIconData;
        state.DetailsRevealed = true;
        state.SelectedActionKey = item.Type == ShortcutType.Action ? item.Path : "";

        // Set properties via generated setters
        vm.IsEditMode = true;
        vm.SelectedType = item.Type;
        vm.Path = displayPath;
        vm.Arguments = item.Arguments;
        vm.WorkingDirectory = item.WorkingDirectory;
        vm.Label = item.Label;
        vm.IconSource = item.IconSource;
        vm.SelectedBuiltInIconKey = iconKey;
        vm.SelectedColor = item.IconColor;
        vm.AutoIconData = item.AutoIconData;
        vm.FileIconData = item.FileIconData;
        vm.DetailsRevealed = true;

        if (item.Type == ShortcutType.Action)
            vm.SelectedActionKey = item.Path;

        vm.UpdateCanConfirm();

        return vm;
    }

    // ── Tab state data class ───────────────────────────

    public sealed class TabStateData
    {
        public string Path { get; set; } = "";
        public string Label { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";
        public string? AutoIconData { get; set; }
        public string? FileIconData { get; set; }
        public IconSource IconSource { get; set; } = IconSource.BuiltIn;
        public string SelectedBuiltInIconKey { get; set; } = "rocket";
        public string SelectedColor { get; set; } = "#FFFFFF";
        public string SelectedActionKey { get; set; } = "";
        public bool DetailsRevealed { get; set; }
    }

}
