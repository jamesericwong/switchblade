namespace SwitchBlade.Services
{
    /// <summary>
    /// Defines the behavior for preserving selection state when the window list refreshes.
    /// </summary>
    public enum RefreshBehavior
    {
        /// <summary>
        /// Preserve the current scroll position; selection may change silently.
        /// </summary>
        PreserveScroll,

        /// <summary>
        /// Preserve the selected item by identity (Hwnd+Title match).
        /// </summary>
        PreserveIdentity,

        /// <summary>
        /// Preserve the selected item's index position in the list.
        /// </summary>
        PreserveIndex
    }
}
