using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Bloom.Services;

namespace Bloom.ViewModels;

public partial class UpdateWindowViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _statusText = "Ready to download";

    [ObservableProperty]
    private string _versionInfo = "";

    [ObservableProperty]
    private double _progressWidth;

    [ObservableProperty]
    private double _trackWidth = 280;

    [ObservableProperty]
    private bool _isCancelled;

    public UpdateWindowViewModel() : this(ServiceLocator.Update) { }

    public UpdateWindowViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
        _syncContext = SynchronizationContext.Current;
        VersionInfo = $"{_updateService.CurrentVersion} \u2192 {_updateService.NewVersion ?? "?"}";
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    public async Task StartDownloadAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            StatusText = "Downloading...";

            await _updateService.DownloadAsync(progress =>
            {
                _syncContext?.Post(_ =>
                {
                    ProgressPercent = progress;
                    ProgressWidth = TrackWidth * progress / 100.0;
                    StatusText = $"Downloading... {progress}%";
                }, null);
            }, _cts.Token);

            StatusText = "Applying update...";
            ProgressWidth = TrackWidth;

            _updateService.ApplyAndRestart();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            IsCancelled = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
}
