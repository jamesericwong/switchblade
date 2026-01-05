using System;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    public class ThumbnailService : IDisposable
    {
        private IntPtr _currentThumbnail = IntPtr.Zero;
        private IntPtr _currentSourceHwnd = IntPtr.Zero;
        private readonly Window _targetWindow;
        private readonly ILogger _logger;
        private readonly IntPtr _targetHwnd;

        public ThumbnailService(Window targetWindow, ILogger logger)
        {
            _targetWindow = targetWindow;
            _logger = logger;
            _targetHwnd = WindowNative.GetWindowHandle(targetWindow);

            // Subscribe to window size changes
            _targetWindow.SizeChanged += TargetWindow_SizeChanged;
        }

        private void TargetWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateThumbnailProperties();
        }

        public void UpdateThumbnail(IntPtr sourceHwnd)
        {
            if (_currentThumbnail != IntPtr.Zero)
            {
                NativeInterop.DwmUnregisterThumbnail(_currentThumbnail);
                _currentThumbnail = IntPtr.Zero;
            }

            _currentSourceHwnd = sourceHwnd;
            if (sourceHwnd == IntPtr.Zero) return;

            int result = NativeInterop.DwmRegisterThumbnail(_targetHwnd, sourceHwnd, out _currentThumbnail);

            if (result == 0 && _currentThumbnail != IntPtr.Zero)
            {
                UpdateThumbnailProperties();
            }
        }

        private FrameworkElement? _previewContainer;

        public void SetPreviewContainer(FrameworkElement element)
        {
            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged -= PreviewContainer_SizeChanged;
            }

            _previewContainer = element;

            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged += PreviewContainer_SizeChanged;
            }
        }

        private void PreviewContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbnailProperties();
        }

        public void RefreshThumbnail()
        {
            UpdateThumbnailProperties();
        }

        private void UpdateThumbnailProperties()
        {
            if (_currentThumbnail == IntPtr.Zero || _previewContainer == null) return;

            // Get DPI for the window
            uint dpi = NativeInterop.GetDpiForWindow(_targetHwnd);
            double scale = dpi / 96.0;

            // Get source window dimensions
            NativeInterop.Rect sourceRect;
            NativeInterop.GetWindowRect(_currentSourceHwnd, out sourceRect);

            double sourceW = sourceRect.Right - sourceRect.Left;
            double sourceH = sourceRect.Bottom - sourceRect.Top;

            if (sourceW <= 0 || sourceH <= 0)
            {
                sourceW = 800;
                sourceH = 600;
            }

            // Get Container Dimensions
            double containerW = _previewContainer.ActualWidth;
            double containerH = _previewContainer.ActualHeight;

            // Calculate Scale to Fit
            double scaleX = containerW / sourceW;
            double scaleY = containerH / sourceH;
            double fitScale = Math.Min(scaleX, scaleY);

            // Calculate Final Dimensions
            double destW = sourceW * fitScale;
            double destH = sourceH * fitScale;

            // Center it
            double offsetX = (containerW - destW) / 2;
            double offsetY = (containerH - destH) / 2;

            // Get window position for offset calculation
            // For WinUI, we need to calculate the position differently
            // Using approximate values for now
            double rootX = 20; // Margin
            double rootY = 52; // Title bar + margin

            // Convert to Physical Pixels for DWM
            int finalLeft = (int)((rootX + offsetX) * scale);
            int finalTop = (int)((rootY + offsetY) * scale);
            int finalRight = finalLeft + (int)(destW * scale);
            int finalBottom = finalTop + (int)(destH * scale);

            int paddingPixels = (int)(10 * scale);

            NativeInterop.DWM_THUMBNAIL_PROPERTIES props = new NativeInterop.DWM_THUMBNAIL_PROPERTIES();
            props.dwFlags = NativeInterop.DWM_TNP_VISIBLE | NativeInterop.DWM_TNP_RECTDESTINATION | NativeInterop.DWM_TNP_OPACITY | NativeInterop.DWM_TNP_SOURCECLIENTAREAONLY;
            props.fVisible = true;
            props.opacity = 255;
            props.fSourceClientAreaOnly = true;

            props.rcDestination = new NativeInterop.Rect
            {
                Left = finalLeft + paddingPixels,
                Top = finalTop + paddingPixels,
                Right = finalRight - paddingPixels,
                Bottom = finalBottom - paddingPixels
            };

            NativeInterop.DwmUpdateThumbnailProperties(_currentThumbnail, ref props);
        }

        public void Dispose()
        {
            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged -= PreviewContainer_SizeChanged;
            }

            _targetWindow.SizeChanged -= TargetWindow_SizeChanged;

            if (_currentThumbnail != IntPtr.Zero)
            {
                NativeInterop.DwmUnregisterThumbnail(_currentThumbnail);
                _currentThumbnail = IntPtr.Zero;
            }
        }
    }
}
