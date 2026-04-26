using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using Bloom.Messages;
using Bloom.Models;
using Bloom.Services;
using Bloom.ViewModels;

namespace Bloom.Views
{
    public partial class MainWindow : Window
    {
        // ── Position save timer ──
        private CancellationTokenSource? _positionSaveCts;

        // ── Global hotkey ──
        private IntPtr _hotkeyHwnd;
        private readonly Dictionary<int, string> _itemHotkeyIdToItemId = new();
        private int _registeredItemHotkeyCount;

        // ── Bloom contexts ──
        private readonly BloomContext _appBloom;
        private readonly BloomContext _settingsBloom;
        private BloomContext? _activeBloom;
        private BloomContext? _closingBloom;
        private readonly SemaphoreSlim _bloomLock = new(1, 1);

        // ── Bloom button ──
        private Border _bloomButton = null!;
        private Viewbox _bloomIcon = null!;

        // ── ViewModel subscription tracking ──
        private MainWindowViewModel? _subscribedVm;
        private bool _openedOnce;

        // ── Group navigation ──
        private readonly Stack<BloomItem?> _groupNavStack = new();
        private BloomItem? _currentGroup;

        // ── Canvas size (computed once, shared by both blooms) ──
        private double _canvasSize;

        // ── Deactivation close delay ──
        private CancellationTokenSource? _deactivateCloseCts;

        // ── Services / handlers ──
        private readonly ToastService _toastService;
        private readonly DragDropHandler _dragDropHandler;
        private readonly BloomDragHandler _bloomDragHandler;
        private readonly DialogHandler _dialogHandler;

        // ── Settings petals (dynamic — Update petal appears only when available) ──
        private PetalItem[] GetSettingsPetals()
        {
            var petals = new List<PetalItem>
            {
                new() { Label = "Add Item",  IconColor = "#51CF66", ProcessName = "@@add",
                         IconPath = LucideIcon.FilePlus.PathData },
                new() { Label = "Add Group", IconColor = "#CC5DE8", ProcessName = "@@creategroup",
                         IconPath = LucideIcon.FolderPlus.PathData },
                new() { Label = "Settings", IconColor = "#339AF0", ProcessName = "@@bloomsettings",
                         IconPath = LucideIcon.Settings.PathData },
                new() { Label = "About",    IconColor = "#B197FC", ProcessName = "@@about",
                         IconPath = LucideIcon.Info.PathData },
                new() { Label = "Report Bug", IconColor = "#FF922B", ProcessName = "@@reportbug",
                         IconPath = LucideIcon.Bug.PathData },
                new() { Label = "Feature Request", IconColor = "#22B8CF", ProcessName = "@@featurerequest",
                         IconPath = LucideIcon.Lightbulb.PathData },
                new() { Label = "Docs", IconColor = "#74C0FC", ProcessName = "@@docs",
                         IconPath = LucideIcon.BookOpenText.PathData },
                new() { Label = "What's New", IconColor = "#FFD43B", ProcessName = "@@changelog",
                         IconPath = LucideIcon.Sparkles.PathData },
            };

            if (DataContext is MainWindowViewModel { IsUpdateAvailable: true, AutoUpdate: false })
            {
                petals.Add(new() { Label = "Update", IconColor = "#FFD43B", ProcessName = "@@update",
                                    IconPath = LucideIcon.Download.PathData });
            }

            petals.Add(new() { Label = "Exit", IconColor = "#FF6B6B", ProcessName = "@@exit",
                                IconPath = LucideIcon.LogOut.PathData });

#if DEBUG
            petals.Add(new() { Label = "Add 5 Test", IconColor = "#E599F7", ProcessName = "@@debug_add5",
                                IconPath = LucideIcon.TestTubes.PathData });
#endif

            return petals.ToArray();
        }

        // ────────────────────────────────────────────────────
        // Constructor
        // ────────────────────────────────────────────────────

        public MainWindow()
        {
            InitializeComponent();

            _bloomButton = this.FindControl<Border>("BloomButton")!;
            _bloomIcon = this.FindControl<Viewbox>("BloomIcon")!;
            _toastService = new ToastService(this);

            // Create TWO petal overlay windows
            var appWindow = new PetalWindow();
            var appCanvas = appWindow.FindControl<Canvas>("PetalCanvas")!;
            _appBloom = new BloomContext(appWindow, appCanvas);

            var settingsWindow = new PetalWindow();
            var settingsCanvas = settingsWindow.FindControl<Canvas>("PetalCanvas")!;
            _settingsBloom = new BloomContext(settingsWindow, settingsCanvas);

            // Main button handlers (drag, click, right-click)
            _bloomDragHandler = new BloomDragHandler(
                this, _bloomButton,
                () => _activeBloom ?? _closingBloom,
                _ => ToggleBloomAsync(_appBloom),
                _ => ToggleBloomAsync(_settingsBloom),
                () => FlushSavePosition(),
                ComputeEdgeAwareness,
                () => DataContext as MainWindowViewModel,
                SnapCloseBloom);

            // Canvas handlers for both windows
            appCanvas.PointerMoved += (_, e) => OnCanvasPointerMoved(_appBloom, e);
            appCanvas.PointerExited += (_, e) => OnCanvasPointerExited(_appBloom);
            appCanvas.PointerPressed += (_, e) => OnPetalCanvasPressed(e);

            settingsCanvas.PointerMoved += (_, e) => OnCanvasPointerMoved(_settingsBloom, e);
            settingsCanvas.PointerExited += (_, e) => OnCanvasPointerExited(_settingsBloom);
            settingsCanvas.PointerPressed += (_, e) => OnPetalCanvasPressed(e);

            // Deactivation: MainWindow is the reliable source (always active after Topmost toggle).
            // PetalWindow.Deactivated only fires when it was activated (e.g. user clicked a petal).
            this.Deactivated += OnWindowDeactivated;
            appWindow.Deactivated += OnWindowDeactivated;
            settingsWindow.Deactivated += OnWindowDeactivated;

            // Drag-and-drop from Windows Explorer (and petal windows)
            _dragDropHandler = new DragDropHandler(_bloomButton, () => DataContext as MainWindowViewModel, _toastService.Show, () => _currentGroup);
            _dragDropHandler.Attach(this);
            _dragDropHandler.Attach(appWindow);
            _dragDropHandler.Attach(settingsWindow);

            // Dialog handler
            _dialogHandler = new DialogHandler(
                this,
                () => DataContext as MainWindowViewModel,
                CloseActiveBloomAsync,
                CancelPendingDeactivationClose,
                () => _appBloom,
                () => _currentGroup);

            Closing += (_, _) =>
            {
                FlushSavePosition();
                _toastService.Dispose();
                if (_hotkeyHwnd != IntPtr.Zero)
                    HotkeyService.UnregisterAll(_hotkeyHwnd, _registeredItemHotkeyCount);
            };
            Closed += (_, _) =>
            {
                try { _appBloom.Window.Close(); } catch { }
                try { _settingsBloom.Window.Close(); } catch { }
            };

            // DataContext propagation
            _appBloom.Window.DataContext = DataContext;
            _settingsBloom.Window.DataContext = DataContext;

            // Navigation messages — delegates to DialogHandler
            WeakReferenceMessenger.Default.Register<AddItemRequestedMessage>(this, (r, _) => _dialogHandler.OnAddItemRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<CreateGroupRequestedMessage>(this, (r, _) => _dialogHandler.OnCreateGroupRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<SettingsRequestedMessage>(this, (r, _) => _dialogHandler.OnSettingsRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<AboutRequestedMessage>(this, (r, _) => _dialogHandler.OnAboutRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<UpdateRequestedMessage>(this, (r, _) => _dialogHandler.OnUpdateRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<EditItemRequestedMessage>(this, (r, m) => _dialogHandler.OnEditItemRequestedAsync(m).FireAndForget());
            WeakReferenceMessenger.Default.Register<ChangelogRequestedMessage>(this, (r, _) => _dialogHandler.OnChangelogRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<NavigateIntoGroupMessage>(this, (r, m) => NavigateIntoGroupAsync(m.Group).FireAndForget());
            WeakReferenceMessenger.Default.Register<NavigateBackRequestedMessage>(this, (r, _) => NavigateBackAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<SetPetalsHotkeyRequestedMessage>(this, (r, m) => ((MainWindow)r).RegisterHotkey(HotkeyService.HOTKEY_PETALS, m.Hotkey));
            WeakReferenceMessenger.Default.Register<SetBloomHotkeyRequestedMessage>(this, (r, m) => ((MainWindow)r).RegisterHotkey(HotkeyService.HOTKEY_BLOOM, m.Hotkey));
            WeakReferenceMessenger.Default.Register<HotkeyWindowRequestedMessage>(this, (r, _) => _dialogHandler.OnHotkeyWindowRequestedAsync().FireAndForget());
            WeakReferenceMessenger.Default.Register<ItemHotkeysChangedMessage>(this, (r, _) => ((MainWindow)r).RegisterAllItemHotkeys());

            DataContextChanged += (_, _) =>
            {
                _appBloom.Window.DataContext = DataContext;
                _settingsBloom.Window.DataContext = DataContext;
                SubscribeToViewModel(DataContext as MainWindowViewModel);
                if (DataContext is MainWindowViewModel dcVm)
                {
                    ApplyScale(dcVm.Scale);
                    ApplyAlwaysOnTop(dcVm.AlwaysOnTop);
                }
            };

            SubscribeToViewModel(DataContext as MainWindowViewModel);

            // Apply initial settings from config (if DataContext is already set)
            if (DataContext is MainWindowViewModel initVm)
            {
                ApplyScale(initVm.Scale);
                ApplyAlwaysOnTop(initVm.AlwaysOnTop);
            }

            // Restore saved position or center on screen
            Opened += (_, _) =>
            {
                if (_openedOnce) return;
                _openedOnce = true;

                if (DataContext is MainWindowViewModel vm && vm.WindowX.HasValue && vm.WindowY.HasValue)
                {
                    Log.Information("[Position] Restoring saved position: ({X},{Y})", vm.WindowX.Value, vm.WindowY.Value);
                    Position = new PixelPoint(vm.WindowX.Value, vm.WindowY.Value);
                }
                else
                {
                    Log.Information("[Position] No saved position, centering on screen");
                    CenterOnScreen();
                }

                // Wire owner handle now that the window is realized — ensures bloom
                // button always stays on top of petals via the hit-test tick.
                (_appBloom.Window as PetalWindow)?.SetOwnerHandle(this);
                (_settingsBloom.Window as PetalWindow)?.SetOwnerHandle(this);

                // Global hotkeys
                _hotkeyHwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (_hotkeyHwnd != IntPtr.Zero)
                {
                    Win32Properties.AddWndProcHookCallback(this, WndProcHook);
                    if (DataContext is MainWindowViewModel hkVm)
                    {
                        RegisterHotkey(HotkeyService.HOTKEY_PETALS, hkVm.PetalsHotkey);
                        RegisterHotkey(HotkeyService.HOTKEY_BLOOM, hkVm.BloomHotkey);
                        RegisterAllItemHotkeys();
                    }
                }

                _dialogHandler.ShowTutorialIfFirstLaunch().FireAndForget();
                _dialogHandler.ShowChangelogIfUpdated().FireAndForget();

                if (DataContext is MainWindowViewModel startupVm)
                    _ = startupVm.CheckForUpdatesOnMenuOpenAsync();
            };

            PositionChanged += (_, _) => DebounceSavePosition();
        }

        private void CenterOnScreen()
        {
            var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
            if (screen == null) return;
            var wa = screen.WorkingArea;
            var scaling = screen.Scaling;
            int w = (int)(Width * scaling);
            int h = (int)(Height * scaling);
            Position = new PixelPoint(
                wa.X + (wa.Width - w) / 2,
                wa.Y + (wa.Height - h) / 2);
        }

        private void DebounceSavePosition()
        {
            _positionSaveCts?.Cancel();
            _positionSaveCts = new CancellationTokenSource();
            var token = _positionSaveCts.Token;
            // Capture position and VM on UI thread before the async delay
            var pos = Position;
            var vm = DataContext as MainWindowViewModel;
            Task.Delay(300, token).ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;
                if (vm != null)
                {
                    vm.WindowX = pos.X;
                    vm.WindowY = pos.Y;
                    vm.SavePosition();
                }
            }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        private void FlushSavePosition([System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
        {
            _positionSaveCts?.Cancel();
            var pos = Position;
            Log.Information("[Position] FlushSavePosition from {Caller} — Position=({X},{Y})", caller, pos.X, pos.Y);
            if (DataContext is MainWindowViewModel vm)
            {
                vm.WindowX = pos.X;
                vm.WindowY = pos.Y;
                vm.SavePosition();
                Log.Information("[Position] Saved to config: ({X},{Y})", pos.X, pos.Y);
            }
            else
            {
                Log.Warning("[Position] FlushSavePosition — DataContext is null, skip save");
            }
        }

        private void SubscribeToViewModel(MainWindowViewModel? newVm)
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedVm.Items.CollectionChanged -= OnItemsCollectionChanged;
                _subscribedVm = null;
            }

            if (newVm == null) return;

            _subscribedVm = newVm;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            newVm.Items.CollectionChanged += OnItemsCollectionChanged;
            InitializePetals(newVm);
        }

        private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => OnItemsChanged();

        // ────────────────────────────────────────────────────
        // Initialize petals from ViewModel data
        // ────────────────────────────────────────────────────

        private void InitializePetals(MainWindowViewModel vm)
        {
            RecomputeCanvasSize();
            _appBloom.SourceItems = GetCurrentAppPetals(vm);
            _settingsBloom.SourceItems = GetSettingsPetals();
            BuildPetals(_appBloom, _appBloom.SourceItems, vm.LabelMode);
            BuildPetals(_settingsBloom, _settingsBloom.SourceItems, vm.LabelMode);
        }

        private PetalItem[] GetCurrentAppPetals(MainWindowViewModel vm)
        {
            if (_currentGroup != null)
                return PetalConverter.ConvertToPetalsForGroup(_currentGroup.ChildIds, vm.Items);

            return PetalConverter.ConvertToPetals(vm.Items.Where(i => !i.IsInGroup));
        }

        // Convert BloomItem[] → PetalItem[] — delegates to PetalConverter

        // ────────────────────────────────────────────────────
        // Bloom orchestrator
        // ────────────────────────────────────────────────────

        private async Task ToggleBloomAsync(BloomContext requested)
        {
            CancelPendingDeactivationClose();
            if (!await _bloomLock.WaitAsync(0)) return;
            try
            {
                if (_activeBloom == requested)
                {
                    await CloseActiveBloomAsync();
                    RehideIfNeeded();
                }
                else
                {
                    if (_activeBloom != null)
                    {
                        // Switching blooms — don't re-hide
                        _rehideAfterGroupClose = false;
                        await CloseActiveBloomAsync();
                    }

                    await OpenBloomAsync(requested);
                }
            }
            finally
            {
                _bloomLock.Release();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private bool _hotkeyTriggered;

        private void MoveBloomToCursorIfEnabled()
        {
            if (!_hotkeyTriggered) return;
            if (DataContext is not MainWindowViewModel vm || !vm.ShowBloomAtCursor) return;
            if (!GetCursorPos(out var cursor)) return;

            var scaling = Screens.ScreenFromVisual(this)?.Scaling ?? 1.0;
            int halfBtn = (int)(PetalLayoutEngine.ButtonSize * scaling / 2);
            Position = new PixelPoint(cursor.X - halfBtn, cursor.Y - halfBtn);
        }

        private async Task OpenBloomAsync(BloomContext ctx)
        {
            // Always rebuild app bloom on open so stale group petals are cleared
            if (ctx == _appBloom && DataContext is MainWindowViewModel vm2)
                RebuildPetals(_appBloom, GetCurrentAppPetals(vm2));

            if (ctx.PetalItems.Count == 0) return;

            MoveBloomToCursorIfEnabled();
            _hotkeyTriggered = false;

            // Activate so Deactivated fires when user clicks away (auto-unbloom)
            if (!IsActive) Activate();

            _activeBloom = ctx;
            _bloomButton.Classes.Add("open");

            if (DataContext is MainWindowViewModel vm)
                vm.IsMenuOpen = true;

            await AnimateBloomAsync(ctx, true);
        }

        private async Task CloseActiveBloomAsync()
        {
            CancelPendingDeactivationClose();
            if (_activeBloom == null) return;

            var closing = _activeBloom;
            _activeBloom = null;
            _closingBloom = closing;

            _bloomButton.Classes.Remove("open");

            if (DataContext is MainWindowViewModel vm)
                vm.IsMenuOpen = false;

            // Reset group navigation on close — next open always starts at root
            _groupNavStack.Clear();
            _currentGroup = null;

            // Stop the hit-test timer but keep WS_EX_TRANSPARENT so clicks still
            // pass through to other apps during the fade-out animation.
            // Full cleanup (remove WS_EX_TRANSPARENT) happens in HidePetalWindow.
            (closing.Window as PetalWindow)?.DisableClickThrough(keepTransparent: true);

            // Re-assert bloom button above petals for the duration of the close
            // animation.  The hit-test timer (which normally manages z-order) was just
            // stopped, so temporarily force topmost even when the setting is off.
            this.Topmost = false;
            this.Topmost = true;

            await AnimateBloomAsync(closing, false);

            _closingBloom = null;

            // Restore correct topmost state after the animation completes.
            if (DataContext is MainWindowViewModel mvm && !mvm.AlwaysOnTop)
                this.Topmost = false;
        }

        // ────────────────────────────────────────────────────
        // Group navigation
        // ────────────────────────────────────────────────────

        private async Task NavigateIntoGroupAsync(BloomItem group)
        {
            CancelPendingDeactivationClose();
            if (_activeBloom != _appBloom) return;
            if (DataContext is not MainWindowViewModel vm) return;

            // Push current group (null = root) so we can go back
            _groupNavStack.Push(_currentGroup);
            _currentGroup = group;

            // Close animation without hiding the petal window
            await BloomAnimator.AnimateBloomAsync(
                _appBloom, false, ComputeEdgeAwareness, _ => { }, _ => { });

            // Rebuild petals for the group's children + Back petal
            var petals = PetalConverter.ConvertToPetalsForGroup(group.ChildIds, vm.Items);
            RebuildPetals(_appBloom, petals);

            // Open animation — ShowPetalWindow repositions for new canvas size
            await BloomAnimator.AnimateBloomAsync(
                _appBloom, true, ComputeEdgeAwareness, ShowPetalWindow, _ => { });
        }

        private async Task NavigateBackAsync()
        {
            CancelPendingDeactivationClose();
            if (_activeBloom != _appBloom || _groupNavStack.Count == 0) return;
            if (DataContext is not MainWindowViewModel vm) return;

            var parentGroup = _groupNavStack.Pop();
            _currentGroup = parentGroup;

            // Close animation without hiding
            await BloomAnimator.AnimateBloomAsync(
                _appBloom, false, ComputeEdgeAwareness, _ => { }, _ => { });

            // Rebuild: root items (filtered) or parent group's children
            var petals = GetCurrentAppPetals(vm);

            RebuildPetals(_appBloom, petals);

            // Open animation
            await BloomAnimator.AnimateBloomAsync(
                _appBloom, true, ComputeEdgeAwareness, ShowPetalWindow, _ => { });
        }

        // Dialog handlers — delegates to DialogHandler

        private void OnItemsChanged()
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // If we're inside a group, validate it still exists
            if (_currentGroup != null && !vm.Items.Contains(_currentGroup))
            {
                _groupNavStack.Clear();
                _currentGroup = null;
            }

            RecomputeCanvasSize();

            var newPetals = GetCurrentAppPetals(vm);
            RebuildPetals(_appBloom, newPetals);

            // If the bloom is currently open, snap the new petals to expanded
            if (_activeBloom == _appBloom && _appBloom.PetalItems.Count > 0)
            {
                var (bias, spread) = ComputeEdgeAwareness(_appBloom);
                PetalLayoutEngine.LayoutPetals(_appBloom, bias, spread);
                _appBloom.LastBias = bias;
                _appBloom.LastSpread = spread;
                ShowPetalWindow(_appBloom);
                BloomAnimator.SnapToExpanded(_appBloom);
            }

            RegisterAllItemHotkeys();
        }

        // ────────────────────────────────────────────────────
        // Canvas sizing (computed once, reused for every bloom)
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Compute the canvas size from the largest petal set (root items, any
        /// group's children + back petal, or settings petals) at 90° worst-case
        /// spread. Both bloom contexts share this size so we never need to resize.
        /// </summary>
        private void RecomputeCanvasSize()
        {
            int maxCount = 1;

            if (DataContext is MainWindowViewModel vm)
            {
                // Root-level items (not in any group)
                int rootCount = vm.Items.Count(i => !i.IsInGroup);
                if (rootCount > maxCount) maxCount = rootCount;

                // Each group's children + 1 for Back petal
                foreach (var item in vm.Items)
                {
                    if (item.Type == ShortcutType.Group)
                    {
                        int groupCount = item.ChildIds.Count + 1; // +1 for Back
                        if (groupCount > maxCount) maxCount = groupCount;
                    }
                }
            }

            // Settings petals
            int settingsCount = GetSettingsPetals().Length;
            if (settingsCount > maxCount) maxCount = settingsCount;

            double maxR = PetalLayoutEngine.ComputeMaxLayoutRadius(maxCount, 90);
            double petalHalf = PetalLayoutEngine.PetalSize / 2.0;
            double extraPadding = PetalLayoutEngine.PetalSize * 0.05
                                + PetalLayoutEngine.RepelStrength
                                + 16 * PetalLayoutEngine.ScaleFactor;
            _canvasSize = 2 * (maxR + petalHalf + extraPadding);
        }

        // ────────────────────────────────────────────────────
        // Rebuild petals
        // ────────────────────────────────────────────────────

        private void RebuildPetals(BloomContext ctx, PetalItem[] items, LabelMode? labelModeOverride = null)
        {
            ctx.Canvas.Children.Clear();
            ctx.PetalItems.Clear();
            ctx.PetalPositions.Clear();
            ctx.PetalBaseZ.Clear();
            ctx.SourceItems = items;
            BuildPetals(ctx, items, labelModeOverride);
        }

        // ────────────────────────────────────────────────────
        // Build petals (delegates to PetalFactory)
        // ────────────────────────────────────────────────────

        private void BuildPetals(BloomContext ctx, PetalItem[] items, LabelMode? labelModeOverride = null)
        {
            var labelMode = labelModeOverride
                ?? (DataContext is MainWindowViewModel vm ? vm.LabelMode : LabelMode.Hidden);

            int n = items.Length > 0 ? items.Length : 1;

            // Capacity-based layout for initial z-index assignment (full circle)
            var (counts, radii) = PetalLayoutEngine.ComputeLayout(n, 360);
            ctx.LayerCounts = counts;
            ctx.LayerRadii = radii;

            // Use the pre-computed global canvas size (covers the largest group at 90° spread)
            ctx.CanvasSize = _canvasSize;
            double center = ctx.CanvasSize / 2.0;

            ctx.PetalItems.Clear();
            ctx.PetalPositions.Clear();
            ctx.PetalBaseZ.Clear();

            if (items.Length > 0)
            {
                int totalLayers = ctx.LayerCounts.Length;
                int itemIndex = 0;

                for (int layer = 0; layer < totalLayers; layer++)
                {
                    int count = ctx.LayerCounts[layer];
                    int baseZ = (totalLayers - layer) * 20;

                    for (int i = 0; i < count; i++)
                    {
                        var petal = items[itemIndex];
                        int editIndex = ctx == _appBloom && petal.SourceItemId != null ? itemIndex : -1;
                        var petalBorder = PetalFactory.CreatePetalElement(petal, labelMode, editIndex);

                        Canvas.SetLeft(petalBorder, center - PetalLayoutEngine.PetalSize / 2.0);
                        Canvas.SetTop(petalBorder, center - PetalLayoutEngine.PetalSize / 2.0);

                        int petalZ = baseZ + i;
                        petalBorder.ZIndex = petalZ;
                        ctx.PetalBaseZ.Add(petalZ);

                        petalBorder.RenderTransform =
                            TransformOperations.Parse("translate(0px,0px) scale(0)");
                        petalBorder.Opacity = 0;
                        petalBorder.IsHitTestVisible = false;

                        ctx.PetalPositions.Add((0, 0));

                        int capturedIdx = itemIndex;
                        int hoverZ = totalLayers * 20 + 10;
                        petalBorder.PointerEntered += (_, _) =>
                        {
                            ctx.HoveredIndex = capturedIdx;
                            petalBorder.ZIndex = hoverZ;
                        };
                        petalBorder.PointerExited += (_, _) =>
                        {
                            if (ctx.HoveredIndex == capturedIdx)
                                ctx.HoveredIndex = -1;
                            petalBorder.ZIndex = ctx.PetalBaseZ[capturedIdx];
                        };

                        ctx.Canvas.Children.Add(petalBorder);
                        ctx.PetalItems.Add(petalBorder);
                        itemIndex++;
                    }
                }
            }

        }

        // ────────────────────────────────────────────────────
        // Show / hide petal window
        // ────────────────────────────────────────────────────

        private void ShowPetalWindow(BloomContext ctx)
        {
            PixelPoint tearCenter;
            try
            {
                tearCenter = _bloomButton.PointToScreen(
                    new Point(PetalLayoutEngine.ButtonSize / 2, PetalLayoutEngine.ButtonSize / 2));
            }
            catch
            {
                var s = Screens.ScreenFromVisual(this)?.Scaling ?? 1.0;
                tearCenter = new PixelPoint(
                    (int)(Position.X + Width / 2.0 * s),
                    (int)(Position.Y + Height / 2.0 * s));
            }

            var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
            double scaling = screen?.Scaling ?? 1.0;

            int halfW = (int)Math.Round(ctx.CanvasSize * scaling / 2);
            int halfH = (int)Math.Round(ctx.CanvasSize * scaling / 2);

            // Size the window before Show so it opens at the right dimensions
            ctx.Window.Width = ctx.CanvasSize;
            ctx.Window.Height = ctx.CanvasSize;

            if (!ctx.Window.IsVisible)
                ctx.Window.Show();

            // Always re-apply size AND position AFTER the window is visible.
            // Show() can change DPI context or trigger layout that shifts the window.
            ctx.Window.Width = ctx.CanvasSize;
            ctx.Window.Height = ctx.CanvasSize;
            ctx.Window.Position = new PixelPoint(
                tearCenter.X - halfW,
                tearCenter.Y - halfH);

            Log.Information("[ShowPetal] canvasSize={CS:F1} scaling={S:F2} pos=({PX},{PY}) petals={PC}",
                ctx.CanvasSize, scaling,
                ctx.Window.Position.X, ctx.Window.Position.Y,
                ctx.PetalItems.Count);

            (ctx.Window as PetalWindow)?.EnableClickThrough();

            // Ensure MainWindow (bloom button) renders above PetalWindow.
            // The false→true cycle forces this window above the petal in z-order.
            // The hit-test timer in PetalWindow will maintain this going forward.
            this.Topmost = false;
            this.Topmost = true;
            if (DataContext is MainWindowViewModel mvm2 && !mvm2.AlwaysOnTop)
                this.Topmost = false;
        }

        private static void HidePetalWindow(BloomContext ctx)
        {
            (ctx.Window as PetalWindow)?.DisableClickThrough();
            // Move off-screen instead of Hide() to preserve the WS_EX_LAYERED state
            // that enables cross-process click-through. Avalonia resets DComp on Show().
            ctx.Window.Position = new PixelPoint(-10000, -10000);
        }

        /// <summary>
        /// Instantly close an active bloom that is mid-animation (closing direction).
        /// Called from BloomDragHandler when the user starts dragging during a close animation.
        /// </summary>
        private void SnapCloseBloom(BloomContext ctx)
        {
            if (_activeBloom == ctx)
                _activeBloom = null;
            if (_closingBloom == ctx)
                _closingBloom = null;

            _bloomButton.Classes.Remove("open");

            if (DataContext is MainWindowViewModel vm)
                vm.IsMenuOpen = false;

            _groupNavStack.Clear();
            _currentGroup = null;

            BloomAnimator.SnapToClosed(ctx, HidePetalWindow);
        }

        // ────────────────────────────────────────────────────
        // Edge awareness (delegates to PetalLayoutEngine)
        // ────────────────────────────────────────────────────

        private (double biasAngleDeg, double spreadDeg) ComputeEdgeAwareness(BloomContext ctx)
        {
            var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
            if (screen == null)
                return (-90, 360);

            double scaling = screen.Scaling;
            var bounds = screen.WorkingArea;

            PixelPoint buttonCenter;
            try
            {
                buttonCenter = _bloomButton.PointToScreen(
                    new Point(PetalLayoutEngine.ButtonSize / 2, PetalLayoutEngine.ButtonSize / 2));
            }
            catch
            {
                double wh = Width / 2.0 * scaling;
                buttonCenter = new PixelPoint(
                    (int)(Position.X + wh), (int)(Position.Y + wh));
            }

            return PetalLayoutEngine.ComputeEdgeAwareness(
                ctx,
                (bounds.X, bounds.Y, bounds.Width, bounds.Height),
                scaling,
                (buttonCenter.X, buttonCenter.Y));
        }

        // ────────────────────────────────────────────────────
        // Animation (delegates to BloomAnimator)
        // ────────────────────────────────────────────────────

        private Task AnimateBloomAsync(BloomContext ctx, bool open)
        {
            return BloomAnimator.AnimateBloomAsync(
                ctx, open,
                ComputeEdgeAwareness,
                ShowPetalWindow,
                HidePetalWindow);
        }

        // ────────────────────────────────────────────────────
        // Deactivation
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Cancel any pending deactivation close.  Called at the start of every
        /// intentional bloom operation (group nav, dialog open, toggle) so the
        /// deferred close from OnWindowDeactivated doesn't race.
        /// </summary>
        private void CancelPendingDeactivationClose()
        {
            _deactivateCloseCts?.Cancel();
            _deactivateCloseCts = null;
        }

        private void RehideIfNeeded()
        {
            if (!_rehideAfterGroupClose) return;
            _rehideAfterGroupClose = false;
            Hide();
            _appBloom.Window.Hide();
            _settingsBloom.Window.Hide();
            _bloomHidden = true;
        }

        private async Task CloseAndRehideAsync()
        {
            await CloseActiveBloomAsync();
            RehideIfNeeded();
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            if (_activeBloom == null) return;
            if (DataContext is MainWindowViewModel vm && !vm.UnBloomOnFocusLoss) return;
            if (IsActive || _appBloom.Window.IsActive || _settingsBloom.Window.IsActive) return;

            // Defer the close slightly so intentional actions (group nav, dialogs)
            // that are triggered by the same petal click can cancel it first.
            CancelPendingDeactivationClose();
            var cts = new CancellationTokenSource();
            _deactivateCloseCts = cts;
            _ = DeferredDeactivationCloseAsync(cts.Token);
        }

        private async Task DeferredDeactivationCloseAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(80, token);
                if (_activeBloom == null) return;
                // Re-check: focus may have returned to one of our windows
                if (IsActive || _appBloom.Window.IsActive || _settingsBloom.Window.IsActive) return;
                await CloseActiveBloomAsync();
                RehideIfNeeded();
            }
            catch (OperationCanceledException) { }
        }

        private void OnPetalCanvasPressed(PointerPressedEventArgs e)
        {
            var ctx = _activeBloom;
            if (ctx == null) return;
            if (DataContext is MainWindowViewModel vm && !vm.UnBloomOnFocusLoss) return;

            var point = e.GetCurrentPoint(ctx.Canvas);
            if (point.Properties.IsLeftButtonPressed)
                _ = CloseAndRehideAsync();
        }

        // ────────────────────────────────────────────────────
        // Repel effect (delegates to BloomAnimator)
        // ────────────────────────────────────────────────────

        private void OnCanvasPointerMoved(BloomContext ctx, PointerEventArgs e)
        {
            if (!ctx.IsExpanded) return;

            var pos = e.GetPosition(ctx.Canvas);
            double center = ctx.CanvasSize / 2.0;
            BloomAnimator.UpdateRepelTransforms(ctx, pos.X - center, pos.Y - center);
        }

        private static void OnCanvasPointerExited(BloomContext ctx)
        {
            if (!ctx.IsExpanded) return;
            BloomAnimator.ResetInteractiveTransforms(ctx);
        }

        // ────────────────────────────────────────────────────
        // Scale
        // ────────────────────────────────────────────────────

        private void ApplyScale(AppScale scale)
        {
            double factor = PetalLayoutEngine.GetScaleFactor(scale);
            PetalLayoutEngine.ScaleFactor = factor;

            Width = 60 * factor;
            Height = 60 * factor;

            _bloomButton.Width = 52 * factor;
            _bloomButton.Height = 52 * factor;
            _bloomButton.CornerRadius = new CornerRadius(26 * factor);

            _bloomIcon.Width = 36 * factor;
            _bloomIcon.Height = 36 * factor;

            RecomputeCanvasSize();

            if (DataContext is MainWindowViewModel vm)
            {
                RebuildPetals(_appBloom, GetCurrentAppPetals(vm));
                RebuildPetals(_settingsBloom, GetSettingsPetals());
            }
        }

        private void ApplyAlwaysOnTop(bool alwaysOnTop)
        {
            Topmost = alwaysOnTop;
            _appBloom.Window.Topmost = alwaysOnTop;
            _settingsBloom.Window.Topmost = alwaysOnTop;

            if (_appBloom.Window is PetalWindow pw1)
                pw1.AlwaysOnTop = alwaysOnTop;
            if (_settingsBloom.Window is PetalWindow pw2)
                pw2.AlwaysOnTop = alwaysOnTop;
        }

        // ────────────────────────────────────────────────────
        // ViewModel property changed
        // ────────────────────────────────────────────────────

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainWindowViewModel vm) return;

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.IsMenuOpen):
                    if (!vm.IsMenuOpen && _activeBloom != null)
                        _ = CloseActiveBloomAsync();
                    break;

                case nameof(MainWindowViewModel.Theme):
                case nameof(MainWindowViewModel.LabelMode):
                    RebuildPetals(_appBloom, GetCurrentAppPetals(vm));
                    RebuildPetals(_settingsBloom, GetSettingsPetals());
                    break;

                case nameof(MainWindowViewModel.Scale):
                    ApplyScale(vm.Scale);
                    break;

                case nameof(MainWindowViewModel.IsUpdateAvailable):
                    RebuildPetals(_settingsBloom, GetSettingsPetals());
                    break;

                case nameof(MainWindowViewModel.AlwaysOnTop):
                    ApplyAlwaysOnTop(vm.AlwaysOnTop);
                    break;
            }
        }

        // ────────────────────────────────────────────────────
        // Global hotkey
        // ────────────────────────────────────────────────────

        private const int WM_HOTKEY = 0x0312;
        private bool _bloomHidden;
        private bool _togglingVisibility;
        private bool _rehideAfterGroupClose;

        private IntPtr WndProcHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                handled = true;
                _hotkeyTriggered = true;

                if (id == HotkeyService.HOTKEY_PETALS)
                    TogglePetalsHotkeyAsync().FireAndForget();
                else if (id == HotkeyService.HOTKEY_BLOOM)
                    ToggleBloomVisibilityAsync().FireAndForget();
                else if (_itemHotkeyIdToItemId.TryGetValue(id, out var itemId))
                    HandleItemHotkey(itemId);
            }
            return IntPtr.Zero;
        }

        private async Task TogglePetalsHotkeyAsync()
        {
            if (_bloomHidden)
            {
                Show();
                _appBloom.Window.Show();
                _settingsBloom.Window.Show();
                _bloomHidden = false;
                _rehideAfterGroupClose = true;
            }
            await ToggleBloomAsync(_appBloom);
        }

        private async Task ToggleBloomVisibilityAsync()
        {
            if (_togglingVisibility) return;
            _togglingVisibility = true;
            try
            {
                _rehideAfterGroupClose = false;
                if (_bloomHidden)
                {
                    Show();
                    _appBloom.Window.Show();
                    _settingsBloom.Window.Show();
                    _bloomHidden = false;
                    await ToggleBloomAsync(_appBloom);
                }
                else
                {
                    if (_activeBloom != null)
                        await CloseActiveBloomAsync();
                    Hide();
                    _appBloom.Window.Hide();
                    _settingsBloom.Window.Hide();
                    _bloomHidden = true;
                }
            }
            finally
            {
                _togglingVisibility = false;
            }
        }

        private void RegisterHotkey(int id, string? hotkeyString)
        {
            if (_hotkeyHwnd == IntPtr.Zero) return;
            HotkeyService.Unregister(_hotkeyHwnd, id);

            if (!string.IsNullOrEmpty(hotkeyString) && HotkeyService.Parse(hotkeyString, out var mod, out var vk))
                HotkeyService.Register(_hotkeyHwnd, id, mod, vk);
        }

        private void RegisterAllItemHotkeys()
        {
            if (_hotkeyHwnd == IntPtr.Zero) return;

            // Unregister all existing item hotkeys
            for (int i = 0; i < _registeredItemHotkeyCount; i++)
                HotkeyService.Unregister(_hotkeyHwnd, HotkeyService.HOTKEY_ITEM_BASE + i);
            _itemHotkeyIdToItemId.Clear();
            _registeredItemHotkeyCount = 0;

            if (DataContext is not MainWindowViewModel vm) return;

            int nextId = HotkeyService.HOTKEY_ITEM_BASE;
            foreach (var item in vm.Items)
            {
                if (string.IsNullOrEmpty(item.Hotkey)) continue;
                if (!HotkeyService.Parse(item.Hotkey, out var mod, out var vk)) continue;

                if (HotkeyService.Register(_hotkeyHwnd, nextId, mod, vk))
                    _itemHotkeyIdToItemId[nextId] = item.Id;
                nextId++;
            }
            _registeredItemHotkeyCount = nextId - HotkeyService.HOTKEY_ITEM_BASE;
        }

        private bool _handlingItemHotkey;

        private async void HandleItemHotkey(string itemId)
        {
            if (_handlingItemHotkey) return;
            _handlingItemHotkey = true;
            try
            {
                if (DataContext is not MainWindowViewModel vm) return;
                var item = vm.Items.FirstOrDefault(i => i.Id == itemId);
                if (item == null) return;

                if (item.Type == ShortcutType.Group)
                {
                    await NavigateIntoGroupAndOpenAsync(item);
                }
                else
                {
                    var process = ServiceLocator.Process;
                    process.Launch(item.Path,
                        string.IsNullOrEmpty(item.Arguments) ? null : item.Arguments,
                        string.IsNullOrEmpty(item.WorkingDirectory) ? null : item.WorkingDirectory);
                }
            }
            finally
            {
                _handlingItemHotkey = false;
            }
        }

        private async Task NavigateIntoGroupAndOpenAsync(BloomItem group)
        {
            // Toggle: if this group is already open, close it
            if (_activeBloom == _appBloom && _currentGroup == group)
            {
                await CloseActiveBloomAsync();
                RehideIfNeeded();
                return;
            }

            // If bloom is hidden, show it and remember to re-hide on close
            if (_bloomHidden)
            {
                Show();
                _appBloom.Window.Show();
                _settingsBloom.Window.Show();
                _bloomHidden = false;
                _rehideAfterGroupClose = true;
            }

            if (_activeBloom == null)
            {
                // Bloom not open — set group and open
                _groupNavStack.Clear();
                _groupNavStack.Push(null);
                _currentGroup = group;
                await ToggleBloomAsync(_appBloom);
            }
            else if (_activeBloom == _appBloom)
            {
                // App bloom already open with different content — navigate into the group
                await NavigateIntoGroupAsync(group);
            }
            else
            {
                // Settings bloom is open — close it, then open app bloom at group
                await CloseActiveBloomAsync();
                _groupNavStack.Clear();
                _groupNavStack.Push(null);
                _currentGroup = group;
                await ToggleBloomAsync(_appBloom);
            }
        }

        // Bloom button drag & click — delegates to BloomDragHandler
        // Drag-and-drop — delegates to DragDropHandler
        // Toast notification — delegates to ToastService
    }
}
