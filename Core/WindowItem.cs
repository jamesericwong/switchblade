using System;

namespace SwitchBlade.Core
{
    public class WindowItem
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public bool IsChromeTab { get; set; }

        public override string ToString()
        {
            return $"{Title} ({ProcessName})";
        }
    }
}
