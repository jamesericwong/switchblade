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
                // Apply Settings
                var app = (App)System.Windows.Application.Current;
                this.Opacity = 0; // Start transparent for fade in
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                SearchBox.Focus();
                SearchBox.Text = "";
                
                FadeIn();
                _ = _viewModel.RefreshWindows();
                SwitchBlade.Core.Logger.Log("Showing Window (Activated & Focused).");
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
                    _thumbnailService?.UpdateThumbnail(_viewModel.SelectedWindow.Hwnd);
                    ResultsConfig.ScrollIntoView(_viewModel.SelectedWindow);
                }
                else
                {
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