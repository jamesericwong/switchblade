using System;
using System.Globalization;
using System.Windows;
using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class ShortcutVisibilityConverterTests
    {
        private readonly ShortcutVisibilityConverter _converter = new ShortcutVisibilityConverter();

        [Fact]
        public void Convert_BothTrue_ReturnsVisible()
        {
            object[] values = { true, true };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void Convert_IsVisibleFalse_ReturnsCollapsed()
        {
            object[] values = { false, true };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_EnabledFalse_ReturnsCollapsed()
        {
            object[] values = { true, false };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_InvalidEnabledValue_ReturnsCollapsed()
        {
            object[] values = { true, "not a bool" };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_TooFewValues_ReturnsCollapsed()
        {
            object[] values = { true };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_InvalidIsVisibleValue_ReturnsCollapsed()
        {
            object[] values = { "not a bool", true };
            var result = _converter.Convert(values, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => 
                _converter.ConvertBack(Visibility.Visible, null!, null!, CultureInfo.InvariantCulture));
        }
    }
}
