using System;
using System.Diagnostics.CodeAnalysis;
using SwitchBlade.Contracts;

namespace SwitchBlade.Core
{
    [ExcludeFromCodeCoverage]
    public class WindowInterop : IWindowInterop
    {
        public void EnumWindows(NativeInterop.EnumWindowsProc callback, IntPtr lParam)
        {
            NativeInterop.EnumWindows(callback, lParam);
        }

        public bool IsWindowVisible(IntPtr hWnd)
        {
            return NativeInterop.IsWindowVisible(hWnd);
        }

        public unsafe int GetWindowTextUnsafe(IntPtr hWnd, IntPtr lpString, int nMaxCount)
        {
            return NativeInterop.GetWindowTextUnsafe(hWnd, (char*)lpString, nMaxCount);
        }

        public (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid)
        {
            return NativeInterop.GetProcessInfo(pid);
        }

        public uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId)
        {
            return NativeInterop.GetWindowThreadProcessId(hWnd, out lpdwProcessId);
        }

        public void ForceForegroundWindow(IntPtr hWnd)
        {
            NativeInterop.ForceForegroundWindow(hWnd);
        }
    }
}
