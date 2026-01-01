using System;
using System.Windows;
using System.Windows.Interop;
using SwitchBlade.Core;

namespace SwitchBlade.Services
{
    public class ThumbnailService : IDisposable
    {
        private IntPtr _currentThumbnail = IntPtr.Zero;
        private readonly Window _targetWindow;

        public ThumbnailService(Window targetWindow)
        {
            _targetWindow = targetWindow;
        }

        public void UpdateThumbnail(IntPtr sourceHwnd)
        {
            if (_currentThumbnail != IntPtr.Zero)
            {
                Interop.DwmUnregisterThumbnail(_currentThumbnail);
                _currentThumbnail = IntPtr.Zero;
            }

            if (sourceHwnd == IntPtr.Zero) return;

            var helper = new WindowInteropHelper(_targetWindow);
            int result = Interop.DwmRegisterThumbnail(helper.Handle, sourceHwnd, out _currentThumbnail);

            if (result == 0 && _currentThumbnail != IntPtr.Zero)
            {
                UpdateThumbnailProperties();
            }
        }

        private FrameworkElement? _previewContainer;

        public void SetPreviewContainer(FrameworkElement element)
        {
            _previewContainer = element;
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

            // Calculate dimensions
            int left = (int)(rootPoint.X * dpiX);
            int top = (int)(rootPoint.Y * dpiY);
            int width = (int)(_previewContainer.ActualWidth * dpiX);
            int height = (int)(_previewContainer.ActualHeight * dpiY);

            // Add some padding inside the container
            int padding = (int)(10 * dpiX);

            Interop.DWM_THUMBNAIL_PROPERTIES props = new Interop.DWM_THUMBNAIL_PROPERTIES();
            props.dwFlags = Interop.DWM_TNP_VISIBLE | Interop.DWM_TNP_RECTDESTINATION | Interop.DWM_TNP_OPACITY | Interop.DWM_TNP_SOURCECLIENTAREAONLY;
            props.fVisible = true;
            props.opacity = 255;
            props.fSourceClientAreaOnly = true;

            props.rcDestination = new Interop.Rect
            {
                Left = left + padding,
                Top = top + padding,
                Right = left + width - padding,
                Bottom = top + height - padding
            };

            Interop.DwmUpdateThumbnailProperties(_currentThumbnail, ref props);
        }

        public void Dispose()
        {
            if (_currentThumbnail != IntPtr.Zero)
            {
                Interop.DwmUnregisterThumbnail(_currentThumbnail);
                _currentThumbnail = IntPtr.Zero;
            }
        }
    }
}
