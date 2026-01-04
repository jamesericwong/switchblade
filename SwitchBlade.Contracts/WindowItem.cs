using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SwitchBlade.Contracts
{
    public class WindowItem : INotifyPropertyChanged
    {
        private int _shortcutIndex = -1;

        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;

        // Kept for backward compat / specific logic if needed, but 'Source' is preferred for activation
        public bool IsChromeTab { get; set; }

        // Indicates this is a tab within Windows Terminal
        public bool IsTerminalTab { get; set; }

        public IWindowProvider? Source { get; set; }

        public override string ToString()
        {
            return $"{Title} ({ProcessName})";
        }

        /// <summary>
        /// The 0-based index for the shortcut (0-9). -1 if no shortcut.
        /// </summary>
        public int ShortcutIndex
        {
            get => _shortcutIndex;
            set
            {
                if (_shortcutIndex != value)
                {
                    _shortcutIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShortcutDisplay));
                    OnPropertyChanged(nameof(IsShortcutVisible));
                }
            }
        }

        /// <summary>
        /// Returns the display string for the shortcut (1-9, 0).
        /// </summary>
        public string ShortcutDisplay
        {
            get
            {
                if (_shortcutIndex >= 0 && _shortcutIndex < 9)
                    return (_shortcutIndex + 1).ToString();
                if (_shortcutIndex == 9)
                    return "0";
                return string.Empty;
            }
        }

        public bool IsShortcutVisible => _shortcutIndex >= 0 && _shortcutIndex <= 9;


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
