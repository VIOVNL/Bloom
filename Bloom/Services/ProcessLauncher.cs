using System;
using System.Diagnostics;
using Serilog;

namespace Bloom.Services;

internal static class ProcessLauncher
{
    internal static void Launch(string fileName, string? arguments = null, string? workingDirectory = null)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : workingDirectory;

            // Launch via "cmd /c start" so the process is fully independent
            // and does not appear as a child of Bloom in the process tree.
            var startArgs = string.IsNullOrEmpty(arguments)
                ? $"/c start \"\" \"{fileName}\""
                : $"/c start \"\" \"{fileName}\" {arguments}";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = startArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dir,
            })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to launch process: {FileName}", fileName);
        }
    }
}
