using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Serilog;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views;

internal sealed class BloomDragHandler
{
    private readonly Window _owner;
    private readonly Border _bloomButton;
    private readonly Func<BloomContext?> _getActiveBloom;
    private readonly Func<BloomContext, Task> _toggleAppBloom;
    private readonly Func<BloomContext, Task> _toggleSettingsBloom;
    private readonly Action _flushSavePosition;
    private readonly Func<BloomContext, (double biasAngleDeg, double spreadDeg)> _computeEdgeAwareness;
    private readonly Func<MainWindowViewModel?> _getVm;
    private readonly Action<BloomContext> _snapCloseBloom;

    // ── Drag state ──
    private PointerPressedEventArgs? _lastPointerPressed;
    private Point _pressPosition;
    private bool _isDragging;
    private bool _rightClickPending;
    private PixelPoint _bloomDragScreenStart;
    private PixelPoint _bloomDragWindowStart;
    private PixelPoint _dragPetalStart;
    private const double DragThreshold = 4;

    public BloomDragHandler(
        Window owner,
        Border bloomButton,
        Func<BloomContext?> getActiveBloom,
        Func<BloomContext, Task> toggleAppBloom,
        Func<BloomContext, Task> toggleSettingsBloom,
        Action flushSavePosition,
        Func<BloomContext, (double, double)> computeEdgeAwareness,
        Func<MainWindowViewModel?> getVm,
        Action<BloomContext> snapCloseBloom)
    {
        _owner = owner;
        _bloomButton = bloomButton;
        _getActiveBloom = getActiveBloom;
        _toggleAppBloom = toggleAppBloom;
        _toggleSettingsBloom = toggleSettingsBloom;
        _flushSavePosition = flushSavePosition;
        _computeEdgeAwareness = computeEdgeAwareness;
        _getVm = getVm;
        _snapCloseBloom = snapCloseBloom;

        bloomButton.PointerPressed += OnBloomPointerPressed;
        bloomButton.PointerMoved += OnBloomPointerMoved;
        bloomButton.PointerReleased += OnBloomPointerReleased;
    }

    private void OnBloomPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_owner);
        if (point.Properties.IsLeftButtonPressed)
        {
            _lastPointerPressed = e;
            _pressPosition = e.GetPosition(_owner);
            _isDragging = false;
            _rightClickPending = false;
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            _rightClickPending = true;
            e.Handled = true;
        }
    }

    private void OnBloomPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPointerPressed == null) return;

        if (!_isDragging)
        {
            var current = e.GetPosition(_owner);
            if (Math.Abs(current.X - _pressPosition.X) > DragThreshold ||
                Math.Abs(current.Y - _pressPosition.Y) > DragThreshold)
            {
                _isDragging = true;
                _bloomDragScreenStart = _owner.PointToScreen(e.GetPosition(_owner));
                _bloomDragWindowStart = _owner.Position;

                var activeBloom = _getActiveBloom();
                if (activeBloom != null)
                {
                    _dragPetalStart = activeBloom.Window.Position;

                    // If bloom is still animating, snap to final state so drag can track it.
                    // IsHitTestVisible distinguishes direction: true = opening, false = closing.
                    if (!activeBloom.IsExpanded && activeBloom.PetalItems.Count > 0)
                    {
                        bool isOpening = activeBloom.PetalItems[0].IsHitTestVisible;
                        if (isOpening)
                            BloomAnimator.SnapToExpanded(activeBloom);
                        else
                            _snapCloseBloom(activeBloom);
                    }
                }

                e.Pointer.Capture(_bloomButton);
                Log.Information("[Position] Manual drag started — Position=({X},{Y})", _owner.Position.X, _owner.Position.Y);
            }
        }

        if (_isDragging)
        {
            var currentScreen = _owner.PointToScreen(e.GetPosition(_owner));
            int dx = currentScreen.X - _bloomDragScreenStart.X;
            int dy = currentScreen.Y - _bloomDragScreenStart.Y;
            _owner.Position = new PixelPoint(_bloomDragWindowStart.X + dx, _bloomDragWindowStart.Y + dy);

            var activeBloom = _getActiveBloom();
            if (activeBloom is { IsExpanded: true } ctx)
            {
                ctx.Window.Position = new PixelPoint(
                    _dragPetalStart.X + dx, _dragPetalStart.Y + dy);

                var (newBias, newSpread) = _computeEdgeAwareness(ctx);
                if (Math.Abs(newBias - ctx.LastBias) > 3 ||
                    Math.Abs(newSpread - ctx.LastSpread) > 3 ||
                    double.IsNaN(ctx.LastBias))
                {
                    ctx.LastBias = newBias;
                    ctx.LastSpread = newSpread;
                    PetalLayoutEngine.LayoutPetals(ctx, newBias, newSpread);

                    for (int i = 0; i < ctx.PetalItems.Count; i++)
                    {
                        var (px, py) = ctx.PetalPositions[i];
                        ctx.PetalItems[i].RenderTransform =
                            TransformOperations.Parse(
                                $"translate({PetalFactory.Fmt(px)}px,{PetalFactory.Fmt(py)}px) scale(1)");
                    }
                }
            }

            e.Handled = true;
        }
    }

    private void OnBloomPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_rightClickPending)
        {
            _rightClickPending = false;
            var vm = _getVm();
            if (vm != null)
                _ = vm.CheckForUpdatesOnMenuOpenAsync();
            _ = _toggleSettingsBloom(null!);
            e.Handled = true;
            return;
        }

        if (_isDragging)
        {
            e.Pointer.Capture(null);
            Log.Information("[Position] Manual drag ended — Position=({X},{Y})", _owner.Position.X, _owner.Position.Y);
            _flushSavePosition();
        }
        else if (_lastPointerPressed != null)
        {
            var vm = _getVm();
            if (vm != null)
                _ = vm.CheckForUpdatesOnMenuOpenAsync();
            _ = _toggleAppBloom(null!);
        }

        _isDragging = false;
        _lastPointerPressed = null;
    }
}
