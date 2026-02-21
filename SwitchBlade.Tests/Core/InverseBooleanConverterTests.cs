using System;
using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class InverseBooleanConverterTests
    {
        private readonly InverseBooleanConverter _converter = new InverseBooleanConverter();

        [Fact]
        public void Convert_BoolTrue_ReturnsFalse()
        {
            var result = _converter.Convert(true, typeof(bool), null!, null!);
            Assert.Equal(false, result);
        }

        [Fact]
        public void Convert_BoolFalse_ReturnsTrue()
        {
            var result = _converter.Convert(false, typeof(bool), null!, null!);
            Assert.Equal(true, result);
        }

        [Fact]
        public void Convert_NonBool_ReturnsOriginalValue()
        {
            var result = _converter.Convert("not a bool", typeof(bool), null!, null!);
            Assert.Equal("not a bool", result);
        }

        [Fact]
        public void ConvertBack_BoolTrue_ReturnsFalse()
        {
            var result = _converter.ConvertBack(true, typeof(bool), null!, null!);
            Assert.Equal(false, result);
        }

        [Fact]
        public void ConvertBack_BoolFalse_ReturnsTrue()
        {
            var result = _converter.ConvertBack(false, typeof(bool), null!, null!);
            Assert.Equal(true, result);
        }

        [Fact]
        public void ConvertBack_NonBool_ReturnsOriginalValue()
        {
            var result = _converter.ConvertBack(123, typeof(bool), null!, null!);
            Assert.Equal(123, result);
        }
    }
}
