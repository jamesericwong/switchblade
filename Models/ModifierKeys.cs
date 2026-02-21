namespace SwitchBlade.Services
{
    /// <summary>
    /// Constants for modifier key flags used in hotkeys and shortcuts.
    /// These values match the Windows API MOD_* constants.
    /// </summary>
    public static class ModifierKeyFlags
    {
        /// <summary>No modifier key required.</summary>
        public const uint None = 0;

        /// <summary>Alt key modifier.</summary>
        public const uint Alt = 1;

        /// <summary>Ctrl key modifier.</summary>
        public const uint Ctrl = 2;

        /// <summary>Shift key modifier.</summary>
        public const uint Shift = 4;

        /// <summary>Windows key modifier.</summary>
        public const uint Win = 8;

        /// <summary>
        /// Converts a modifier value to its string representation.
        /// </summary>
        public static string ToString(uint modifier)
        {
            if (modifier == None) return "None";
            
            var parts = new System.Collections.Generic.List<string>();
            if ((modifier & Alt) != 0) parts.Add("Alt");
            if ((modifier & Ctrl) != 0) parts.Add("Ctrl");
            if ((modifier & Shift) != 0) parts.Add("Shift");
            if ((modifier & Win) != 0) parts.Add("Win");
            
            return parts.Count > 0 ? string.Join("+", parts) : "None";
        }

        /// <summary>
        /// Converts a string representation to its modifier value.
        /// </summary>
        public static uint FromString(string value)
        {
            return value switch
            {
                "Alt" => Alt,
                "Ctrl" => Ctrl,
                "Shift" => Shift,
                "Win" => Win,
                _ => None
            };
        }
    }
}

