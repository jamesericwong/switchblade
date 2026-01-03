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
        public void ShortcutVisibilityConverter_BothTrue_ReturnsVisible()
        {
            var converter = new ShortcutVisibilityConverter();
            object[] values = new object[] { true, true }; // IsShortcutVisible=true, Enable=true
            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void ShortcutVisibilityConverter_IsVisibleFalse_ReturnsCollapsed()
        {
            var converter = new ShortcutVisibilityConverter();
            object[] values = new object[] { false, true }; // IsShortcutVisible=false, Enable=true
            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void ShortcutVisibilityConverter_Disabled_ReturnsCollapsed()
        {
            var converter = new ShortcutVisibilityConverter();
            object[] values = new object[] { true, false }; // IsShortcutVisible=true, Enable=false
            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void ShortcutVisibilityConverter_InvalidInput_ReturnsCollapsed()
        {
            var converter = new ShortcutVisibilityConverter();
            object[] values = new object[] { new object(), null! };
            var result = converter.Convert(values, typeof(Visibility), new object(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }
    }
}
