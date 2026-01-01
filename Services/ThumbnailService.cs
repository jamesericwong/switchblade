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

        private void UpdateThumbnailProperties()
        {
            if (_currentThumbnail == IntPtr.Zero) return;

            // TODO: Make these configurable or dynamic based on UI element
            int width = 300;
            int height = 200;
            int margin = 20;

            Interop.DWM_THUMBNAIL_PROPERTIES props = new Interop.DWM_THUMBNAIL_PROPERTIES();
            props.dwFlags = Interop.DWM_TNP_VISIBLE | Interop.DWM_TNP_RECTDESTINATION | Interop.DWM_TNP_OPACITY | Interop.DWM_TNP_SOURCECLIENTAREAONLY;
            props.fVisible = true;
            props.opacity = 255;
            props.fSourceClientAreaOnly = true;

            props.rcDestination = new Interop.Rect
            {
                Left = (int)_targetWindow.ActualWidth - width - margin,
                Top = (int)_targetWindow.ActualHeight - height - margin,
                Right = (int)_targetWindow.ActualWidth - margin,
                Bottom = (int)_targetWindow.ActualHeight - margin
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
