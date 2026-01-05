using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;
using SwitchBlade.Handlers;
using WinRT.Interop;

namespace SwitchBlade
{
    /// <summary>
    /// Main application window - WinUI 3 version
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger;
        private ThumbnailService? _thumbnailService;
        private HotKeyService? _hotKeyService;
        private KeyboardInputHandler? _keyboardHandler;
        private readonly List<IWindowProvider> _providers = new();

        private AppWindow? _appWindow;
        private IntPtr _hwnd;

        public MainViewModel ViewModel => _viewModel;
        public IReadOnlyList<IWindowProvider> Providers => _providers;

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

            // Get the window handle for interop
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Configure window
            ConfigureWindow();

            // Set up the window
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;

            // Set data context for x:Bind
            // In WinUI, x:Bind uses the code-behind as the default data context
        }

        private void ConfigureWindow()
        {
            if (_appWindow != null)
            {
                // Set window size
                _appWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));

                // Extend content into title bar for custom chrome
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

                // Set title bar colors to transparent for custom appearance
                if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = _appWindow.TitleBar;
                    titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                }
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // Window is being activated
                InitializeServices();
            }
        }

        private bool _servicesInitialized = false;

        private void InitializeServices()
        {
            if (_servicesInitialized) return;
            _servicesInitialized = true;

            _logger.Log("MainWindow: Initializing services after activation");

            // Set up thumbnail service for DWM previews
            // _thumbnailService = new ThumbnailService(this, _logger);
            // _thumbnailService.SetPreviewContainer(PreviewCanvas);

            // Set up HotKey service
            // _hotKeyService = new HotKeyService(this, _settingsService, _logger, OnHotKeyPressed);

            // Set up keyboard input handler
            _keyboardHandler = new KeyboardInputHandler(_viewModel, _logger, _settingsService, ActivateWindow);

            // Subscribe to keyboard events
            SearchBox.KeyDown += SearchBox_KeyDown;

            // Subscribe to property changes for updating preview
            // _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initialize providers and windows
            InitializeProviders();
            _ = _viewModel.RefreshWindows();

            // Focus search box
            _dispatcherService.InvokeAsync(() =>
            {
                SearchBox.Focus(FocusState.Programmatic);
            });
        }

        private void InitializeProviders()
        {
            _logger.Log("MainWindow: Loading providers");
            _providers.Clear();

            // Add built-in provider
            _providers.Add(new WindowFinder());

            // Load plugins using proper API
            // var pluginsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Plugins");
            // var pluginLoader = new PluginLoader(pluginsPath);
            // var context = new PluginContext(_logger);
            // var plugins = pluginLoader.LoadPlugins(context);
            // foreach (var plugin in plugins)
            // {
            //     _providers.Add(plugin);
            // }

            // MainViewModel gets providers via DI, but keep _providers for Providers property
            _logger.Log($"MainWindow: Loaded {_providers.Count} providers");
        }

        public void ForceOpen()
        {
            _logger.Log("MainWindow: ForceOpen called");

            // Refresh windows
            _ = _viewModel.RefreshWindows();

            // Fade in effect using WinUI animation
            FadeIn();

            // Focus search box
            _dispatcherService.InvokeAsync(() =>
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            });
        }

        private void OnHotKeyPressed()
        {
            _logger.Log("MainWindow: Hotkey pressed");

            if (this.Visible)
            {
                FadeOut();
            }
            else
            {
                this.Activate();
                ForceOpen();
            }
        }

        private void FadeIn()
        {
            // Simple opacity animation for WinUI
            // TODO: Implement proper WinUI composition animation
            this.Activate();
        }

        private void FadeOut()
        {
            // Simple hide
            // TODO: Implement proper WinUI fade animation
            this.Hide();
        }

        public void Hide()
        {
            if (_appWindow != null)
            {
                _appWindow.Hide();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedWindow))
            {
                var selected = _viewModel.SelectedWindow;
                if (selected != null && _settingsService.Settings.EnablePreviews)
                {
                    _thumbnailService?.UpdateThumbnail(selected.Hwnd);
                }
                else
                {
                    _thumbnailService?.UpdateThumbnail(IntPtr.Zero);
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_keyboardHandler != null)
            {
                _keyboardHandler.HandleKeyDown(e);
            }
        }

        private void ActivateWindow(WindowItem window)
        {
            _logger.Log($"MainWindow: Activating window: {window.Title}");

            // Hide this window first
            FadeOut();

            // Switch to the target window
            NativeInterop.SetForegroundWindow(window.Hwnd);

            if (NativeInterop.IsIconic(window.Hwnd))
            {
                NativeInterop.ShowWindow(window.Hwnd, NativeInterop.SW_RESTORE);
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _logger.Log("MainWindow: Window closing");

            _thumbnailService?.Dispose();
            _hotKeyService?.Dispose();

            if (_viewModel is IDisposable disposableVm)
            {
                disposableVm.Dispose();
            }
        }
    }
}
