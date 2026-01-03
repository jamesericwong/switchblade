using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;
using SwitchBlade.Handlers;

namespace SwitchBlade
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger;
        private readonly KeyboardInputHandler _keyboardHandler;
        private readonly WindowResizeHandler _resizeHandler;

        private HotKeyService? _hotKeyService;
        private ThumbnailService? _thumbnailService;
        private BackgroundPollingService? _backgroundPollingService;
        private IntPtr _lastThumbnailHwnd = IntPtr.Zero;

        public List<IWindowProvider> Providers { get; private set; } = new List<IWindowProvider>();

        // Constructor Injection - Explicit Dependencies
        public MainWindow(
            MainViewModel viewModel,
            ISettingsService settingsService,
            IDispatcherService dispatcherService,
            ILogger logger)
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
                () => this.Hide(),
                ActivateWindow,
                () => ResultsConfig.ActualHeight);

            _resizeHandler = new WindowResizeHandler(this, _logger);

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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.Log($"MainWindow Loaded. Initial Size: {this.Width}x{this.Height}, ResizeMode: {this.ResizeMode}, Style: {this.WindowStyle}");

            // Initialize Services that require Window handle
            _hotKeyService = new HotKeyService(this, _settingsService, _logger, OnHotKeyPressed);
            _thumbnailService = new ThumbnailService(this, _logger);
            _thumbnailService.SetPreviewContainer(PreviewCanvas);

            // Initialize Background Polling Service
            _backgroundPollingService = new BackgroundPollingService(
                _settingsService,
                _dispatcherService,
                () => _viewModel.RefreshWindows());

            // Initial load
            this.Width = _settingsService.Settings.WindowWidth;
            this.Height = _settingsService.Settings.WindowHeight;

            _logger.Log($"Applied Settings Size: {this.Width}x{this.Height}");

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
            SwitchBlade.Contracts.NativeInterop.ForceForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            SearchBox.Focus();
            SearchBox.Text = "";

            FadeIn();
            _ = _viewModel.RefreshWindows();
            _logger.Log("Forced Open (Tray/Menu).");
        }

        private void OnHotKeyPressed()
        {
            _logger.Log($"Global Hotkey Pressed. Current Visibility: {this.Visibility}");
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

        private void ListBoxItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_settingsService.Settings.EnablePreviews)
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
