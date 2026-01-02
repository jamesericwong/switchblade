using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;

namespace SwitchBlade
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private HotKeyService? _hotKeyService;
        private ThumbnailService? _thumbnailService;
        private ChromeTabFinder? _chromeTabFinder;

        public MainWindow()
        {
            InitializeComponent();
            
            var app = (App)System.Windows.Application.Current;
            var settingsService = app.SettingsService;

            // Composition Root (Manual DI for now)
            // Composition Root (Manual DI for now)
            _chromeTabFinder = new ChromeTabFinder(settingsService);
            var providers = new List<IWindowProvider>
            {
                new WindowFinder(settingsService),
                _chromeTabFinder
            };
            
            _viewModel = new MainViewModel(providers, settingsService);
            DataContext = _viewModel;
            
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Loaded += MainWindow_Loaded;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Log($"MainWindow Loaded. Initial Size: {this.Width}x{this.Height}, ResizeMode: {this.ResizeMode}, Style: {this.WindowStyle}");

            // Initialize Services that require Window handle
            _hotKeyService = new HotKeyService(this, ((App)System.Windows.Application.Current).SettingsService, OnHotKeyPressed);
            _thumbnailService = new ThumbnailService(this);
            _thumbnailService.SetPreviewContainer(PreviewCanvas);

            // Initial load
            var app = (App)System.Windows.Application.Current;
            this.Width = app.SettingsService.Settings.WindowWidth;
            this.Height = app.SettingsService.Settings.WindowHeight;
            
            Logger.Log($"Applied Settings Size: {this.Width}x{this.Height}");

            SearchBox.Focus();
            _ = _viewModel.RefreshWindows();
        }

        private void OnHotKeyPressed()
        {
            Logger.Log($"Global Hotkey Pressed. Current Visibility: {this.Visibility}");
            if (this.Visibility == Visibility.Visible)
            {
                Logger.Log("Hiding Window.");
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
                Logger.Log("Showing Window (Activated & Focused).");
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
            Logger.Log($"Resize Grip (Bottom-Right) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try 
                {
                    // Manual Resize via System Command
                    Interop.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle, 
                                        Interop.WM_SYSCOMMAND, 
                                        (IntPtr)(Interop.SC_SIZE + Interop.SC_SIZE_HTBOTTOMRIGHT), 
                                        IntPtr.Zero);
                    Logger.Log("Sent SC_SIZE + HTBOTTOMRIGHT command.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resize Grip Error", ex);
                }
            }
        }

        private void ResizeGripBottomLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Logger.Log($"Resize Grip (Bottom-Left) Clicked. ButtonState: {e.ButtonState}");
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try 
                {
                    // Manual Resize via System Command - Bottom-Left corner
                    Interop.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle, 
                                        Interop.WM_SYSCOMMAND, 
                                        (IntPtr)(Interop.SC_SIZE + Interop.SC_SIZE_HTBOTTOMLEFT), 
                                        IntPtr.Zero);
                    Logger.Log("Sent SC_SIZE + HTBOTTOMLEFT command.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Resize Grip Error", ex);
                }
            }
        }

        private void ActivateWindow(WindowItem? windowItem)
        {
            if (windowItem != null)
            {
                if (windowItem.IsChromeTab)
                {
                    // Find the Chrome provider (it's one of our providers)
                    // Since we didn't store it in a field, we look it up or cast
                    // ideally we should have stored it. 
                    // Quick fix: New up a temp one? No, bad for dependency.
                    // Better: The ViewModel has the providers? No.
                    // We can match based on IsChromeTab.
                    
                    // Actually, let's just use the one we created in Constructor if we can access it.
                    // We need to promote 'providers' to a class field or '_chromeTabFinder' to a field.
                    // Let's rely on the field '_chromeTabFinder' we will create in the next step.
                    _chromeTabFinder?.ActivateWindow(windowItem);
                }
                else
                {
                    // Robust window activation for standard apps
                    if (Interop.IsIconic(windowItem.Hwnd))
                    {
                        Interop.ShowWindow(windowItem.Hwnd, Interop.SW_RESTORE);
                    }
                    
                    // Try simple switch first (often works better than SetForeground for task switching)
                    Interop.SwitchToThisWindow(windowItem.Hwnd, true);
                    
                    if (Interop.GetForegroundWindow() != windowItem.Hwnd)
                    {
                        // Fallback: The "AttachThreadInput" hack to steal focus
                        uint dummyPid;
                        var foregroundThreadId = Interop.GetWindowThreadProcessId(Interop.GetForegroundWindow(), out dummyPid);
                        var myThreadId = Interop.GetCurrentThreadId();
                        
                        if (foregroundThreadId != myThreadId)
                        {
                            Interop.AttachThreadInput(myThreadId, foregroundThreadId, true);
                            Interop.SetForegroundWindow(windowItem.Hwnd);
                            Interop.AttachThreadInput(myThreadId, foregroundThreadId, false);
                        }
                        else
                        {
                             Interop.SetForegroundWindow(windowItem.Hwnd);
                        }
                    }
                }

                FadeOut(() => this.Hide());
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only log non-character keys to avoid spam, or log special keys
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Down || e.Key == Key.Up)
            {
                 Logger.Log($"MainWindow KeyDown: {e.Key}");
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
            base.OnClosed(e);
        }
    }
}