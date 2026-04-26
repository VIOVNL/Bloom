using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using Bloom.Models;
using Bloom.ViewModels;

namespace Bloom.Services;

internal sealed class DragDropHandler
{
    private readonly Border _bloomButton;
    private readonly Func<MainWindowViewModel?> _getVm;
    private readonly Action<string> _showToast;
    private readonly Func<BloomItem?> _getCurrentGroup;

    public DragDropHandler(Border bloomButton, Func<MainWindowViewModel?> getVm, Action<string> showToast, Func<BloomItem?> getCurrentGroup)
    {
        _bloomButton = bloomButton;
        _getVm = getVm;
        _showToast = showToast;
        _getCurrentGroup = getCurrentGroup;
    }

    public void Attach(Control target)
    {
        target.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        target.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        target.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        target.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        _bloomButton.Classes.Add("draghover");
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _bloomButton.Classes.Remove("draghover");
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // DataFormats.Files is obsolete
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
#pragma warning restore CS0618
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        _bloomButton.Classes.Remove("draghover");
#pragma warning disable CS0618
        if (!e.Data.Contains(DataFormats.Files)) return;
        e.Handled = true;

        var files = e.Data.GetFiles();
#pragma warning restore CS0618
        if (files == null) return;
        var vm = _getVm();
        if (vm == null) return;

        var currentGroup = _getCurrentGroup();
        int addedCount = 0;
        foreach (var storageItem in files)
        {
            var uri = storageItem.Path;
            if (uri == null || !uri.IsFile) continue;

            var item = CreateBloomItemFromPath(uri.LocalPath);
            if (item != null)
            {
                if (currentGroup != null)
                    currentGroup.ChildIds.Add(item.Id);
                vm.AddBloomItem(item);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            if (currentGroup != null)
            {
                vm.RecalculateIsInGroup();
                vm.SaveConfig();
            }
            _showToast(addedCount == 1 ? "Item added!" : $"{addedCount} items added!");
        }
    }

    // ── BloomItem creation from file paths ──────────────

    internal static BloomItem? CreateBloomItemFromPath(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".lnk")
                return CreateBloomItemFromLnk(filePath);

            if (ext == ".exe")
                return CreateBloomItemFromExe(filePath);

            if (Directory.Exists(filePath))
            {
                return new BloomItem
                {
                    Label = new DirectoryInfo(filePath).Name,
                    Type = ShortcutType.Folder,
                    Path = filePath,
                    IconSource = IconSource.BuiltIn,
                    BuiltInIconKey = LucideIcon.Folder.Name,
                    IconColor = NextColor()
                };
            }

            if (File.Exists(filePath))
            {
                var fileIcon = GetIconForExtension(ext);
                return new BloomItem
                {
                    Label = Path.GetFileNameWithoutExtension(filePath),
                    Type = ShortcutType.Software,
                    Path = filePath,
                    IconSource = IconSource.BuiltIn,
                    BuiltInIconKey = fileIcon.Name,
                    IconColor = NextColor()
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create BloomItem from dropped path: {Path}", filePath);
            return null;
        }
    }

#pragma warning disable CA1416 // Windows-only app
    private static BloomItem CreateBloomItemFromExe(string exePath)
    {
        var label = Path.GetFileNameWithoutExtension(exePath);
        var iconData = IconExtractorService.ExtractIconAsBase64(exePath);

        return new BloomItem
        {
            Label = label,
            Type = ShortcutType.Software,
            Path = exePath,
            IconSource = iconData != null ? IconSource.Auto : IconSource.BuiltIn,
            AutoIconData = iconData,
            BuiltInIconKey = iconData == null ? LucideIcon.File.Name : "",
            IconColor = iconData != null ? "#FFFFFF" : NextColor()
        };
    }

    private static BloomItem CreateBloomItemFromLnk(string lnkPath)
    {
        var label = Path.GetFileNameWithoutExtension(lnkPath);
        var target = ShortcutResolverService.ResolveShortcutTarget(lnkPath);

        if (string.IsNullOrEmpty(target))
        {
            return new BloomItem
            {
                Label = label,
                Type = ShortcutType.Software,
                Path = lnkPath,
                IconSource = IconSource.BuiltIn,
                BuiltInIconKey = LucideIcon.File.Name,
                IconColor = NextColor()
            };
        }

        if (Directory.Exists(target))
        {
            return new BloomItem
            {
                Label = label,
                Type = ShortcutType.Folder,
                Path = target,
                IconSource = IconSource.BuiltIn,
                BuiltInIconKey = LucideIcon.Folder.Name,
                IconColor = NextColor()
            };
        }

        var targetExt = Path.GetExtension(target).ToLowerInvariant();
        if (targetExt == ".exe" && File.Exists(target))
        {
            var iconData = IconExtractorService.ExtractIconAsBase64(target);
            if (iconData != null)
            {
                return new BloomItem
                {
                    Label = label,
                    Type = ShortcutType.Software,
                    Path = target,
                    IconSource = IconSource.Auto,
                    AutoIconData = iconData,
                    IconColor = "#FFFFFF"
                };
            }
        }

        var targetIcon = GetIconForExtension(targetExt);
        return new BloomItem
        {
            Label = label,
            Type = ShortcutType.Software,
            Path = target,
            IconSource = IconSource.BuiltIn,
            BuiltInIconKey = targetIcon.Name,
            IconColor = NextColor()
        };
    }
#pragma warning restore CA1416

    // ── Color cycling ───────────────────────────────────

    private static readonly string[] PleasantColors =
    {
        "#FF6B6B", "#51CF66", "#339AF0", "#FFA94D", "#CC5DE8",
        "#22B8CF", "#FFD43B", "#5C7CFA", "#F06595", "#20C997"
    };

    private static readonly Random _colorRng = new();
    private static int _lastColorIndex = -1;

    private static string NextColor()
    {
        int idx;
        do { idx = _colorRng.Next(PleasantColors.Length); }
        while (idx == _lastColorIndex && PleasantColors.Length > 1);
        _lastColorIndex = idx;
        return PleasantColors[idx];
    }

    // ── File extension → icon mapping ───────────────────

    private static LucideIcon GetIconForExtension(string ext)
    {
        return ExtensionIcons.TryGetValue(ext, out var icon) ? icon : LucideIcon.File;
    }

    private static readonly Dictionary<string, LucideIcon> ExtensionIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        { ".mp4", LucideIcon.FilePlay }, { ".mkv", LucideIcon.FilePlay }, { ".avi", LucideIcon.FilePlay },
        { ".mov", LucideIcon.FilePlay }, { ".wmv", LucideIcon.FilePlay }, { ".flv", LucideIcon.FilePlay },
        { ".webm", LucideIcon.FilePlay }, { ".m4v", LucideIcon.FilePlay }, { ".mpg", LucideIcon.FilePlay },
        { ".mpeg", LucideIcon.FilePlay }, { ".ts", LucideIcon.FilePlay },
        // Image
        { ".png", LucideIcon.FileImage }, { ".jpg", LucideIcon.FileImage }, { ".jpeg", LucideIcon.FileImage },
        { ".gif", LucideIcon.FileImage }, { ".bmp", LucideIcon.FileImage }, { ".svg", LucideIcon.FileImage },
        { ".webp", LucideIcon.FileImage }, { ".ico", LucideIcon.FileImage }, { ".tiff", LucideIcon.FileImage },
        { ".tif", LucideIcon.FileImage }, { ".raw", LucideIcon.FileImage }, { ".psd", LucideIcon.FileImage },
        // Audio
        { ".mp3", LucideIcon.FileMusic }, { ".wav", LucideIcon.FileMusic }, { ".flac", LucideIcon.FileMusic },
        { ".aac", LucideIcon.FileMusic }, { ".ogg", LucideIcon.FileMusic }, { ".wma", LucideIcon.FileMusic },
        { ".m4a", LucideIcon.FileMusic }, { ".opus", LucideIcon.FileMusic }, { ".mid", LucideIcon.FileMusic },
        { ".midi", LucideIcon.FileMusic },
        // Code / script
        { ".cs", LucideIcon.FileCode }, { ".js", LucideIcon.FileCode }, { ".py", LucideIcon.FileCode },
        { ".java", LucideIcon.FileCode }, { ".cpp", LucideIcon.FileCode }, { ".c", LucideIcon.FileCode },
        { ".h", LucideIcon.FileCode }, { ".rs", LucideIcon.FileCode }, { ".go", LucideIcon.FileCode },
        { ".rb", LucideIcon.FileCode }, { ".php", LucideIcon.FileCode }, { ".swift", LucideIcon.FileCode },
        { ".kt", LucideIcon.FileCode }, { ".lua", LucideIcon.FileCode }, { ".sh", LucideIcon.FileCode },
        { ".bat", LucideIcon.FileCode }, { ".ps1", LucideIcon.FileCode }, { ".cmd", LucideIcon.FileCode },
        { ".tsx", LucideIcon.FileCode }, { ".jsx", LucideIcon.FileCode }, { ".vue", LucideIcon.FileCode },
        { ".html", LucideIcon.FileCode }, { ".css", LucideIcon.FileCode }, { ".scss", LucideIcon.FileCode },
        { ".xml", LucideIcon.FileCode }, { ".json", LucideIcon.FileCode }, { ".yaml", LucideIcon.FileCode },
        { ".yml", LucideIcon.FileCode }, { ".toml", LucideIcon.FileCode }, { ".ini", LucideIcon.FileCode },
        // Text / document
        { ".txt", LucideIcon.FileText }, { ".md", LucideIcon.FileText }, { ".rtf", LucideIcon.FileText },
        { ".log", LucideIcon.FileText }, { ".csv", LucideIcon.FileText },
        // PDF
        { ".pdf", LucideIcon.FileText },
        // Spreadsheet
        { ".xls", LucideIcon.FileSpreadsheet }, { ".xlsx", LucideIcon.FileSpreadsheet },
        { ".ods", LucideIcon.FileSpreadsheet },
        // Presentation
        { ".ppt", LucideIcon.Presentation }, { ".pptx", LucideIcon.Presentation },
        { ".odp", LucideIcon.Presentation },
        // Archive
        { ".zip", LucideIcon.FileArchive }, { ".rar", LucideIcon.FileArchive },
        { ".7z", LucideIcon.FileArchive }, { ".tar", LucideIcon.FileArchive },
        { ".gz", LucideIcon.FileArchive }, { ".bz2", LucideIcon.FileArchive },
        { ".xz", LucideIcon.FileArchive },
        // Database
        { ".db", LucideIcon.Database }, { ".sqlite", LucideIcon.Database },
        { ".sql", LucideIcon.Database }, { ".mdb", LucideIcon.Database },
        // Word processing
        { ".doc", LucideIcon.FileText }, { ".docx", LucideIcon.FileText },
        { ".odt", LucideIcon.FileText },
        // Web
        { ".url", LucideIcon.Globe },
        // Disc image
        { ".iso", LucideIcon.FileArchive },
    };
}
