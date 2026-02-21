using System;
using System.Runtime.InteropServices;
using static SwitchBlade.Contracts.NativeInterop;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Abstraction for static NativeInterop methods to facilitate unit testing.
    /// </summary>
    public interface IWindowInterop
    {
        /// <see cref="NativeInterop.EnumWindows(EnumWindowsProc, IntPtr)"/>
        void EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        /// <see cref="NativeInterop.IsWindowVisible(IntPtr)"/>
        bool IsWindowVisible(IntPtr hWnd);

        /// <see cref="NativeInterop.GetWindowTextUnsafe(IntPtr, char*, int)"/>
        int GetWindowTextUnsafe(IntPtr hWnd, IntPtr lpString, int nMaxCount);

        /// <see cref="NativeInterop.GetProcessInfo(uint)"/>
        (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid);

        /// <see cref="NativeInterop.GetWindowThreadProcessId(IntPtr, out uint)"/>
        uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <see cref="NativeInterop.ForceForegroundWindow(IntPtr)"/>
        void ForceForegroundWindow(IntPtr hWnd);
    }
}
