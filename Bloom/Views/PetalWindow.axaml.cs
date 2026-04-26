using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Serilog;

namespace Bloom.Views;

public partial class PetalWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private const uint LWA_ALPHA = 0x02;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private DispatcherTimer? _hitTestTimer;
    private bool _isClickThrough;
    private bool _layeredModeApplied;
    private IntPtr _ownerHwnd;

    public bool AlwaysOnTop { get; set; } = true;

    public PetalWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set the MainWindow handle so the tick can re-assert it above the petal window.
    /// Must be called after the owner window is shown (platform handle available).
    /// </summary>
    public void SetOwnerHandle(Window owner)
    {
        _ownerHwnd = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }

    /// <summary>
    /// Switch from DComp (WS_EX_NOREDIRECTIONBITMAP) to legacy layered window mode
    /// so that WS_EX_TRANSPARENT actually provides cross-process click-through.
    /// </summary>
    private void EnsureLayeredMode()
    {
        if (_layeredModeApplied) return;

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        Log.Information("[ClickThrough] Before swap: exStyle=0x{Style:X8}", exStyle);

        // Remove DComp, add legacy Layered
        exStyle &= ~WS_EX_NOREDIRECTIONBITMAP;
        exStyle |= WS_EX_LAYERED;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Initialize layered window as fully opaque so Avalonia content stays visible
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

        // MSDN: after SetWindowLong, call SetWindowPos with SWP_FRAMECHANGED to apply.
        // Also re-assert HWND_TOPMOST so the z-order isn't lost by the style change.
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        int verify = GetWindowLong(hwnd, GWL_EXSTYLE);
        Log.Information("[ClickThrough] After swap: exStyle=0x{Style:X8} hasLayered={L} hasDComp={D}",
            verify, (verify & WS_EX_LAYERED) != 0, (verify & WS_EX_NOREDIRECTIONBITMAP) != 0);

        _layeredModeApplied = true;
    }

    public void EnableClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        EnsureLayeredMode();
        SetTransparentStyle(true);

        if (_hitTestTimer == null)
        {
            _hitTestTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            _hitTestTimer.Tick += OnHitTestTick;
        }
        _hitTestTimer.Start();
    }

    /// <summary>
    /// Stop the hit-test timer.  When <paramref name="keepTransparent"/> is true the
    /// WS_EX_TRANSPARENT style is left in place so clicks still pass through during
    /// the close animation.  The full cleanup happens in the final off-screen move.
    /// </summary>
    public void DisableClickThrough(bool keepTransparent = false)
    {
        _hitTestTimer?.Stop();
        if (!keepTransparent && _isClickThrough)
            SetTransparentStyle(false);
    }

    private int _tickLog;

    private int _zOrderTick;

    private void OnHitTestTick(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        // Re-assert z-order every ~1s (125 ticks at 8ms) instead of every tick
        // to avoid flickering.  Order: petal first, then owner so the bloom
        // button always renders on top of its petals.
        // When AlwaysOnTop is ON  → HWND_TOPMOST (above all windows).
        // When AlwaysOnTop is OFF → HWND_TOP     (bloom button still above petals,
        //                                          but not above unrelated windows).
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero && _zOrderTick++ % 125 == 0)
        {
            var zOrder = AlwaysOnTop ? HWND_TOPMOST : HWND_TOP;
            SetWindowPos(hwnd, zOrder, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            if (_ownerHwnd != IntPtr.Zero)
                SetWindowPos(_ownerHwnd, zOrder, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        if (!GetCursorPos(out var cursor)) return;

        try
        {
            var clientPoint = this.PointToClient(new PixelPoint(cursor.X, cursor.Y));
            var hit = this.InputHitTest(clientPoint);

            bool overContent = hit != null && hit != this && hit != PetalCanvas;

            if (_tickLog++ % 120 == 0)
            {
                string hitType = hit switch
                {
                    null => "null",
                    _ when hit == this => "Window",
                    _ when hit == PetalCanvas => "Canvas",
                    _ => hit.GetType().Name
                };
                Log.Debug("[ClickThrough] cursor=({X},{Y}) hit={Hit} overContent={OC} clickThrough={CT}",
                    cursor.X, cursor.Y, hitType, overContent, _isClickThrough);
            }

            if (overContent && _isClickThrough)
                SetTransparentStyle(false);
            else if (!overContent && !_isClickThrough)
                SetTransparentStyle(true);
        }
        catch
        {
            // Window might not be fully realized yet
        }
    }

    private void SetTransparentStyle(bool transparent)
    {
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (transparent)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        _isClickThrough = transparent;
    }
}
