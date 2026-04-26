using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Serilog;

namespace Bloom.Services;

internal static class HotkeyService
{
    public const int HOTKEY_PETALS = 1;
    public const int HOTKEY_BLOOM  = 2;
    public const int HOTKEY_ITEM_BASE = 100;

    public const uint MOD_ALT   = 0x0001;
    public const uint MOD_CTRL  = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN   = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool Register(IntPtr hwnd, int id, uint modifiers, uint vk)
    {
        Unregister(hwnd, id);
        var ok = RegisterHotKey(hwnd, id, modifiers | MOD_NOREPEAT, vk);
        if (!ok)
            Log.Warning("[Hotkey] RegisterHotKey failed (id={Id}, mod=0x{Mod:X}, vk=0x{Vk:X})", id, modifiers, vk);
        else
            Log.Information("[Hotkey] Registered hotkey (id={Id}, mod=0x{Mod:X}, vk=0x{Vk:X})", id, modifiers, vk);
        return ok;
    }

    public static void Unregister(IntPtr hwnd, int id)
    {
        UnregisterHotKey(hwnd, id);
    }

    public static void UnregisterAll(IntPtr hwnd, int itemHotkeyCount = 0)
    {
        UnregisterHotKey(hwnd, HOTKEY_PETALS);
        UnregisterHotKey(hwnd, HOTKEY_BLOOM);
        for (int i = 0; i < itemHotkeyCount; i++)
            UnregisterHotKey(hwnd, HOTKEY_ITEM_BASE + i);
    }

    public static bool Parse(string? combo, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(combo)) return false;

        var parts = combo.Split('+');
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            var upper = part.ToUpperInvariant();

            if (upper is "ALT")        { modifiers |= MOD_ALT; continue; }
            if (upper is "CTRL" or "CONTROL") { modifiers |= MOD_CTRL; continue; }
            if (upper is "SHIFT")      { modifiers |= MOD_SHIFT; continue; }
            if (upper is "WIN" or "SUPER")  { modifiers |= MOD_WIN; continue; }

            if (VkMap.TryGetValue(upper, out var mapped))
            {
                vk = mapped;
                continue;
            }

            // Single letter A-Z
            if (upper.Length == 1 && upper[0] >= 'A' && upper[0] <= 'Z')
            {
                vk = (uint)upper[0]; // VK_A = 0x41 = 'A'
                continue;
            }

            // Single digit 0-9
            if (upper.Length == 1 && upper[0] >= '0' && upper[0] <= '9')
            {
                vk = (uint)upper[0]; // VK_0 = 0x30 = '0'
                continue;
            }

            return false; // unknown token
        }

        return modifiers != 0 && vk != 0;
    }

    public static string Format(uint modifiers, uint vk)
    {
        var parts = new List<string>();
        if ((modifiers & MOD_CTRL)  != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT)   != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN)   != 0) parts.Add("Win");

        // Reverse lookup VK
        foreach (var (name, code) in VkMap)
        {
            if (code == vk)
            {
                parts.Add(CapitalizeFirst(name));
                return string.Join(" + ", parts);
            }
        }

        // Letter or digit
        if (vk >= 'A' && vk <= 'Z')
        {
            parts.Add(((char)vk).ToString());
            return string.Join(" + ", parts);
        }
        if (vk >= '0' && vk <= '9')
        {
            parts.Add(((char)vk).ToString());
            return string.Join(" + ", parts);
        }

        parts.Add($"0x{vk:X2}");
        return string.Join(" + ", parts);
    }

    private static string CapitalizeFirst(string s)
    {
        if (s.Length <= 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    private static readonly Dictionary<string, uint> VkMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Function keys
        { "F1",  0x70 }, { "F2",  0x71 }, { "F3",  0x72 }, { "F4",  0x73 },
        { "F5",  0x74 }, { "F6",  0x75 }, { "F7",  0x76 }, { "F8",  0x77 },
        { "F9",  0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
        // Common keys
        { "SPACE",     0x20 }, { "ENTER",     0x0D }, { "RETURN",    0x0D },
        { "TAB",       0x09 }, { "ESCAPE",    0x1B }, { "ESC",       0x1B },
        { "BACKSPACE", 0x08 }, { "DELETE",    0x2E }, { "DEL",       0x2E },
        { "INSERT",    0x2D }, { "HOME",      0x24 }, { "END",       0x23 },
        { "PAGEUP",    0x21 }, { "PAGEDOWN",  0x22 },
        { "UP",        0x26 }, { "DOWN",      0x28 },
        { "LEFT",      0x25 }, { "RIGHT",     0x27 },
        // Punctuation / OEM
        { "OEM_PLUS",    0xBB }, { "OEM_MINUS",   0xBD },
        { "OEM_COMMA",   0xBC }, { "OEM_PERIOD",  0xBE },
        { "OEM_1",       0xBA }, // ; :
        { "OEM_2",       0xBF }, // / ?
        { "OEM_3",       0xC0 }, // ` ~
        { "OEM_4",       0xDB }, // [ {
        { "OEM_5",       0xDC }, // \ |
        { "OEM_6",       0xDD }, // ] }
        { "OEM_7",       0xDE }, // ' "
    };
}
