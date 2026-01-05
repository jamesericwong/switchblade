using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Converts boolean inputs to Visibility - WinUI version.
    /// Note: WinUI doesn't have IMultiValueConverter, so this is simplified.
    /// Use x:Bind with a function instead in XAML for multi-value scenarios.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// For backwards compatibility - WinUI doesn't support IMultiValueConverter.
    /// This converter just checks a single bool value.
    /// The MainWindow uses x:Bind directly with the property now.
    /// </summary>
    public class ShortcutVisibilityConverter : BoolToVisibilityConverter
    {
        // Just inherit from BoolToVisibilityConverter - 
        // actual multi-value logic moved to ViewModel
    }
}
