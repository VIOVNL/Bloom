using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bloom.Services;

public interface IUpdateService
{
    bool IsUpdateAvailable { get; }
    string? NewVersion { get; }
    string CurrentVersion { get; }
    Task<bool> CheckForUpdatesAsync();
    Task DownloadAsync(Action<int>? progress = null, CancellationToken ct = default);
    void ApplyAndRestart();
}
