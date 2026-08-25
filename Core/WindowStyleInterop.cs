using System;
using SwitchBlade.Contracts;
using SwitchBlade.Handlers;

namespace SwitchBlade.Core
{
    /// <summary>Pure passthrough to the static NativeInterop styling calls.</summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class WindowStyleInterop : IWindowStyleInterop
    {
        public void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value)
        {
            NativeInterop.DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }

        public IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        {
            return NativeInterop.GetWindowLongPtr(hwnd, index);
        }

        public void SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        {
            NativeInterop.SetWindowLongPtr(hwnd, index, value);
        }

        public IntPtr GetWindow(IntPtr hwnd, uint cmd)
        {
            return NativeInterop.GetWindow(hwnd, cmd);
        }
    }
}
