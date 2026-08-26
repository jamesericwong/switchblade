using System;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Focused abstraction over OS-level window queries, consumed by result-stabilization
    /// services such as <see cref="LastKnownGoodStrategy"/>.
    /// </summary>
    public interface IWindowIntrospection
    {
        /// <summary>
        /// Retrieves the PID that owns the given window handle.
        /// </summary>
        int GetPid(IntPtr hwnd);

        /// <summary>
        /// Retrieves the process name and executable path for a given PID.
        /// </summary>
        (string ProcessName, string? ExecutablePath) GetProcessInfo(uint pid);

        /// <summary>
        /// Checks whether a window handle is still valid/visible.
        /// </summary>
        bool IsWindowValid(IntPtr hwnd);
    }
}
