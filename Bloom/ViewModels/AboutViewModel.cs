using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Bloom.Messages;
using Bloom.Services;

namespace Bloom.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly IProcessLauncher _processLauncher;

    [ObservableProperty]
    private string _versionText = "v1.0.0";

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private string _updateButtonText = "Check for Updates";

    [ObservableProperty]
    private bool _isChecking;

    public AboutViewModel(IUpdateService updateService, IProcessLauncher processLauncher)
    {
        _updateService = updateService;
        _processLauncher = processLauncher;
        _versionText = $"v{updateService.CurrentVersion}";
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        UpdateButtonText = "Checking...";
        UpdateStatusText = "";

        try
        {
            var hasUpdate = await _updateService.CheckForUpdatesAsync();
            if (hasUpdate)
            {
                UpdateStatusText = $"Update available: v{_updateService.NewVersion}";
                UpdateButtonText = "Update Available!";
                WeakReferenceMessenger.Default.Send(new UpdateAvailableNotification());
            }
            else
            {
                UpdateStatusText = "You're on the latest version";
                UpdateButtonText = "Check for Updates";
            }
        }
        catch
        {
            UpdateStatusText = "Could not check for updates";
            UpdateButtonText = "Check for Updates";
        }

        IsChecking = false;
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        _processLauncher.Launch(url);
    }
}
