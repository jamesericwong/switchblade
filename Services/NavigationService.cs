using System;
using System.Collections.Generic;
using System.Linq;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Implementation of INavigationService.
    /// Handles selection preservation and navigation calculations.
    /// </summary>
    public class NavigationService : INavigationService
    {
        public WindowItem? ResolveSelection(
            IList<WindowItem> filteredWindows,
            IntPtr? previousHwnd,
            string? previousTitle,
            int previousIndex,
            RefreshBehavior behavior,
            bool resetSelection)
        {
            if (filteredWindows == null || filteredWindows.Count == 0)
                return null;

            // Force first item if reset requested (user typing)
            if (resetSelection)
                return filteredWindows[0];

            switch (behavior)
            {
                case RefreshBehavior.PreserveIdentity:
                    return ResolveByIdentity(filteredWindows, previousHwnd, previousTitle);

                case RefreshBehavior.PreserveIndex:
                    return ResolveByIndex(filteredWindows, previousIndex);

                case RefreshBehavior.PreserveScroll:
                default:
                    return ResolveByScroll(filteredWindows, previousHwnd, previousTitle, previousIndex);
            }
        }

        private WindowItem ResolveByIdentity(IList<WindowItem> windows, IntPtr? hwnd, string? title)
        {
            var match = windows.FirstOrDefault(w => w.Hwnd == hwnd && w.Title == title);
            return match ?? windows[0];
        }

        private WindowItem ResolveByIndex(IList<WindowItem> windows, int previousIndex)
        {
            int idx = Math.Clamp(previousIndex, 0, windows.Count - 1);
            return windows[idx];
        }

        private WindowItem ResolveByScroll(IList<WindowItem> windows, IntPtr? hwnd, string? title, int previousIndex)
        {
            // No previous selection -> select first
            if (hwnd == null || hwnd == IntPtr.Zero)
                return windows[0];

            // Try to find same item
            var sameItem = windows.FirstOrDefault(w => w.Hwnd == hwnd && w.Title == title);
            if (sameItem != null)
                return sameItem;

            // Fallback to index
            int idx = Math.Clamp(previousIndex, 0, windows.Count - 1);
            return windows[idx];
        }

        public int CalculateMoveIndex(int currentIndex, int direction, int itemCount)
        {
            if (itemCount == 0) return -1;

            // Nothing selected
            if (currentIndex < 0)
                return direction > 0 ? 0 : itemCount - 1;

            int newIndex = currentIndex + direction;
            return Math.Clamp(newIndex, 0, itemCount - 1);
        }

        public int CalculatePageMoveIndex(int currentIndex, int direction, int pageSize, int itemCount)
        {
            if (itemCount == 0 || pageSize <= 0) return -1;

            int idx = currentIndex < 0 ? 0 : currentIndex;
            int newIndex = idx + (direction * pageSize);
            return Math.Clamp(newIndex, 0, itemCount - 1);
        }
    }
}
