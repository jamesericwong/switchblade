using System;

namespace SwitchBlade.Contracts
{
    public class WindowItem
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        
        // Kept for backward compat / specific logic if needed, but 'Source' is preferred for activation
        public bool IsChromeTab { get; set; } 

        public IWindowProvider? Source { get; set; }

        public override string ToString()
        {
            return $"{Title} ({ProcessName})";
        }
    }
}
