namespace Bloom.Services;

/// <summary>
/// Simple service locator providing testability seams without DI framework overhead.
/// ViewModels access services through these properties instead of direct static calls.
/// </summary>
internal static class ServiceLocator
{
    internal static IUpdateService Update { get; set; } = new UpdateService();
    internal static IConfigService Config { get; set; } = new ConfigServiceInstance();
    internal static IProcessLauncher Process { get; set; } = new ProcessLauncherInstance();
    internal static IStartupService Startup { get; set; } = new StartupServiceInstance();
    internal static IApplicationService Application { get; set; } = new ApplicationService();
    internal static IIconExtractorService IconExtractor { get; set; } = new IconExtractorServiceInstance();
    internal static IFileSystemService FileSystem { get; set; } = new FileSystemServiceInstance();

    /// <summary>Instance wrapper around static ConfigService.</summary>
    private sealed class ConfigServiceInstance : IConfigService
    {
        public Models.BloomConfig Load() => ConfigService.Load();
        public void Save(Models.BloomConfig config) => ConfigService.Save(config);
    }

    /// <summary>Instance wrapper around static ProcessLauncher.</summary>
    private sealed class ProcessLauncherInstance : IProcessLauncher
    {
        public void Launch(string fileName, string? arguments = null, string? workingDirectory = null)
            => ProcessLauncher.Launch(fileName, arguments, workingDirectory);
    }

    /// <summary>Instance wrapper around static StartupService.</summary>
    private sealed class StartupServiceInstance : IStartupService
    {
        public void SetStartWithWindows(bool enabled)
            => StartupService.SetStartWithWindows(enabled);
    }

    /// <summary>Instance wrapper around static IconExtractorService.</summary>
    private sealed class IconExtractorServiceInstance : IIconExtractorService
    {
        public string? ExtractIconAsBase64(string filePath)
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows))
                return null;
            return IconExtractorService.ExtractIconAsBase64(filePath);
        }
    }

    /// <summary>Instance wrapper for file system operations.</summary>
    private sealed class FileSystemServiceInstance : IFileSystemService
    {
        public bool FileExists(string path) => System.IO.File.Exists(path);
        public bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
    }
}
