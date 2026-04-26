using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace Bloom.Services;

public class UpdateService : IUpdateService
{
    private const string UpdateUrl = "https://bloom.viov.nl/updates/";
    private readonly UpdateManager _mgr = new(UpdateUrl);
    private UpdateInfo? _updateInfo;

    public bool IsUpdateAvailable => _updateInfo != null;

    public string? NewVersion => _updateInfo?.TargetFullRelease.Version.ToString();

    public string CurrentVersion => _mgr.IsInstalled
        ? _mgr.CurrentVersion?.ToString() ?? AssemblyVersion
        : AssemblyVersion;

    private static string AssemblyVersion =>
        typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task<bool> CheckForUpdatesAsync()
    {
        if (!_mgr.IsInstalled) return false;
        try
        {
            _updateInfo = await _mgr.CheckForUpdatesAsync();
            return _updateInfo != null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to check for updates");
            return false;
        }
    }

    public async Task DownloadAsync(Action<int>? progress = null, CancellationToken ct = default)
    {
        if (_updateInfo == null) return;
        await _mgr.DownloadUpdatesAsync(_updateInfo, progress, ct);
    }

    public void ApplyAndRestart()
    {
        if (_updateInfo == null) return;
        _mgr.ApplyUpdatesAndRestart(_updateInfo.TargetFullRelease);
    }
}
