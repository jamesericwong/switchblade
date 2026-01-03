using Xunit;
using System.Windows;
using SwitchBlade.Core;

namespace SwitchBlade.Tests.Core
{
    public class ConverterTests
    {
        [Fact]
        public void InverseBooleanConverter_True_ReturnsFalse()
        {
            var converter = new InverseBooleanConverter();
            var result = converter.Convert(true, typeof(bool), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void InverseBooleanConverter_False_ReturnsTrue()
        {
            var converter = new InverseBooleanConverter();
            var result = converter.Convert(false, typeof(bool), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void InverseBooleanConverter_NonBool_ReturnsOriginal()
        {
            var converter = new InverseBooleanConverter();
            var obj = new object();
            var result = converter.Convert(obj, typeof(bool), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Same(obj, result);
        }

        [Fact]
        public void IndexToNumberBadgeConverter_NonListBoxItem_ReturnsEmpty()
        {
            // Testing with object instead of actual ListBoxItem to avoid STA thread requirement.
            // The converter returns empty string for non-ListBoxItem or null values.
            var converter = new IndexToNumberBadgeConverter();

            var result = converter.Convert(new object(), typeof(string), new object(), System.Globalization.CultureInfo.InvariantCulture);

            // Should be empty because value is not ListBoxItem
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void IndexLessThan10ToVisibilityConverter_NullItem_ReturnsCollapsed()
        {
            // Testing with object instead of actual ListBoxItem to avoid STA thread requirement.
            var converter = new IndexLessThan10ToVisibilityConverter();

            // values: [item, enableShortcuts]
            object[] values = new object[] { new object(), true };

            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void IndexLessThan10ToVisibilityConverter_ShortcutsDisabled_ReturnsCollapsed()
        {
            // When shortcuts are disabled, should return Collapsed regardless of item.
            var converter = new IndexLessThan10ToVisibilityConverter();

            object[] values = new object[] { new object(), false }; // Shortcuts disabled

            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Collapsed, result);
        }
    }
}
