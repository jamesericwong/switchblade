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

        // Animation properties for staggered badge fade-in and slide-in
        // Default to VISIBLE so that if an item is replaced (e.g. title update) but shouldn't animate, it stays visible.
        // We strictly RESET to hidden (0, -20) only when we determine an animation should run.
        private double _badgeOpacity = 1;
        private double _badgeTranslateX = 0;

        /// <summary>
        /// Tracks if this specific item execution has already animated its badge.
        /// Unique per WindowItem instance -> allows multiple tabs (same HWND) to animate separately.
        /// </summary>
        public bool HasBeenAnimated { get; set; } = false;

        /// <summary>
        /// Opacity of the badge for fade-in animation (0 to 1).
        /// </summary>
        public double BadgeOpacity
        {
            get => _badgeOpacity;
            set
            {
                if (_badgeOpacity != value)
                {
                    _badgeOpacity = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// X translation of the badge for slide-in animation (-10 to 0).
        /// </summary>
        public double BadgeTranslateX
        {
            get => _badgeTranslateX;
            set
            {
                if (_badgeTranslateX != value)
                {
                    _badgeTranslateX = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Resets animation state to initial values (hidden, offset to left).
        /// </summary>
        public void ResetBadgeAnimation()
        {
            _badgeOpacity = 0;
            _badgeTranslateX = -20;
            OnPropertyChanged(nameof(BadgeOpacity));
            OnPropertyChanged(nameof(BadgeTranslateX));
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
