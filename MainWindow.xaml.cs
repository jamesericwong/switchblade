using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;
using SwitchBlade.Handlers;
using System.Runtime.InteropServices;
using System.Threading.Tasks; // Added for Task

namespace SwitchBlade
{
    [ExcludeFromCodeCoverage]
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger;
        private readonly KeyboardInputHandler _keyboardHandler;
        private readonly WindowResizeHandler _resizeHandler;

        private HotKeyService? _hotKeyService;
        private BackgroundPollingService? _backgroundPollingService;
        private BadgeAnimationService? _badgeAnimationService;
        private ThumbnailService? _thumbnailService;
        private IntPtr _lastThumbnailHwnd = IntPtr.Zero;

        public List<IWindowProvider> Providers { get; private set; } = new List<IWindowProvider>();

        // Constructor Injection - Explicit Dependencies
        public MainWindow(
            MainViewModel viewModel,
            ISettingsService settingsService,
            IDispatcherService dispatcherService,
            ILogger logger,
            INumberShortcutService numberShortcutService)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _settingsService = settingsService;
            _dispatcherService = dispatcherService;
            _logger = logger;

            // Sync Providers list with what's in the ViewModel (ViewModel is the source of truth for providers)
            Providers.Clear();
            foreach (var provider in _viewModel.WindowProviders)
            {
                Providers.Add(provider);
            }

            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initialize handlers (extracted for SRP)
            _keyboardHandler = new KeyboardInputHandler(
                _viewModel,
                _settingsService,
                numberShortcutService,
                () => this.Hide(),
                ActivateWindow,
                () => ResultsConfig.ListActualHeight);

            _resizeHandler = new WindowResizeHandler(this, _logger);

            // Ensure the window handle (HWND) exists so we can register the global hotkey.
            // This is critical for /minimized startup where the window is never shown initially.
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            _logger.Log($"EnsureHandle completed. HWND: {helper.Handle}");

            // Initialize HotKeyService early so the global hotkey works even when starting minimized.
            // This must happen after EnsureHandle() because HotKeyService needs a valid HWND.
            _hotKeyService = new HotKeyService(this, _settingsService, _logger, OnHotKeyPressed);

            this.Loaded += MainWindow_Loaded;
            this.PreviewKeyDown += _keyboardHandler.HandleKeyDown;
        }

        public static T? GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ApplyBackdrop()
        {
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;

            string theme = _settingsService.Settings.CurrentTheme;
            int darkMode = (theme.Contains("Dark", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

            SwitchBlade.Contracts.NativeInterop.DwmSetWindowAttribute(hwnd,
                SwitchBlade.Contracts.NativeInterop.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int backdropType = SwitchBlade.Contracts.NativeInterop.DWM_BACKDROP_MICA;
            SwitchBlade.Contracts.NativeInterop.DwmSetWindowAttribute(hwnd,
                SwitchBlade.Contracts.NativeInterop.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

            int cornerPreference = SwitchBlade.Contracts.NativeInterop.DWMWCP_ROUND;
            SwitchBlade.Contracts.NativeInterop.DwmSetWindowAttribute(hwnd,
                SwitchBlade.Contracts.NativeInterop.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.Log($"MainWindow Loaded. Initial Size: {this.Width}x{this.Height}, ResizeMode: {this.ResizeMode}, Style: {this.WindowStyle}");

            // Initialize ThumbnailService - needs PreviewCanvas which isn't available until loaded
            _thumbnailService = new ThumbnailService(this, _logger);
            _thumbnailService.SetPreviewContainer(PreviewPanel.PreviewCanvas);

            // Initialize Badge Animation Service
            _badgeAnimationService = new BadgeAnimationService(new StoryboardBadgeAnimator(_dispatcherService));
            _viewModel.ResultsUpdated += OnResultsUpdated;
            _viewModel.SearchTextChanged += OnSearchTextChanged;

            // Interaction handlers from UserControl
            ResultsConfig.PreviewItemRequested += ResultList_PreviewItemRequested;
            ResultsConfig.ActivateItemRequested += ResultList_ActivateItemRequested;

            // Initialize Background Polling Service
            _backgroundPollingService = new BackgroundPollingService(
                _settingsService,
                _dispatcherService,
                () => _viewModel.RefreshWindows());

            // Initial load - apply saved size
            this.Width = _settingsService.Settings.WindowWidth;
            this.Height = _settingsService.Settings.WindowHeight;

            // Center the window based on the applied size
            // (WindowStartupLocation="CenterScreen" doesn't account for size changes after load)
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;

            _logger.Log($"Applied Settings Size: {this.Width}x{this.Height}, Centered at: ({this.Left}, {this.Top})");

            SearchBox.FocusInput();
            ApplyBackdrop();
            ConfigureWindowStyles();
            _ = InitialLoadAsync();
        }

        /// <summary>
        /// Fixes Alt+Tab behavior by configuring window styles.
        /// 1. Hides the WPF-generated owner window from Alt+Tab (WS_EX_TOOLWINDOW).
        /// 2. Forces the main window to remain in Alt+Tab (WS_EX_APPWINDOW) despite having a ToolWindow owner.
        /// </summary>
        private void ConfigureWindowStyles()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            
            // 1. Force main window to appear in Alt+Tab (required because owner will be a ToolWindow)
            var mainExStyle = (int)NativeInterop.GetWindowLongPtr(hwnd, NativeInterop.GWL_EXSTYLE);
            if ((mainExStyle & NativeInterop.WS_EX_APPWINDOW) == 0)
            {
                mainExStyle |= NativeInterop.WS_EX_APPWINDOW;
                NativeInterop.SetWindowLongPtr(hwnd, NativeInterop.GWL_EXSTYLE, (IntPtr)mainExStyle);
                _logger.Log($"ConfigureWindowStyles: Added WS_EX_APPWINDOW to Main Window {hwnd}");
            }

            // 2. Remove standard window chrome styles that cause the "Header" to appear in Alt+Tab
            // We keep WS_THICKFRAME (if present) for resizing, but remove Caption/SystemMenu
            var mainStyle = (int)NativeInterop.GetWindowLongPtr(hwnd, NativeInterop.GWL_STYLE);
            bool stylesChanged = false;

            if ((mainStyle & NativeInterop.WS_CAPTION) != 0)
            {
                mainStyle &= ~NativeInterop.WS_CAPTION;
                stylesChanged = true;
            }

            if ((mainStyle & NativeInterop.WS_SYSMENU) != 0)
            {
                mainStyle &= ~NativeInterop.WS_SYSMENU;
                stylesChanged = true;
            }

            if (stylesChanged)
            {
                NativeInterop.SetWindowLongPtr(hwnd, NativeInterop.GWL_STYLE, (IntPtr)mainStyle);
                _logger.Log($"ConfigureWindowStyles: Removed WS_CAPTION/WS_SYSMENU from Main Window {hwnd}");
            }

            // 3. Hide the WPF owner window
            var owner = NativeInterop.GetWindow(hwnd, NativeInterop.GW_OWNER);
            if (owner != IntPtr.Zero)
            {
                var ownerExStyle = (int)NativeInterop.GetWindowLongPtr(owner, NativeInterop.GWL_EXSTYLE);
                if ((ownerExStyle & NativeInterop.WS_EX_TOOLWINDOW) == 0)
                {
                    ownerExStyle |= NativeInterop.WS_EX_TOOLWINDOW;
                    ownerExStyle &= ~NativeInterop.WS_EX_APPWINDOW;
                    NativeInterop.SetWindowLongPtr(owner, NativeInterop.GWL_EXSTYLE, (IntPtr)ownerExStyle);
                    _logger.Log($"ConfigureWindowStyles: Set WS_EX_TOOLWINDOW on owner HWND {owner}");
                }
            }
        }

        private async Task InitialLoadAsync()
        {
            // Reset animation state once at start so all items can animate as they arrive
            if (_settingsService.Settings.EnableBadgeAnimations)
            {
                _badgeAnimationService?.ResetAnimationState(_viewModel.FilteredWindows);
            }

            // Let RefreshWindows run - ResultsUpdated will trigger animations as batches arrive
            await _viewModel.RefreshWindows();

            // If animation is disabled, ensure all badges are visible
            if (!_settingsService.Settings.EnableBadgeAnimations)
            {
                foreach (var item in _viewModel.FilteredWindows)
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                }
            }

            // FORCE SCROLL TO TOP: After initial batches are loaded, ensure we are at the top.
            // WPF's ListBox might have scrolled down if items were inserted at the top.
            await _dispatcherService.InvokeAsync(async () =>
            {
                // Wait briefly for layout to settle
                await Task.Delay(50);
                if (_viewModel.FilteredWindows.Count > 0)
                {
                    _viewModel.MoveSelectionToFirst();
                }
            });
        }

        private bool _pendingAnimationReset = false;
        private bool _isForceOpenPending = false;

        private void OnResultsUpdated(object? sender, EventArgs e)
        {
            _logger.Log($"[OnResultsUpdated] Called. IsVisible={this.IsVisible}, AnimationsEnabled={_settingsService.Settings.EnableBadgeAnimations}");

            // Capture intent BEFORE consuming the flag
            bool wasTextChange = _pendingAnimationReset;

            // Handle pending animation reset (e.g., from search text change or ForceOpen)
            // We do this HERE, on the new list, to ensure all currently visible items get reset.
            if (_pendingAnimationReset && _badgeAnimationService != null && _viewModel.FilteredWindows != null)
            {
                _logger.Log($"[OnResultsUpdated] Applying pending animation reset to {_viewModel.FilteredWindows.Count} items.");
                _badgeAnimationService.ResetAnimationState(_viewModel.FilteredWindows);
                _pendingAnimationReset = false;
            }

            // When search results update, trigger staggered animation for new items (if enabled)
            if (_badgeAnimationService != null && this.IsVisible && _settingsService.Settings.EnableBadgeAnimations && _viewModel.FilteredWindows != null)
            {
                // Debounce only for text-change triggers (typing), not hotkey opens or streaming updates.
                // _isForceOpenPending overrides: hotkey open always skips debounce.
                bool shouldDebounce = wasTextChange && !_isForceOpenPending;
                _isForceOpenPending = false;

                _ = _badgeAnimationService.TriggerStaggeredAnimationAsync(_viewModel.FilteredWindows, skipDebounce: !shouldDebounce);
            }
            else if (this.IsVisible && !_settingsService.Settings.EnableBadgeAnimations && _viewModel.FilteredWindows != null)
            {
                // Ensure badges are visible immediately when animation is disabled
                foreach (var item in _viewModel.FilteredWindows)
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                }
            }
        }

        private void OnSearchTextChanged(object? sender, EventArgs e)
        {
            // Reset animation state on ANY text change (typing or clearing).
            // User requested that re-animation happens on all modifications.
            // Defer the reset to OnResultsUpdated so it applies to the NEW list (post-filter).
            _pendingAnimationReset = true;
        }

        public void ForceOpen()
        {
            // Apply Settings
            var app = (App)System.Windows.Application.Current;
            this.Opacity = 0; // Start transparent for fade in
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            SwitchBlade.Contracts.NativeInterop.ForceForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            SearchBox.FocusInput();

            // Mark that this is a hotkey-triggered open, NOT a typing-triggered change.
            // This ensures the first animation batch skips the debounce for immediate responsiveness.
            _isForceOpenPending = true;

            // Reset badge animation state BEFORE clearing search text
            // (Clearing search text triggers ResultsUpdated which would mark items as animated)
            _logger.Log($"[ForceOpen] Resetting animation state for fresh open");
            _badgeAnimationService?.ResetAnimationState(_viewModel.FilteredWindows);

            // Also hide badges immediately so there's no "visible then animate" flash
            if (_viewModel.FilteredWindows != null)
            {
                foreach (var item in _viewModel.FilteredWindows)
                {
                    if (item.IsShortcutVisible)
                    {
                        item.ResetBadgeAnimation();
                    }
                }
            }

            _viewModel.SearchText = "";
            _logger.Log($"[ForceOpen] Cleared SearchText");

            FadeIn();
            _ = ForceOpenAsync();
            _logger.Log("Forced Open (Tray/Menu).");
        }

        private async Task ForceOpenAsync()
        {
            // Let RefreshWindows run - ResultsUpdated will trigger animations as batches arrive
            // (Reset already done in ForceOpen before calling this)
            await _viewModel.RefreshWindows();

            // If animation is disabled, ensure all badges are visible
            if (!_settingsService.Settings.EnableBadgeAnimations)
            {
                foreach (var item in _viewModel.FilteredWindows)
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                }
            }

            // FORCE SCROLL TO TOP: Ensure we start at the top on every fresh open.
            await _dispatcherService.InvokeAsync(async () =>
            {
                // Wait briefly for layout to settle
                await Task.Delay(50);
                if (_viewModel.FilteredWindows.Count > 0)
                {
                    _viewModel.MoveSelectionToFirst();
                }
            });
        }

        private void OnHotKeyPressed()
        {
            _logger.Log($"Global Hotkey Pressed. Current Visibility: {this.Visibility}");

            // Suppress hotkey when a modal dialog (e.g., Settings) is open
            if (App.IsModalDialogOpen)
            {
                _logger.Log("Hotkey suppressed: Modal dialog is open.");
                return;
            }

            if (this.Visibility == Visibility.Visible)
            {
                _logger.Log("Hiding Window.");
                FadeOut(() => this.Hide());
            }
            else
            {
                ForceOpen();
            }
        }

        private void FadeIn()
        {
            var duration = _settingsService.Settings.FadeDurationMs;
            var targetOpacity = _settingsService.Settings.WindowOpacity;

            if (duration > 0)
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, targetOpacity, TimeSpan.FromMilliseconds(duration));
                this.BeginAnimation(Window.OpacityProperty, anim);
            }
            else
            {
                this.Opacity = targetOpacity;
            }
        }

        private void FadeOut(Action onCompleted)
        {
            var duration = _settingsService.Settings.FadeDurationMs;

            if (duration > 0 && this.Opacity > 0)
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(duration));
                anim.Completed += (s, e) => onCompleted();
                this.BeginAnimation(Window.OpacityProperty, anim);
            }
            else
            {
                onCompleted();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedWindow))
            {
                if (_settingsService.Settings.EnablePreviews && _viewModel.SelectedWindow != null)
                {
                    // Optimization: Prevent Flicker
                    // Only update thumbnail if the window handle has actually changed.
                    if (_lastThumbnailHwnd != _viewModel.SelectedWindow.Hwnd)
                    {
                        _lastThumbnailHwnd = _viewModel.SelectedWindow.Hwnd;
                        _thumbnailService?.UpdateThumbnail(_viewModel.SelectedWindow.Hwnd);
                    }

                    // Always scroll into view, just in case list was rebuilt
                    ResultsConfig.ScrollIntoView(_viewModel.SelectedWindow);
                }
                else
                {
                    _lastThumbnailHwnd = IntPtr.Zero;
                    _thumbnailService?.UpdateThumbnail(IntPtr.Zero);
                    if (_viewModel.SelectedWindow != null)
                    {
                        ResultsConfig.ScrollIntoView(_viewModel.SelectedWindow);
                    }
                }
            }
        }

        private void ResultList_PreviewItemRequested(object? sender, WindowItem windowItem)
        {
            if (_settingsService.Settings.EnablePreviews)
            {
                _thumbnailService?.UpdateThumbnail(windowItem.Hwnd);
            }
        }

        private void ResultList_ActivateItemRequested(object? sender, WindowItem windowItem)
        {
            ActivateWindow(windowItem);
        }

        private void ResizeGripBottomRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _resizeHandler.HandleBottomRightGripMouseDown(sender, e);

        private void ResizeGripBottomLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _resizeHandler.HandleBottomLeftGripMouseDown(sender, e);


        private void ActivateWindow(WindowItem? windowItem)
        {
            if (windowItem != null)
            {
                if (windowItem.Source != null)
                {
                    windowItem.Source.ActivateWindow(windowItem);
                }
                else
                {
                    // Fallback for items without source
                    _logger.Log($"Warning: WindowItem '{windowItem.Title}' has no Source provider.");

                    // Basic fallback attempt
                    if (SwitchBlade.Contracts.NativeInterop.IsIconic(windowItem.Hwnd))
                    {
                        SwitchBlade.Contracts.NativeInterop.ShowWindow(windowItem.Hwnd, SwitchBlade.Contracts.NativeInterop.SW_RESTORE);
                    }
                    SwitchBlade.Contracts.NativeInterop.SetForegroundWindow(windowItem.Hwnd);
                }

                FadeOut(() => this.Hide());
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _settingsService.Settings.WindowWidth = this.Width;
            _settingsService.Settings.WindowHeight = this.Height;
            _settingsService.SaveSettings();

            _hotKeyService?.Dispose();
            _thumbnailService?.Dispose();
            _backgroundPollingService?.Dispose();
            base.OnClosed(e);
        }
    }
}
