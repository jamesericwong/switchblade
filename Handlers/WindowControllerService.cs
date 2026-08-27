using System;
using System.Threading.Tasks;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Coordinates main-window presentation: native appearance (DWM backdrop, Alt+Tab styles),
    /// fade in/out, force-open and initial-load orchestration, the badge-animation state machine,
    /// and window activation with a native fallback.
    /// Presentation-layer coordinator (like KeyboardInputHandler) — may reference the view model;
    /// WPF specifics are isolated behind IWindowSurface and the interop seams.
    /// </summary>
    public class WindowControllerService
    {
        private readonly IWindowSurface _surface;
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger;
        private readonly MainViewModel _viewModel;
        private readonly Func<bool> _isModalDialogOpen;
        private readonly IWindowStyleInterop _styleInterop;
        private readonly IWindowInterop _windowInterop;

        // Attached at window load (the badge animator needs a view-owned container resolver).
        // Null-safe usage until then matches the previous code-behind behavior.
        private BadgeAnimationService? _badgeAnimations;

        private bool _pendingAnimationReset = false;
        private bool _isForceOpenPending = false;

        public WindowControllerService(
            IWindowSurface surface,
            ISettingsService settingsService,
            IDispatcherService dispatcherService,
            ILogger logger,
            MainViewModel viewModel,
            Func<bool> isModalDialogOpen,
            IWindowStyleInterop styleInterop,
            IWindowInterop windowInterop)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _isModalDialogOpen = isModalDialogOpen ?? throw new ArgumentNullException(nameof(isModalDialogOpen));
            _styleInterop = styleInterop ?? throw new ArgumentNullException(nameof(styleInterop));
            _windowInterop = windowInterop ?? throw new ArgumentNullException(nameof(windowInterop));
        }

        /// <summary>Attaches the badge animation service once it has been constructed at window load.</summary>
        public void SetBadgeAnimationService(BadgeAnimationService badgeAnimations) => _badgeAnimations = badgeAnimations;

        /// <summary>Applies the DWM dark-mode flag, MICA backdrop and rounded corners for the current theme.</summary>
        public void ApplyBackdrop(IntPtr hwnd)
        {
            string theme = _settingsService.Settings.CurrentTheme;
            int darkMode = (theme.Contains("Dark", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

            _styleInterop.DwmSetWindowAttribute(hwnd, NativeInterop.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode);

            int backdropType = NativeInterop.DWM_BACKDROP_MICA;
            _styleInterop.DwmSetWindowAttribute(hwnd, NativeInterop.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);

            int cornerPreference = NativeInterop.DWMWCP_ROUND;
            _styleInterop.DwmSetWindowAttribute(hwnd, NativeInterop.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference);
        }

        /// <summary>
        /// Fixes Alt+Tab behavior by configuring window styles.
        /// 1. Hides the WPF-generated owner window from Alt+Tab (WS_EX_TOOLWINDOW).
        /// 2. Forces the main window to remain in Alt+Tab (WS_EX_APPWINDOW) despite having a ToolWindow owner.
        /// </summary>
        public void ConfigureWindowStyles(IntPtr hwnd)
        {
            // 1. Force main window to appear in Alt+Tab (required because owner will be a ToolWindow)
            var mainExStyle = (int)_styleInterop.GetWindowLongPtr(hwnd, NativeInterop.GWL_EXSTYLE);
            if ((mainExStyle & NativeInterop.WS_EX_APPWINDOW) == 0)
            {
                mainExStyle |= NativeInterop.WS_EX_APPWINDOW;
                _styleInterop.SetWindowLongPtr(hwnd, NativeInterop.GWL_EXSTYLE, (IntPtr)mainExStyle);
                _logger.Log($"ConfigureWindowStyles: Added WS_EX_APPWINDOW to Main Window {hwnd}");
            }

            // 2. Remove standard window chrome styles that cause the "Header" to appear in Alt+Tab
            // We keep WS_THICKFRAME (if present) for resizing, but remove Caption/SystemMenu
            var mainStyle = (int)_styleInterop.GetWindowLongPtr(hwnd, NativeInterop.GWL_STYLE);
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
                _styleInterop.SetWindowLongPtr(hwnd, NativeInterop.GWL_STYLE, (IntPtr)mainStyle);
                _logger.Log($"ConfigureWindowStyles: Removed WS_CAPTION/WS_SYSMENU from Main Window {hwnd}");
            }

            // 3. Hide the WPF owner window
            var owner = _styleInterop.GetWindow(hwnd, NativeInterop.GW_OWNER);
            if (owner != IntPtr.Zero)
            {
                var ownerExStyle = (int)_styleInterop.GetWindowLongPtr(owner, NativeInterop.GWL_EXSTYLE);
                if ((ownerExStyle & NativeInterop.WS_EX_TOOLWINDOW) == 0)
                {
                    ownerExStyle |= NativeInterop.WS_EX_TOOLWINDOW;
                    ownerExStyle &= ~NativeInterop.WS_EX_APPWINDOW;
                    _styleInterop.SetWindowLongPtr(owner, NativeInterop.GWL_EXSTYLE, (IntPtr)ownerExStyle);
                    _logger.Log($"ConfigureWindowStyles: Set WS_EX_TOOLWINDOW on owner HWND {owner}");
                }
            }
        }

        public void FadeIn()
        {
            var duration = _settingsService.Settings.FadeDurationMs;
            var targetOpacity = _settingsService.Settings.WindowOpacity;

            if (duration > 0)
            {
                _surface.AnimateOpacity(0, targetOpacity, duration, null);
            }
            else
            {
                _surface.Opacity = targetOpacity;
            }
        }

        public void FadeOut(Action onCompleted)
        {
            var duration = _settingsService.Settings.FadeDurationMs;

            if (duration > 0 && _surface.Opacity > 0)
            {
                _surface.AnimateOpacity(_surface.Opacity, 0, duration, onCompleted);
            }
            else
            {
                onCompleted();
            }
        }

        /// <summary>Toggles the main window: hides it (with fade-out) when visible, force-opens it otherwise.</summary>
        public void ToggleVisibility()
        {
            _logger.Log($"Global Hotkey Pressed. Current Visibility: {_surface.IsVisible}");

            // Suppress hotkey when a modal dialog (e.g., Settings) is open
            if (_isModalDialogOpen())
            {
                _logger.Log("Hotkey suppressed: Modal dialog is open.");
                return;
            }

            if (_surface.IsVisible)
            {
                _logger.Log("Hiding Window.");
                FadeOut(() => _surface.Hide());
            }
            else
            {
                ForceOpen();
            }
        }

        public void ForceOpen()
        {
            // Apply Settings
            _ = System.Windows.Application.Current;
            _surface.Opacity = 0; // Start transparent for fade in
            _surface.Show();
            _surface.NormalizeState();
            _surface.Activate();
            _windowInterop.ForceForegroundWindow(_surface.Handle);

            _surface.FocusSearchInput();

            // Mark that this is a hotkey-triggered open, NOT a typing-triggered change.
            // This ensures the first animation batch skips the debounce for immediate responsiveness.
            _isForceOpenPending = true;

            // Reset badge animation state BEFORE clearing search text
            // (Clearing search text triggers ResultsUpdated which would mark items as animated)
            _logger.Log($"[ForceOpen] Resetting animation state for fresh open");
            _badgeAnimations?.ResetAnimationState(_viewModel.FilteredWindows);

            // Also hide badges immediately so there's no "visible then animate" flash.
            // FilteredWindows is a non-nullable invariant of MainViewModel (setter rejects null).
            foreach (var item in _viewModel.FilteredWindows)
            {
                if (item.IsShortcutVisible)
                {
                    item.ResetBadgeAnimation();
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

        /// <summary>Runs the initial window load: refreshes windows and settles selection at the top.</summary>
        public async Task InitialLoadAsync()
        {
            // Reset animation state once at start so all items can animate as they arrive
            if (_settingsService.Settings.EnableBadgeAnimations)
            {
                _badgeAnimations?.ResetAnimationState(_viewModel.FilteredWindows);
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

        /// <summary>Handles a results update: applies pending animation resets and triggers staggered animations.</summary>
        public void OnResultsUpdated(object? sender, EventArgs e)
        {
            _logger.Log($"[OnResultsUpdated] Called. IsVisible={_surface.IsVisible}, AnimationsEnabled={_settingsService.Settings.EnableBadgeAnimations}");

            // Capture intent BEFORE consuming the flag
            bool wasTextChange = _pendingAnimationReset;

            // Handle pending animation reset (e.g., from search text change or ForceOpen)
            // We do this HERE, on the new list, to ensure all currently visible items get reset.
            if (_pendingAnimationReset && _badgeAnimations != null && _viewModel.FilteredWindows != null)
            {
                _logger.Log($"[OnResultsUpdated] Applying pending animation reset to {_viewModel.FilteredWindows.Count} items.");
                _badgeAnimations.ResetAnimationState(_viewModel.FilteredWindows); // non-null: guarded above
                _pendingAnimationReset = false;
            }

            // When search results update, trigger staggered animation for new items (if enabled)
            if (_badgeAnimations != null && _surface.IsVisible && _settingsService.Settings.EnableBadgeAnimations && _viewModel.FilteredWindows != null)
            {
                // Debounce only for text-change triggers (typing), not hotkey opens or streaming updates.
                // _isForceOpenPending overrides: hotkey open always skips debounce.
                bool shouldDebounce = wasTextChange && !_isForceOpenPending;
                _isForceOpenPending = false;

                _ = _badgeAnimations.TriggerStaggeredAnimationAsync(_viewModel.FilteredWindows, skipDebounce: !shouldDebounce);
            }
            else if (_surface.IsVisible && !_settingsService.Settings.EnableBadgeAnimations && _viewModel.FilteredWindows != null)
            {
                // Ensure badges are visible immediately when animation is disabled
                foreach (var item in _viewModel.FilteredWindows)
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                }
            }
        }

        /// <summary>
        /// Pulses badges for rows renumbered by an update pass (option C), so streamed re-sorts read as intentional
        /// updates. No-op when badge animations are disabled or no animator is attached.
        /// </summary>
        public void OnItemsRenumbered(object? sender, IReadOnlyList<WindowItem>? items)
        {
            if (_badgeAnimations == null || !_settingsService.Settings.EnableBadgeAnimations || items == null)
            {
                return;
            }

            // Non-null: guarded above.
            var badgeAnimations = _badgeAnimations;
            foreach (var item in items)
            {
                badgeAnimations.PulseRenumber(item);
            }
        }

        /// <summary>Handles a search-text change: defers the animation reset to the next results update.</summary>
        public void OnSearchTextChanged(object? sender, EventArgs e)
        {
            // Reset animation state on ANY text change (typing or clearing).
            // User requested that re-animation happens on all modifications.
            // Defer the reset to OnResultsUpdated so it applies to the NEW list (post-filter).
            _pendingAnimationReset = true;
        }

        /// <summary>Activates a window (via its provider, or a native fallback) and hides the main window.</summary>
        public void ActivateWindow(WindowItem? windowItem)
        {
            if (windowItem == null)
            {
                return;
            }

            // Fail-safe: activation failures must never reach the WPF message loop and crash the app.
            try
            {
                if (windowItem.Source != null)
                {
                    ProviderActivator.TryActivate(windowItem, _logger);
                }
                else
                {
                    // Fallback for items without source
                    _logger.Log($"Warning: WindowItem '{windowItem.Title}' has no Source provider.");

                    // Basic fallback attempt
                    if (_windowInterop.IsIconic(windowItem.Hwnd))
                    {
                        _windowInterop.ShowWindow(windowItem.Hwnd, NativeInterop.SW_RESTORE);
                    }
                    _windowInterop.SetForegroundWindow(windowItem.Hwnd);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to activate window '{windowItem.Title}'", ex);
            }

            FadeOut(() => _surface.Hide());
        }
    }
}
