using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
    public class HotKeyService : IDisposable
    {
        private const int HOTKEY_ID = 9001;
        private const int WM_HOTKEY = 0x0312;
        private readonly Window _window;
        private readonly ISettingsService _settingsService;
        private readonly ILogger _logger;
        private HwndSource? _source;
        private Action _onHotKeyPressed;
        private bool _disposed;

        // Tracks the last successfully registered hotkey so unrelated settings changes
        // don't churn a working registration (unregister + re-register gap, spurious failures).
        private uint _lastRegisteredMods;
        private uint _lastRegisteredKey;
        private bool _hotKeyRegistered;

        public HotKeyService(Window window, ISettingsService settingsService, ILogger logger, Action onHotKeyPressed)
        {
            _window = window;
            _settingsService = settingsService;
            _logger = logger;
            _onHotKeyPressed = onHotKeyPressed;

            _settingsService.SettingsChanged += OnSettingsChanged;

            // Check if the window already has a handle (e.g., from EnsureHandle()).
            // IsLoaded is false until the window is shown, but we can still register
            // the hotkey if the HWND exists (critical for /minimized startup).
            var helper = new WindowInteropHelper(_window);
            if (helper.Handle != IntPtr.Zero)
            {
                _logger.Log($"HotKeyService: Window already has handle {helper.Handle}, registering immediately");
                InitializeHotKey(helper.Handle);
            }
            else if (_window.IsLoaded)
            {
                _logger.Log("HotKeyService: Window is loaded, registering hotkey");
                InitializeHotKey(new WindowInteropHelper(_window).Handle);
            }
            else
            {
                _logger.Log("HotKeyService: Window not ready, waiting for Loaded event");
                _window.Loaded += Window_Loaded;
            }
            _window.Closing += Window_Closing;
        }

        private void InitializeHotKey(IntPtr handle)
        {
            _source = HwndSource.FromHwnd(handle);
            _source.AddHook(HwndHook);

            var mods = _settingsService.Settings.HotKeyModifiers;
            var key = _settingsService.Settings.HotKeyKey;
            if (RegisterHotKey(handle, mods, key))
            {
                RecordRegistration(mods, key);
            }
        }

        private void OnSettingsChanged()
        {
            var mods = _settingsService.Settings.HotKeyModifiers;
            var key = _settingsService.Settings.HotKeyKey;

            if (_hotKeyRegistered && mods == _lastRegisteredMods && key == _lastRegisteredKey)
            {
                // Unrelated settings change: keep the working registration instead of churning it.
                _logger.Log("HotKeyService: Hotkey settings unchanged, keeping existing registration");
                return;
            }

            if (_source == null)
            {
                _logger.Log("HotKeyService: OnSettingsChanged - no window source yet, cannot (re-)register hotkey");
                return;
            }

            UnregisterHotKey(_source.Handle);
            _hotKeyRegistered = false; // re-asserted below only if registration succeeds

            if (RegisterHotKey(_source.Handle, mods, key))
            {
                RecordRegistration(mods, key);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Guard: If already disposed, the subscription is being torn down - never initialize on a dead service.
            if (_disposed)
            {
                _logger.Log("HotKeyService: Window_Loaded called after dispose, skipping");
                return;
            }

            // Guard: If already initialized (e.g., from EnsureHandle()), skip
            if (_source != null)
            {
                _logger.Log("HotKeyService: Window_Loaded called but already initialized, skipping");
                return;
            }

            var helper = new WindowInteropHelper(_window);
            _logger.Log($"HotKeyService: Window_Loaded, initializing with handle {helper.Handle}");
            InitializeHotKey(helper.Handle);
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_source != null)
            {
                UnregisterHotKey(_source.Handle);
                _hotKeyRegistered = false;
            }
        }

        private bool RegisterHotKey(IntPtr handle, uint mods, uint key)
        {
            _logger.Log($"HotKeyService: Attempting to register hotkey. Mods: {mods}, Key: {key:X} (0x{key:X})");
            bool success = NativeInterop.RegisterHotKey(handle, HOTKEY_ID, mods, key);
            if (!success)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                _logger.LogError($"Failed to register hotkey. Mods: {mods}, Key: {key:X}, Win32Error: {error}", new System.ComponentModel.Win32Exception(error));
            }
            else
            {
                _logger.Log($"HotKeyService: Successfully registered hotkey. Mods: {mods}, Key: {key:X}");
            }

            return success;
        }

        private void RecordRegistration(uint mods, uint key)
        {
            _hotKeyRegistered = true;
            _lastRegisteredMods = mods;
            _lastRegisteredKey = key;
        }

        private bool UnregisterHotKey(IntPtr handle)
        {
            bool result = NativeInterop.UnregisterHotKey(handle, HOTKEY_ID);
            if (!result)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                _logger.Log($"HotKeyService: UnregisterHotKey failed, Win32Error: {error}");
            }
            return result;
        }

        internal IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                // Exceptions here would propagate into the WPF message loop and crash the app.
                try
                {
                    _onHotKeyPressed?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unhandled exception in hotkey handler", ex);
                }

                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Unsubscribe before releasing the hotkey so a late Loaded event can never re-initialize.
            _window.Loaded -= Window_Loaded;
            _window.Closing -= Window_Closing;
            _settingsService.SettingsChanged -= OnSettingsChanged;

            if (_source != null)
            {
                UnregisterHotKey(_source.Handle);
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
