using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Bloom.Helpers;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

public partial class UpdateWindow : Window
{
    private Border _progressFill = null!;
    private Border _cancelBtn = null!;

    public UpdateWindow() : this(ServiceLocator.Update) { }

    public UpdateWindow(IUpdateService updateService)
    {
        var vm = new UpdateWindowViewModel(updateService);
        DataContext = vm;

        InitializeComponent();

        _progressFill = this.FindControl<Border>("ProgressFill")!;
        _cancelBtn = this.FindControl<Border>("CancelBtn")!;

        _cancelBtn.PointerPressed += (_, e) =>
        {
            vm.CancelCommand.Execute(null);
            e.Handled = true;
        };
        KeyboardHelper.WireActivate(_cancelBtn, () => vm.CancelCommand.Execute(null));

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) vm.CancelCommand.Execute(null);
        };

        // Bind progress width from ViewModel
        vm.PropertyChanged += OnUpdateVmPropertyChanged;
        Closed += (_, _) => vm.PropertyChanged -= OnUpdateVmPropertyChanged;

        // Drag support
        var versionInfo = this.FindControl<TextBlock>("VersionInfo")!;
        var shell = versionInfo.Parent as StackPanel;
        if (shell?.Parent is Border border)
            border.PointerPressed += OnShellPressed;

        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not UpdateWindowViewModel vm) return;

        var track = _progressFill.Parent as Border;
        vm.TrackWidth = track?.Bounds.Width ?? 280;
        if (vm.TrackWidth <= 0) vm.TrackWidth = 280;

        _ = vm.StartDownloadAsync();
    }

    private void OnUpdateVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateWindowViewModel.IsCancelled)
            && sender is UpdateWindowViewModel { IsCancelled: true })
            Close();
    }

    private void OnShellPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
