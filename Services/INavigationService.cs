namespace SwitchBlade.Services
{
    /// <summary>
    /// Manages the selection and navigation state for the window list.
    /// Encapsulates selection behaviors (PreserveScroll, Identity, Index) for testability.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Preserves or updates selection based on the configured refresh behavior.
        /// </summary>
        /// <param name="filteredWindows">Current filtered window list.</param>
        /// <param name="previousHwnd">Previously selected window handle.</param>
        /// <param name="previousTitle">Previously selected window title.</param>
        /// <param name="previousIndex">Previously selected index.</param>
        /// <param name="behavior">Refresh behavior (PreserveScroll, Identity, Index).</param>
        /// <param name="resetSelection">If true, forces selection to first item.</param>
        /// <returns>The WindowItem that should be selected, or null if list is empty.</returns>
        SwitchBlade.Contracts.WindowItem? ResolveSelection(
            System.Collections.Generic.IList<SwitchBlade.Contracts.WindowItem> filteredWindows,
            System.IntPtr? previousHwnd,
            string? previousTitle,
            int previousIndex,
            RefreshBehavior behavior,
            bool resetSelection);

        /// <summary>
        /// Calculates target selection index for vertical navigation.
        /// </summary>
        int CalculateMoveIndex(int currentIndex, int direction, int itemCount);

        /// <summary>
        /// Calculates target selection index for page-based navigation.
        /// </summary>
        int CalculatePageMoveIndex(int currentIndex, int direction, int pageSize, int itemCount);
    }
}
