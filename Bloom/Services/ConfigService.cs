using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Bloom.Models;

namespace Bloom.Services;

public static class ConfigService
{
    private static readonly string ConfigPath = GetConfigPath();
    private static BloomConfig? _cached;
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var bloomDir = Path.Combine(appData, "viovnl", "bloom");
        Directory.CreateDirectory(bloomDir);
        return Path.Combine(bloomDir, "Bloom.json");
    }

    public static BloomConfig Load()
    {
        lock (_lock)
        {
            if (_cached != null) return _cached;

            if (!File.Exists(ConfigPath))
            {
                _cached = CreateDefaultConfig();
                SaveInternal(_cached);
                return _cached;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                _cached = JsonSerializer.Deserialize<BloomConfig>(json, JsonOptions)
                          ?? new BloomConfig();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load config from {Path}", ConfigPath);
                _cached = new BloomConfig();
            }

            if (_cached.Settings.FirstLaunch)
            {
                _cached = CreateDefaultConfig();
                SaveInternal(_cached);
            }

            return _cached;
        }
    }

    public static void Save(BloomConfig config)
    {
        lock (_lock)
        {
            _cached = config;
            SaveInternal(config);
        }
    }

    private static void SaveInternal(BloomConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save config to {Path}", ConfigPath);
        }
    }

    private static BloomConfig CreateDefaultConfig()
    {
        return new BloomConfig
        {
            Settings = new BloomSettings
            {
                Theme = AppTheme.Dark,
                StartWithWindows = true,
                AutoUpdate = true,
                FirstLaunch = true
            },
            Items = new List<BloomItem>
            {
                new() { Label = "Browser",    Type = ShortcutType.Software, Path = "chrome",
                         IconSource = IconSource.BuiltIn, BuiltInIconKey = "globe",       IconColor = "#4285F4" },
                new() { Label = "Terminal",   Type = ShortcutType.Software, Path = "cmd",
                         IconSource = IconSource.BuiltIn, BuiltInIconKey = "terminal",    IconColor = "#4CAF50" },
                new() { Label = "Documents",  Type = ShortcutType.Folder,   Path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                         IconSource = IconSource.BuiltIn, BuiltInIconKey = "folder",      IconColor = "#FF9800" },
                new() { Label = "Notepad",    Type = ShortcutType.Software, Path = "notepad",
                         IconSource = IconSource.BuiltIn, BuiltInIconKey = "file",        IconColor = "#FFC107" },
            }
        };
    }
}
