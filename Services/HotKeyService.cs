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
            RegisterHotKey(handle);
        }

        private void OnSettingsChanged()
        {
            _logger.Log("HotKeyService: OnSettingsChanged triggered - re-registering hotkey");
            if (_source != null)
            {
                bool unregSuccess = UnregisterHotKey(_source.Handle);
                _logger.Log($"HotKeyService: Unregister result: {unregSuccess}");
                RegisterHotKey(_source.Handle);
            }
            else
            {
                _logger.Log("HotKeyService: OnSettingsChanged - _source is null, cannot re-register");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            }
        }

        private void RegisterHotKey(IntPtr handle)
        {
            var mods = _settingsService.Settings.HotKeyModifiers;
            var key = _settingsService.Settings.HotKeyKey;
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

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onHotKeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
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
