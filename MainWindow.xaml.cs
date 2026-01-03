using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private HotKeyService? _hotKeyService;
        private ThumbnailService? _thumbnailService;
        private BackgroundPollingService? _backgroundPollingService;
        private IntPtr _lastThumbnailHwnd = IntPtr.Zero;
        public List<IWindowProvider> Providers { get; private set; } = new List<IWindowProvider>();

        public MainWindow()
        {
            InitializeComponent();
            
            var app = (App)System.Windows.Application.Current;
            var settingsService = app.SettingsService;

            // Composition Root
            // var providers = new List<IWindowProvider>(); // Replaced by property
            
            // 1. Internal Providers
            Providers.Add(new WindowFinder());
            // providers.Add(new ChromeTabFinder()); // Moved to external plugin

            // 2. Load Plugins
            try
            {
                var pluginPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                var loader = new PluginLoader(pluginPath);
                var plugins = loader.LoadPlugins();
                Providers.AddRange(plugins);
            }
            catch (Exception ex)
            {
                SwitchBlade.Core.Logger.LogError("Error loading plugins", ex);
            }

            // 3. Initialize all providers
            var loggerBridge = new LoggerBridge();
            foreach (var provider in Providers)
            {
                provider.Initialize(settingsService, loggerBridge);
            }
            
            _viewModel = new MainViewModel(Providers, settingsService);
            DataContext = _viewModel;
            
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Loaded += MainWindow_Loaded;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SwitchBlade.Core.Logger.Log($"MainWindow Loaded. Initial Size: {this.Width}x{this.Height}, ResizeMode: {this.ResizeMode}, Style: {this.WindowStyle}");

            // Initialize Services that require Window handle
            _hotKeyService = new HotKeyService(this, ((App)System.Windows.Application.Current).SettingsService, OnHotKeyPressed);
            _thumbnailService = new ThumbnailService(this);
            _thumbnailService.SetPreviewContainer(PreviewCanvas);

            // Initialize Background Polling Service
            _backgroundPollingService = new BackgroundPollingService(
                ((App)System.Windows.Application.Current).SettingsService,
                () => _viewModel.RefreshWindows());

            // Initial load
            var app = (App)System.Windows.Application.Current;
            this.Width = app.SettingsService.Settings.WindowWidth;
            this.Height = app.SettingsService.Settings.WindowHeight;
            
            SwitchBlade.Core.Logger.Log($"Applied Settings Size: {this.Width}x{this.Height}");

            SearchBox.Focus();
            _ = _viewModel.RefreshWindows();
        }

        public void ForceOpen()
        {
             // Apply Settings
             var app = (App)System.Windows.Application.Current;
             this.Opacity = 0; // Start transparent for fade in
             this.Show();
             this.WindowState = WindowState.Normal;
             this.Activate();
             SwitchBlade.Core.Interop.ForceForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
             
             SearchBox.Focus();
             SearchBox.Text = "";
             
             FadeIn();
             _ = _viewModel.RefreshWindows();
             SwitchBlade.Core.Logger.Log("Forced Open (Tray/Menu).");
        }

        private void OnHotKeyPressed()
        {
            SwitchBlade.Core.Logger.Log($"Global Hotkey Pressed. Current Visibility: {this.Visibility}");
            if (this.Visibility == Visibility.Visible)
            {
                SwitchBlade.Core.Logger.Log("Hiding Window.");
                FadeOut(() => this.Hide());
            }
            else
            {
               ForceOpen();
            }
        }

        private void FadeIn()
        {
            var app = (App)System.Windows.Application.Current;
            var duration = app.SettingsService.Settings.FadeDurationMs;
            var targetOpacity = app.SettingsService.Settings.WindowOpacity;

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
            var app = (App)System.Windows.Application.Current;
            var duration = app.SettingsService.Settings.FadeDurationMs;

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
                var app = (App)System.Windows.Application.Current;
                if (app.SettingsService.Settings.EnablePreviews && _viewModel.SelectedWindow != null)
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

        private void ListBoxItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var app = (App)System.Windows.Application.Current;
            if (app.SettingsService.Settings.EnablePreviews)
            {
                if (sender is ListBoxItem item && item.DataContext is WindowItem windowItem)
                {
                    _thumbnailService?.UpdateThumbnail(windowItem.Hwnd);
                }
            }
        }

        private void ListBoxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is WindowItem windowItem)
            {
                ActivateWindow(windowItem);
            }
        }

        private void ResizeGripBottomRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SwitchBlade.Core.Logger.Log($"Resize Grip (Bottom-Right) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try 
                {
                    // Manual Resize via System Command
                    SwitchBlade.Core.Interop.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle, 
                                        SwitchBlade.Core.Interop.WM_SYSCOMMAND, 
                                        (IntPtr)(SwitchBlade.Core.Interop.SC_SIZE + SwitchBlade.Core.Interop.SC_SIZE_HTBOTTOMRIGHT), 
                                        IntPtr.Zero);
                    SwitchBlade.Core.Logger.Log("Sent SC_SIZE + HTBOTTOMRIGHT command.");
                }
                catch (Exception ex)
                {
                    SwitchBlade.Core.Logger.LogError("Resize Grip Error", ex);
                }
            }
        }

        private void ResizeGripBottomLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SwitchBlade.Core.Logger.Log($"Resize Grip (Bottom-Left) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try 
                {
                    // Manual Resize via System Command - Bottom-Left corner
                    SwitchBlade.Core.Interop.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle, 
                                        SwitchBlade.Core.Interop.WM_SYSCOMMAND, 
                                        (IntPtr)(SwitchBlade.Core.Interop.SC_SIZE + SwitchBlade.Core.Interop.SC_SIZE_HTBOTTOMLEFT), 
                                        IntPtr.Zero);
                    SwitchBlade.Core.Logger.Log("Sent SC_SIZE + HTBOTTOMLEFT command.");
                }
                catch (Exception ex)
                {
                    SwitchBlade.Core.Logger.LogError("Resize Grip Error", ex);
                }
            }
        }

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
                    // Fallback for items without source (shouldn't happen with new architecture, but safe to keep basic activation?)
                    // Actually, if we didn't migrate everything perfectly, might crash.
                    // But we did migrate WindowFinder and ChromeTabFinder.
                    // Let's log if source is missing.
                    SwitchBlade.Core.Logger.Log($"Warning: WindowItem '{windowItem.Title}' has no Source provider.");
                    
                    // Basic fallback attempt
                     if (SwitchBlade.Core.Interop.IsIconic(windowItem.Hwnd))
                     {
                         SwitchBlade.Core.Interop.ShowWindow(windowItem.Hwnd, SwitchBlade.Core.Interop.SW_RESTORE);
                     }
                     SwitchBlade.Core.Interop.SetForegroundWindow(windowItem.Hwnd);
                }

                FadeOut(() => this.Hide());
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only log non-character keys to avoid spam, or log special keys
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Down || e.Key == Key.Up)
            {
                 SwitchBlade.Core.Logger.Log($"MainWindow KeyDown: {e.Key}");
            }

            if (e.Key == Key.Escape)
            {
                this.Hide(); // Don't close, just hide
            }
            else if (e.Key == Key.Down)
            {
                _viewModel.MoveSelection(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _viewModel.MoveSelection(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ActivateWindow(_viewModel.SelectedWindow);
                e.Handled = true;
            }
            else if (e.Key == Key.Home && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToFirst();
                e.Handled = true;
            }
            else if (e.Key == Key.End && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _viewModel.MoveSelectionToLast();
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(-1, pageSize);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                int pageSize = CalculatePageSize();
                _viewModel.MoveSelectionByPage(1, pageSize);
                e.Handled = true;
            }
            // Number Shortcuts Feature
            else if (((App)System.Windows.Application.Current).SettingsService.Settings.EnableNumberShortcuts)
            {
                var settings = ((App)System.Windows.Application.Current).SettingsService.Settings;
                // Check if the required modifier key is pressed
                if (IsModifierKeyPressed(settings.NumberShortcutModifier))
                {
                    // When Alt is pressed, WPF sets e.Key to Key.System and the actual key is in e.SystemKey
                    Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
                    int? index = GetNumberKeyIndex(actualKey);
                    if (index.HasValue)
                    {
                        ActivateWindowByIndex(index.Value);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the number of items visible in the ListBox (page size).
        /// </summary>
        private int CalculatePageSize()
        {
            var app = (App)System.Windows.Application.Current;
            double itemHeight = app.SettingsService.Settings.ItemHeight;
            if (itemHeight <= 0) itemHeight = 50; // Fallback default
            
            double listBoxHeight = ResultsConfig.ActualHeight;
            if (listBoxHeight <= 0) listBoxHeight = 400; // Fallback default
            
            int pageSize = (int)(listBoxHeight / itemHeight);
            return Math.Max(1, pageSize); // At least 1
        }

        /// <summary>
        /// Checks if the specified modifier key is currently pressed.
        /// Modifier values: Alt=1, Ctrl=2, Shift=4, None=0
        /// </summary>
        private bool IsModifierKeyPressed(uint modifier)
        {
            return modifier switch
            {
                0 => true, // No modifier required
                1 => Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
                2 => Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
                4 => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
                _ => false
            };
        }

        /// <summary>
        /// Maps a key to a window index (0-9). Returns null if the key is not a number key.
        /// Keys 1-9 map to indices 0-8, key 0 maps to index 9.
        /// </summary>
        private int? GetNumberKeyIndex(Key key)
        {
            return key switch
            {
                Key.D1 or Key.NumPad1 => 0,
                Key.D2 or Key.NumPad2 => 1,
                Key.D3 or Key.NumPad3 => 2,
                Key.D4 or Key.NumPad4 => 3,
                Key.D5 or Key.NumPad5 => 4,
                Key.D6 or Key.NumPad6 => 5,
                Key.D7 or Key.NumPad7 => 6,
                Key.D8 or Key.NumPad8 => 7,
                Key.D9 or Key.NumPad9 => 8,
                Key.D0 or Key.NumPad0 => 9,
                _ => null
            };
        }

        /// <summary>
        /// Activates a window by its index in the filtered list.
        /// </summary>
        private void ActivateWindowByIndex(int index)
        {
            if (index >= 0 && index < _viewModel.FilteredWindows.Count)
            {
                var windowItem = _viewModel.FilteredWindows[index];
                SwitchBlade.Core.Logger.Log($"Number shortcut activated: index {index} -> '{windowItem.Title}'");
                ActivateWindow(windowItem);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var app = (App)System.Windows.Application.Current;
            app.SettingsService.Settings.WindowWidth = this.Width;
            app.SettingsService.Settings.WindowHeight = this.Height;
            app.SettingsService.SaveSettings();

            _hotKeyService?.Dispose();
            _thumbnailService?.Dispose();
            _backgroundPollingService?.Dispose();
            base.OnClosed(e);
        }
    }
}