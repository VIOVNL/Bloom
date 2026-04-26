using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bloom.Models;
using Bloom.Services;

namespace Bloom.ViewModels;

public partial class IconChooserViewModel : ViewModelBase
{
    public const int PageSize = 200;

    [ObservableProperty]
    private string _selectedIconKey = "rocket";

    [ObservableProperty]
    private string _selectedColor = "#FFFFFF";

    [ObservableProperty]
    private string _iconSearchText = "";

    [ObservableProperty]
    private bool _confirmed;

    [ObservableProperty]
    private int _currentPage;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _pageLabel = "1 / 1";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    public string[] AvailableColors => UIConstants.FullColors;

    // Cached filtered list for paging
    private List<LucideIcon> _filteredIcons = new();
    private int _savedBrowsePage;

    [RelayCommand]
    private void SelectIcon(string key)
    {
        SelectedIconKey = key;
    }

    [RelayCommand]
    private void SelectColor(string hex)
    {
        SelectedColor = hex;
    }

    [RelayCommand]
    private void Confirm()
    {
        Confirmed = true;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages - 1)
        {
            CurrentPage++;
            UpdatePageState();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
            UpdatePageState();
        }
    }

    /// <summary>Filter all icons by search text. Caches result for paging.</summary>
    public List<LucideIcon> UpdateFilteredIcons(string search)
    {
        bool hasSearch = !string.IsNullOrEmpty(search);

        // Save browse page before searching so we can restore it when search is cleared
        if (hasSearch && _lastSearchWasEmpty)
            _savedBrowsePage = CurrentPage;

        _filteredIcons = new List<LucideIcon>();
        foreach (var lucide in LucideIcon.List)
        {
            if (hasSearch &&
                !lucide.Label.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !lucide.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (LucideGeometryCache.TryGet(lucide.Name, out var g) && g == null)
                continue;
            _filteredIcons.Add(lucide);
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(_filteredIcons.Count / (double)PageSize));

        if (!hasSearch && !_lastSearchWasEmpty)
            CurrentPage = Math.Min(_savedBrowsePage, TotalPages - 1);
        else if (hasSearch)
            CurrentPage = 0;

        _lastSearchWasEmpty = !hasSearch;
        UpdatePageState();
        return GetCurrentPageIcons();
    }

    private bool _lastSearchWasEmpty = true;

    /// <summary>Get icons for the current page.</summary>
    public List<LucideIcon> GetCurrentPageIcons()
    {
        int skip = CurrentPage * PageSize;
        int take = Math.Min(PageSize, _filteredIcons.Count - skip);
        if (take <= 0) return new List<LucideIcon>();
        return _filteredIcons.GetRange(skip, take);
    }

    private void UpdatePageState()
    {
        CanGoBack = CurrentPage > 0;
        CanGoForward = CurrentPage < TotalPages - 1;
        PageLabel = $"{CurrentPage + 1} / {TotalPages}";
    }
}
