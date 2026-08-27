using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SwitchBlade.Core;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;
using SwitchBlade.Handlers;

namespace SwitchBlade
{
    [ExcludeFromCodeCoverage]
    public partial class MainWindow : Window, IWindowSurface
    {
        private readonly MainViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger;
        private readonly IUIService _uiService;
        private readonly WindowControllerService _controller;
        private readonly KeyboardInputHandler _keyboardHandler;
        private readonly WindowResizeHandler _resizeHandler;

        private readonly HotKeyService? _hotKeyService;
        private BackgroundPollingService? _backgroundPollingService;
        private BadgeAnimationService? _badgeAnimationService;
        private ThumbnailService? _thumbnailService;
        private IntPtr _lastThumbnailHwnd = IntPtr.Zero;

        public List<IWindowProvider> Providers { get; private set; } = [];

        // Constructor Injection - Explicit Dependencies
        public MainWindow(
            MainViewModel viewModel,
            ISettingsService settingsService,
            IDispatcherService dispatcherService,
            ILogger logger,
            INumberShortcutService numberShortcutService,
            IUIService uiService,
            IWindowStyleInterop styleInterop,
            IWindowInterop windowInterop)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _settingsService = settingsService;
            _dispatcherService = dispatcherService;
            _logger = logger;
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));

            // Sync Providers list with what's in the ViewModel (ViewModel is the source of truth for providers)
            Providers.Clear();
            foreach (var provider in _viewModel.WindowProviders)
            {
                Providers.Add(provider);
            }

            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Presentation coordinator: native appearance, fade/force-open orchestration and activation.
            _controller = new WindowControllerService(
                this,
                settingsService,
                dispatcherService,
                logger,
                viewModel,
                () => _uiService.IsModalDialogOpen,
                styleInterop,
                windowInterop);

            // Initialize handlers (extracted for SRP)
            _keyboardHandler = new KeyboardInputHandler(
                _viewModel,
                settingsService,
                numberShortcutService,
                () => this.Hide(),
                _controller.ActivateWindow,
                () => ResultsConfig.ListActualHeight,
                logger);

            _resizeHandler = new WindowResizeHandler(this, _logger);

            // Ensure the window handle (HWND) exists so we can register the global hotkey.
            // This is critical for /minimized startup where the window is never shown initially.
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            _logger.Log($"EnsureHandle completed. HWND: {helper.Handle}");

            // Initialize HotKeyService early so the global hotkey works even when starting minimized.
            // This must happen after EnsureHandle() because HotKeyService needs a valid HWND.
            _hotKeyService = new HotKeyService(this, settingsService, logger, OnHotKeyPressed);

            this.Loaded += MainWindow_Loaded;
            this.PreviewKeyDown += _keyboardHandler.HandleKeyDown;
        }

        // IWindowSurface implementation — WPF glue only (Show/Hide/IsVisible/Opacity come from Window).
        public IntPtr Handle => new WindowInteropHelper(this).Handle;

        // Window.Activate() returns bool, so the void interface member is implemented explicitly.
        void IWindowSurface.Activate() => this.Activate();

        public void NormalizeState() => WindowState = WindowState.Normal;

        public void FocusSearchInput() => SearchBox.FocusInput();

        public void AnimateOpacity(double from, double to, int durationMs, Action? onCompleted)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs));
            if (onCompleted != null)
            {
                anim.Completed += (_, _) => onCompleted();
            }

            BeginAnimation(Window.OpacityProperty, anim);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.Log($"MainWindow Loaded. Initial Size: {this.Width}x{this.Height}, ResizeMode: {this.ResizeMode}, Style: {this.WindowStyle}");

            // Initialize ThumbnailService - needs PreviewCanvas which isn't available until loaded
            _thumbnailService = new ThumbnailService(this, _logger);
            _thumbnailService.SetPreviewContainer(PreviewPanel.PreviewCanvas);

            // Initialize Badge Animation Service (view owns the item-to-container mapping)
            _badgeAnimationService = new BadgeAnimationService(
                new StoryboardBadgeAnimator(_dispatcherService, ResolveBadgeContainer),
                logger: _logger);
            _controller.SetBadgeAnimationService(_badgeAnimationService);
            _viewModel.ResultsUpdated += _controller.OnResultsUpdated;
            _viewModel.SearchTextChanged += _controller.OnSearchTextChanged;
            _viewModel.Renumbered += _controller.OnItemsRenumbered;

            // Interaction handlers from UserControl
            ResultsConfig.PreviewItemRequested += ResultList_PreviewItemRequested;
            ResultsConfig.ActivateItemRequested += ResultList_ActivateItemRequested;

            // Initialize Background Polling Service
            _backgroundPollingService = new BackgroundPollingService(
                _settingsService,
                _dispatcherService,
                () => _viewModel.RefreshWindows(),
                logger: _logger);

            // Initial load - apply saved size
            this.Width = _settingsService.Settings.WindowWidth;
            this.Height = _settingsService.Settings.WindowHeight;

            // Center the window based on the applied size
            // (WindowStartupLocation="CenterScreen" doesn't account for size changes after load)
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;

            _logger.Log($"Applied Settings Size: {this.Width}x{this.Height}, Centered at: ({this.Left}, {this.Top})");

            SearchBox.FocusInput();
            var hwnd = new WindowInteropHelper(this).Handle;
            _controller.ApplyBackdrop(hwnd);
            _controller.ConfigureWindowStyles(hwnd);
            _ = _controller.InitialLoadAsync();
        }

        public void ForceOpen() => _controller.ForceOpen();

        private void OnHotKeyPressed() => _controller.ToggleVisibility();

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

        private void ResultList_PreviewItemRequested(object? sender, WindowItem windowItem)
        {
            if (_settingsService.Settings.EnablePreviews)
            {
                _thumbnailService?.UpdateThumbnail(windowItem.Hwnd);
            }
        }

        private void ResultList_ActivateItemRequested(object? sender, WindowItem windowItem)
        {
            _controller.ActivateWindow(windowItem);
        }

        private void ResizeGripBottomRight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _resizeHandler.HandleBottomRightGripMouseDown(sender, e);

        private void ResizeGripBottomLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _resizeHandler.HandleBottomLeftGripMouseDown(sender, e);


        /// <summary>
        /// Maps a window item to its realized ListBoxItem badge container, or null while unrealized.
        /// </summary>
        private ListBoxItem? ResolveBadgeContainer(WindowItem item) =>
            ResultsConfig.InnerListBox?.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;

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
