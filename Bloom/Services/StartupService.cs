using System;
using System.Runtime.InteropServices;

namespace Bloom.Services;

internal static class StartupService
{
    internal static void SetStartWithWindows(bool enabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string valueName = "Bloom";

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to set startup registry key");
        }
    }
}
