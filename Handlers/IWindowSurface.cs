using System;

namespace SwitchBlade.Handlers
{
    /// <summary>
    /// Narrow seam over the main window's WPF surface so presentation-coordination logic
    /// (fade, force-open) stays testable without a real Window.
    /// Show/Hide/Activate/IsVisible/Opacity are satisfied by Window's own members when MainWindow implements this.
    /// </summary>
    public interface IWindowSurface
    {
        IntPtr Handle { get; }

        bool IsVisible { get; }

        double Opacity { get; set; }

        void Show();

        void Hide();

        void Activate();

        /// <summary>Restores the window to its normal (non-minimized) state.</summary>
        void NormalizeState();

        /// <summary>Moves keyboard focus into the search input.</summary>
        void FocusSearchInput();

        /// <summary>Animates window opacity from one value to another over the given duration.</summary>
        void AnimateOpacity(double from, double to, int durationMs, Action? onCompleted);
    }
}
