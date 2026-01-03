using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Converts a ListBoxItem to its actual index in the parent ItemsControl.
    /// Returns Visible if index is less than 10 and EnableNumberShortcuts is true.
    /// </summary>
    public class IndexLessThan10ToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: the ListBoxItem
            // values[1]: EnableNumberShortcuts boolean
            if (values.Length < 2)
                return Visibility.Collapsed;

            if (values[1] is not bool enableShortcuts || !enableShortcuts)
                return Visibility.Collapsed;

            if (values[0] is System.Windows.Controls.ListBoxItem item)
            {
                var itemsControl = System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(item);
                if (itemsControl != null)
                {
                    int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                    return index >= 0 && index < 10 ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a ListBoxItem to its number badge text (1-9, 0 for 10th).
    /// Returns empty string if index >= 10.
    /// </summary>
    public class IndexToNumberBadgeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Controls.ListBoxItem item)
            {
                var itemsControl = System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(item);
                if (itemsControl != null)
                {
                    int index = itemsControl.ItemContainerGenerator.IndexFromContainer(item);
                    if (index >= 0 && index < 9)
                        return (index + 1).ToString();
                    if (index == 9)
                        return "0";
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
