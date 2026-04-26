using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Bloom.Helpers;

internal static class KeyboardHelper
{
    /// <summary>
    /// Wires Enter/Space key activation on a Border acting as a button.
    /// </summary>
    internal static void WireActivate(Border border, Action handler)
    {
        border.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                handler();
            }
        };
    }
}
