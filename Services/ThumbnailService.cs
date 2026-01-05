using System;
using System.Windows;
using System.Windows.Interop;
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

        public ThumbnailService(Window targetWindow, ILogger logger)
        {
            _targetWindow = targetWindow;
            _logger = logger;

            // Subscribe to window size changes (handles horizontal resize where container position changes)
            _targetWindow.SizeChanged += TargetWindow_SizeChanged;
        }

        private void TargetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Refresh thumbnail properties when the window size changes
            // This handles horizontal resizing where the preview container's position changes
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

            var helper = new WindowInteropHelper(_targetWindow);
            int result = NativeInterop.DwmRegisterThumbnail(helper.Handle, sourceHwnd, out _currentThumbnail);

            if (result == 0 && _currentThumbnail != IntPtr.Zero)
            {
                UpdateThumbnailProperties();
            }
            else
            {
                // Optional: log failure
                // _logger.Log($"DwmRegisterThumbnail failed for HWND {sourceHwnd}. Result: {result}");
            }
        }

        private FrameworkElement? _previewContainer;

        public void SetPreviewContainer(FrameworkElement element)
        {
            // Unsubscribe from previous container if any
            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged -= PreviewContainer_SizeChanged;
            }

            _previewContainer = element;

            // Subscribe to size changes to update thumbnail positioning
            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged += PreviewContainer_SizeChanged;
            }
        }

        private void PreviewContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Refresh thumbnail properties when the container size changes
            UpdateThumbnailProperties();
        }

        /// <summary>
        /// Manually refresh the thumbnail positioning (e.g., after window resize).
        /// </summary>
        public void RefreshThumbnail()
        {
            UpdateThumbnailProperties();
        }

        private void UpdateThumbnailProperties()
        {
            if (_currentThumbnail == IntPtr.Zero || _previewContainer == null) return;

            // Get position of the container relative to the window
            var transform = _previewContainer.TransformToAncestor(_targetWindow);
            var rootPoint = transform.Transform(new System.Windows.Point(0, 0));

            // Adjust for High DPI if necessary, but DWM usually expects physical pixels? 
            // WPF works in logical pixels. DWM works in physical pixels.
            // We need to convert.

            var source = PresentationSource.FromVisual(_targetWindow);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 1. Get source window dimensions
            NativeInterop.Rect sourceRect;
            NativeInterop.GetWindowRect(_currentSourceHwnd, out sourceRect);
            // Note: GetWindowRect might return 0 size if minimized? 
            // DWM usually handles minimized windows fine, but we need the restored size for aspect ratio.
            // If minimized, we might need GetWindowPlacement, but let's try GetWindowRect first.
            // Actually, for DWM thumbnail, it shows the "live" content. If minimized, it might be 0 or small.
            // However, typical Alt-Tab logic gets the "snapshot" size.

            // Safe fallback width/height
            double sourceW = sourceRect.Right - sourceRect.Left;
            double sourceH = sourceRect.Bottom - sourceRect.Top;

            if (sourceW <= 0 || sourceH <= 0)
            {
                sourceW = 800; // default assumption
                sourceH = 600;
            }

            // 2. Get Container Dimensions (Logical)
            double containerW = _previewContainer.ActualWidth;
            double containerH = _previewContainer.ActualHeight;

            // 3. Calculate Scale to Fit (Uniform)
            double scaleX = containerW / sourceW;
            double scaleY = containerH / sourceH;
            double scale = Math.Min(scaleX, scaleY);

            // 4. Calculate Final Dimensions (Logical)
            double destW = sourceW * scale;
            double destH = sourceH * scale;

            // 5. Center it
            double offsetX = (containerW - destW) / 2;
            double offsetY = (containerH - destH) / 2;

            // 6. Convert to Physical Pixels for DWM
            // DWM coordinates are relative to the *target window's client area* (if sourceClientAreaOnly is false?? no, relative to target window logic)
            // But they must be in physical pixels.
            // rootPoint from TransformToDevice is already in physical pixels?
            // Wait, TransformToAncestor -> Transform(0,0) gives coordinates relative to TargetWindow *visual*.
            // We need to apply DPI scaling to our calculated Logicals.

            int finalLeft = (int)((rootPoint.X * dpiX) + (offsetX * dpiX));
            int finalTop = (int)((rootPoint.Y * dpiY) + (offsetY * dpiY));
            int finalRight = finalLeft + (int)(destW * dpiX);
            int finalBottom = finalTop + (int)(destH * dpiY);

            // Add padding (optional, applied inside the calculated rect?)
            // Let's keep it tight or add small padding
            int paddingPixels = (int)(10 * dpiX);
            // Apply padding by shrinking the box slightly? Or leave as is.
            // Scaling already fits it inside container.

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
            // Unsubscribe from size changes
            if (_previewContainer != null)
            {
                _previewContainer.SizeChanged -= PreviewContainer_SizeChanged;
            }

            // Unsubscribe from window size changes
            _targetWindow.SizeChanged -= TargetWindow_SizeChanged;

            if (_currentThumbnail != IntPtr.Zero)
            {
                NativeInterop.DwmUnregisterThumbnail(_currentThumbnail);
                _currentThumbnail = IntPtr.Zero;
            }
        }
    }
}
