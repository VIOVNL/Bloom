using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Bloom.Services;

public static class WindowsInteropService
{
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    public static void Lock()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            LockWorkStation();
    }

    public static void EmptyRecycleBin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000001);
    }

    public static void SimulateKeys(params byte[] vkCodes)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        const uint KEYUP = 0x0002;
        foreach (var vk in vkCodes)
            keybd_event(vk, 0, 0, 0);
        for (int i = vkCodes.Length - 1; i >= 0; i--)
            keybd_event(vkCodes[i], 0, KEYUP, 0);
    }

    public static void SimulateShortcut(string shortcut)
    {
        var parts = shortcut.Split('+');
        var vkCodes = new List<byte>();
        foreach (var part in parts)
        {
            var vk = MapKeyToVk(part.Trim());
            if (vk != 0) vkCodes.Add(vk);
        }
        if (vkCodes.Count == 0) return;
        SimulateKeys(vkCodes.ToArray());
    }

    private static byte MapKeyToVk(string key)
    {
        if (key.Length == 1 && char.IsLetter(key[0]))
            return (byte)char.ToUpper(key[0]);
        if (key.Length == 1 && char.IsDigit(key[0]))
            return (byte)key[0];

        return key switch
        {
            "Ctrl" => 0x11,
            "Shift" => 0x10,
            "Alt" => 0x12,
            "Win" => 0x5B,
            "Enter" => 0x0D,
            "Esc" => 0x1B,
            "Tab" => 0x09,
            "Space" => 0x20,
            "Backspace" => 0x08,
            "Delete" => 0x2E,
            "Insert" => 0x2D,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "Up" => 0x26,
            "Down" => 0x28,
            "Left" => 0x25,
            "Right" => 0x27,
            "PrintScreen" => 0x2C,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "." => 0xBE,
            "," => 0xBC,
            "=" => 0xBB,
            "-" => 0xBD,
            "[" => 0xDB,
            "]" => 0xDD,
            "\\" => 0xDC,
            ";" => 0xBA,
            "'" => 0xDE,
            "`" => 0xC0,
            "/" => 0xBF,
            "Num0" => 0x60, "Num1" => 0x61, "Num2" => 0x62, "Num3" => 0x63,
            "Num4" => 0x64, "Num5" => 0x65, "Num6" => 0x66, "Num7" => 0x67,
            "Num8" => 0x68, "Num9" => 0x69,
            "Num*" => 0x6A, "Num+" => 0x6B, "Num-" => 0x6D, "Num/" => 0x6F,
            "Num." => 0x6E,
            _ => 0
        };
    }
}
