using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Converts boolean inputs to Visibility.
    /// Expects values[0]: bool IsShortcutVisible
    /// Expects values[1]: bool EnableNumberShortcuts
    /// Returns Visible if both are true, otherwise Collapsed.
    /// </summary>
    public class ShortcutVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return Visibility.Collapsed;

            // Check if shortcuts are globally enabled
            if (values[1] is not bool enableShortcuts || !enableShortcuts)
                return Visibility.Collapsed;

            // Check if the individual item has a visible shortcut
            if (values[0] is bool isVisible && isVisible)
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
