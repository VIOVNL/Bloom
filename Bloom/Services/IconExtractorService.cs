using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Bloom.Services;

[SupportedOSPlatform("windows")]
public static class IconExtractorService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Extracts the main icon from an exe or ico file and returns it as Base64-encoded PNG.
    /// Returns null if extraction fails.
    /// </summary>
    public static string? ExtractIconAsBase64(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".ico")
                return ConvertIcoToBase64(filePath);

            var hIcon = ExtractIcon(IntPtr.Zero, filePath, 0);
            if (hIcon == IntPtr.Zero || hIcon == (IntPtr)1)
                return null;

            try
            {
                using var icon = System.Drawing.Icon.FromHandle(hIcon);
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Failed to extract icon from {Path}", filePath);
            return null;
        }
    }

    private static string? ConvertIcoToBase64(string icoPath)
    {
        try
        {
            using var icon = new System.Drawing.Icon(icoPath);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Failed to convert .ico to base64: {Path}", icoPath);
            return null;
        }
    }
}
