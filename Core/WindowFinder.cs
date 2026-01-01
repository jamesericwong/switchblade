using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SwitchBlade.Core
{
    public class WindowFinder : IWindowProvider
    {
        public IEnumerable<WindowItem> GetWindows()
        {
            var results = new List<WindowItem>();

            Interop.EnumWindows((hwnd, lParam) =>
            {
                if (!Interop.IsWindowVisible(hwnd))
                    return true;

                StringBuilder sb = new StringBuilder(256);
                Interop.GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrWhiteSpace(title))
                    return true;

                // Simple filter to remove common system windows usually not interesting to user
                if (title == "Program Manager") return true;

                // Get Process Name (Optional, for better context)
                // We'll skip complex process walking for now for speed, but could add it later

                results.Add(new WindowItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = "Window", // Placeholder
                    IsChromeTab = false
                });

                return true;
            }, IntPtr.Zero);

            return results;
        }
    }
}
