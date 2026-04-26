using System.Collections.Generic;
using System.Linq;
using Bloom.Models;
using Bloom.Services;

namespace Bloom.Views;

internal static class PetalConverter
{
    internal static PetalItem[] ConvertToPetals(IEnumerable<BloomItem> items)
    {
        return items.Select(item => new PetalItem
        {
            Label = item.Label,
            ProcessName = item.Path,
            Arguments = item.Arguments,
            WorkingDirectory = item.WorkingDirectory,
            ShortcutType = item.Type,
            IconColor = item.IconColor,
            IconPath = item.IconSource == IconSource.BuiltIn
                ? (LucideIcon.TryFromName(item.BuiltInIconKey, out var icon) ? icon.PathData : "")
                : "",
            BitmapIconBase64 = item.IconSource switch
            {
                IconSource.Auto => item.AutoIconData,
                IconSource.File => item.FileIconData,
                _ => null
            },
            SourceGroup = item.Type == ShortcutType.Group ? item : null,
            SourceItemId = item.Id
        }).ToArray();
    }

    internal static PetalItem[] ConvertToPetalsForGroup(List<string> childIds, IEnumerable<BloomItem> allItems)
    {
        var backPetal = new PetalItem
        {
            Label = "Back",
            ProcessName = "@@back",
            IconColor = "#868E96",
            IconPath = LucideIcon.TryFromName("undo-2", out var backIcon) ? backIcon.PathData : "",
            ShortcutType = ShortcutType.Action
        };

        var lookup = allItems.ToDictionary(i => i.Id);
        var resolvedChildren = childIds
            .Where(id => lookup.ContainsKey(id))
            .Select(id => lookup[id]);

        var childPetals = ConvertToPetals(resolvedChildren);
        var result = new PetalItem[1 + childPetals.Length];
        result[0] = backPetal;
        childPetals.CopyTo(result, 1);
        return result;
    }
}
