using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace Bloom.ViewModels;

public partial class ChangelogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "What's New";

    public List<ChangelogVersionGroup> Versions { get; }

    public ChangelogViewModel()
    {
        Versions = LoadChangelog();
    }

    private static List<ChangelogVersionGroup> LoadChangelog()
    {
        try
        {
            var uri = new Uri("avares://Bloom/Assets/changelog.json");
            using var stream = Avalonia.Platform.AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var entries = JsonSerializer.Deserialize<List<ChangelogJsonVersion>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries == null) return [];

            return entries.Select(v => new ChangelogVersionGroup
            {
                Version = $"v{v.Version}",
                Date = v.Date,
                Items = v.Entries
                    .OrderBy(e => e.Type switch
                    {
                        "feature" => 0,
                        "improvement" => 1,
                        "fix" => 2,
                        _ => 3
                    })
                    .Select(e => new ChangelogDisplayItem
                    {
                        Prefix = e.Type switch
                        {
                            "feature" => "NEW",
                            "improvement" => "IMPROVED",
                            "fix" => "FIXED",
                            _ => ""
                        },
                        Text = e.Text,
                        Type = e.Type
                    }).ToList()
            }).ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load changelog");
            return [];
        }
    }

    private sealed class ChangelogJsonVersion
    {
        public string Version { get; set; } = "";
        public string Date { get; set; } = "";
        public List<ChangelogJsonEntry> Entries { get; set; } = [];
    }

    private sealed class ChangelogJsonEntry
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }
}

public class ChangelogVersionGroup
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";
    public List<ChangelogDisplayItem> Items { get; set; } = [];
}

public class ChangelogDisplayItem
{
    public string Prefix { get; set; } = "";
    public string Text { get; set; } = "";
    public string Type { get; set; } = "";
}

public static class ChangelogConverters
{
    public static readonly IValueConverter IsFeature =
        new FuncValueConverter<string, bool>(t => t == "feature");
    public static readonly IValueConverter IsImprovement =
        new FuncValueConverter<string, bool>(t => t == "improvement");
    public static readonly IValueConverter IsFix =
        new FuncValueConverter<string, bool>(t => t == "fix");
}
