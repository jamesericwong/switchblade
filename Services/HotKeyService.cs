using System;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    public class HotKeyService : IDisposable
    {
        private const int HOTKEY_ID = 9001;
        private const int WM_HOTKEY = 0x0312;
        private readonly Window _window;
        private readonly ISettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly IntPtr _hwnd;
        private Action _onHotKeyPressed;
        private NativeInterop.WndProc? _wndProcDelegate;
        private IntPtr _oldWndProc;

        public HotKeyService(Window window, ISettingsService settingsService, ILogger logger, Action onHotKeyPressed)
        {
            _window = window;
            _settingsService = settingsService;
            _logger = logger;
            _onHotKeyPressed = onHotKeyPressed;

            _hwnd = WindowNative.GetWindowHandle(window);

            _settingsService.SettingsChanged += OnSettingsChanged;

            // Set up message hook via subclassing
            SetupMessageHook();
            RegisterHotKey();
        }

        private void SetupMessageHook()
        {
            // Subclass the window to intercept WM_HOTKEY messages
            _wndProcDelegate = new NativeInterop.WndProc(WndProc);
            _oldWndProc = NativeInterop.SetWindowLongPtr(_hwnd, NativeInterop.GWLP_WNDPROC,
                System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _onHotKeyPressed?.Invoke();
                return IntPtr.Zero;
            }
            return NativeInterop.CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        }

        private void OnSettingsChanged()
        {
            _logger.Log("HotKeyService: OnSettingsChanged triggered - re-registering hotkey");
            UnregisterHotKey();
            RegisterHotKey();
        }

        private void RegisterHotKey()
        {
            var mods = _settingsService.Settings.HotKeyModifiers;
            var key = _settingsService.Settings.HotKeyKey;
            _logger.Log($"HotKeyService: Attempting to register hotkey. Mods: {mods}, Key: {key:X}");

            bool success = NativeInterop.RegisterHotKey(_hwnd, HOTKEY_ID, mods, key);
            if (!success)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                _logger.LogError($"Failed to register hotkey. Mods: {mods}, Key: {key:X}, Win32Error: {error}",
                    new System.ComponentModel.Win32Exception(error));
            }
            else
            {
                _logger.Log($"HotKeyService: Successfully registered hotkey. Mods: {mods}, Key: {key:X}");
            }
        }

        private void UnregisterHotKey()
        {
            bool result = NativeInterop.UnregisterHotKey(_hwnd, HOTKEY_ID);
            if (!result)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                _logger.Log($"HotKeyService: UnregisterHotKey failed, Win32Error: {error}");
            }
        }

        public void Dispose()
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
            UnregisterHotKey();

            // Restore original window procedure
            if (_oldWndProc != IntPtr.Zero)
            {
                NativeInterop.SetWindowLongPtr(_hwnd, NativeInterop.GWLP_WNDPROC, _oldWndProc);
            }
        }
    }
}
