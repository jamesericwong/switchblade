using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    [ExcludeFromCodeCoverage]
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

            // Get DPI scaling factors
            var source = PresentationSource.FromVisual(_targetWindow);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 1. Get source window dimensions (Client Area to match fSourceClientAreaOnly = true)
            double sourceW, sourceH;

            // Check if window is minimized
            if (NativeInterop.IsIconic(_currentSourceHwnd))
            {
                // For minimized windows, GetClientRect returns 0x0.
                // We must use GetWindowPlacement to find the "Restored" (Normal) position.
                NativeInterop.WINDOWPLACEMENT placement = new NativeInterop.WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(typeof(NativeInterop.WINDOWPLACEMENT));

                NativeInterop.GetWindowPlacement(_currentSourceHwnd, ref placement);

                // rcNormalPosition is the "Workspace Coordinates" of the restored window.
                // It includes the window frame. This is "close enough" and infinitely better than 0x0.
                // To be exact we'd need to subtract typical frame borders, but that varies by OS theme.
                sourceW = placement.rcNormalPosition.Right - placement.rcNormalPosition.Left;
                sourceH = placement.rcNormalPosition.Bottom - placement.rcNormalPosition.Top;
            }
            else
            {
                NativeInterop.Rect rect;

                // We use GetClientRect because we are limiting the thumbnail to the client area.
                // Using GetWindowRect would include borders/titlebar which are not shown, causing distortion.
                if (NativeInterop.GetClientRect(_currentSourceHwnd, out rect))
                {
                    sourceW = rect.Right - rect.Left;
                    sourceH = rect.Bottom - rect.Top;
                }
                else
                {
                    // Fallback to Window Rect if GetClientRect fails
                    NativeInterop.GetWindowRect(_currentSourceHwnd, out rect);
                    sourceW = rect.Right - rect.Left;
                    sourceH = rect.Bottom - rect.Top;
                }
            }

            if (sourceW <= 0) sourceW = 800;
            if (sourceH <= 0) sourceH = 600;

            // 2. Get Container Dimensions (Logical)
            double containerW = _previewContainer.ActualWidth;
            double containerH = _previewContainer.ActualHeight;

            // 3. Apply Padding BEFORE scaling
            // This ensures the aspect ratio is calculated against the available space, 
            // properly preserving the original shape.
            double padding = 10.0;
            double availableW = containerW - (padding * 2);
            double availableH = containerH - (padding * 2);

            if (availableW <= 0) availableW = 1;
            if (availableH <= 0) availableH = 1;

            // 4. Calculate Scale to Fit (Uniform)
            double scaleX = availableW / sourceW;
            double scaleY = availableH / sourceH;
            double scale = Math.Min(scaleX, scaleY);

            // 5. Calculate Final Dimensions (Logical)
            double destW = sourceW * scale;
            double destH = sourceH * scale;

            // 6. Center it within the full container
            double offsetX = (containerW - destW) / 2;
            double offsetY = (containerH - destH) / 2;

            // 7. Convert to Physical Pixels for DWM
            // DWM destination rect is relative to the target window's client area (in physical pixels).
            // We need to add the container's offset (rootPoint) to our calculated offset.

            // Apply DPI to the final coordinates
            int finalLeft = (int)((rootPoint.X + offsetX) * dpiX);
            int finalTop = (int)((rootPoint.Y + offsetY) * dpiY);
            int finalRight = finalLeft + (int)(destW * dpiX);
            int finalBottom = finalTop + (int)(destH * dpiY);

            NativeInterop.DWM_THUMBNAIL_PROPERTIES props = new NativeInterop.DWM_THUMBNAIL_PROPERTIES();
            props.dwFlags = NativeInterop.DWM_TNP_VISIBLE | NativeInterop.DWM_TNP_RECTDESTINATION | NativeInterop.DWM_TNP_OPACITY | NativeInterop.DWM_TNP_SOURCECLIENTAREAONLY;
            props.fVisible = true;
            props.opacity = 255;
            props.fSourceClientAreaOnly = true;

            props.rcDestination = new NativeInterop.Rect
            {
                Left = finalLeft,
                Top = finalTop,
                Right = finalRight,
                Bottom = finalBottom
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
