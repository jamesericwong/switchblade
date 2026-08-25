using System;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Seam over the static NativeInterop window-styling calls (DWM attributes, window styles)
    /// so the controller's native-appearance policy is unit-testable.
    /// </summary>
    public interface IWindowStyleInterop
    {
        /// <see cref="SwitchBlade.Contracts.NativeInterop.DwmSetWindowAttribute(IntPtr, int, ref int, int)"/> (value size fixed to sizeof(int)).
        void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value);

        /// <see cref="SwitchBlade.Contracts.NativeInterop.GetWindowLongPtr(IntPtr, int)"/>
        IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        /// <see cref="SwitchBlade.Contracts.NativeInterop.SetWindowLongPtr(IntPtr, int, IntPtr)"/>
        void SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        /// <see cref="SwitchBlade.Contracts.NativeInterop.GetWindow(IntPtr, uint)"/>
        IntPtr GetWindow(IntPtr hwnd, uint cmd);
    }
}
