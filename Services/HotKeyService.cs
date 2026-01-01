using System;
using System.Windows;
using System.Windows.Interop;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    public class HotKeyService : IDisposable
    {
        private const int HOTKEY_ID = 9001;
        private const int WM_HOTKEY = 0x0312;
        private readonly Window _window;
        private readonly SettingsService _settingsService;
        private HwndSource? _source;
        private Action _onHotKeyPressed;

        public HotKeyService(Window window, SettingsService settingsService, Action onHotKeyPressed)
        {
            _window = window;
            _settingsService = settingsService;
            _onHotKeyPressed = onHotKeyPressed;
            
            _settingsService.SettingsChanged += OnSettingsChanged;

            if (_window.IsLoaded)
            {
                Window_Loaded(_window, new RoutedEventArgs());
            }
            else
            {
                _window.Loaded += Window_Loaded;
            }
            _window.Closing += Window_Closing;
        }

        private void OnSettingsChanged()
        {
            if (_source != null)
            {
                UnregisterHotKey(_source.Handle);
                RegisterHotKey(_source.Handle);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(_window);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source.AddHook(HwndHook);
            RegisterHotKey(helper.Handle);
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
            Interop.RegisterHotKey(handle, HOTKEY_ID, mods, key);
        }

        private void UnregisterHotKey(IntPtr handle)
        {
             Interop.UnregisterHotKey(handle, HOTKEY_ID);
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
