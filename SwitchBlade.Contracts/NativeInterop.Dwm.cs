using System;
using System.Runtime.InteropServices;

namespace SwitchBlade.Contracts
{
    public static partial class NativeInterop
    {
        [LibraryImport("dwmapi.dll")]
        public static partial int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUnregisterThumbnail(IntPtr thumb);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
    }
}
