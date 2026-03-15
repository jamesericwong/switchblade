using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Shared native interop methods for window management.
    /// Consolidates P/Invoke declarations used by both Core and Plugins.
    /// Modernized for .NET 9+ high performance scenarios.
    /// This file contains shared Types, Constants, and Delegates.
    /// Implementation is split across partial classes in this directory.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static partial class NativeInterop
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public Rect rcDestination;
            public Rect rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rect rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Constants

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const uint GW_OWNER = 4;

        // Window Management
        public const int SW_RESTORE = 9;
        public const int WA_ACTIVE = 1;
        public const int WA_CLICKACTIVE = 2;

        // Modifiers
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        // DWM
        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_VISIBLE = 0x00000008;
        public const int DWM_TNP_OPACITY = 0x00000004;
        public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // DWM Backdrop Types
        public const int DWM_BACKDROP_NONE = 0;
        public const int DWM_BACKDROP_MICA = 2;
        public const int DWM_BACKDROP_ACRYLIC = 3;
        public const int DWM_BACKDROP_TABBED = 4;

        // DWM Window Corner Preferences
        public const int DWMWCP_DEFAULT = 0;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;
        public const int DWMWCP_ROUNDSMALL = 3;

        // SysCommands
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_SIZE = 0xF000;
        public const int SC_SIZE_HTBOTTOMRIGHT = 8;
        public const int SC_SIZE_HTBOTTOMLEFT = 7;
        public const int HTBOTTOMRIGHT = 17;

        // Process Access Flags
        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_VM_READ = 0x0010;

        #endregion
    }
}
