using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class SimpleServiceTests
    {
        [Fact]
        public void ModifierKeyFlags_ToString_ReturnsCorrectStrings()
        {
            Assert.Equal("Alt", ModifierKeyFlags.ToString(ModifierKeyFlags.Alt));
            Assert.Equal("Ctrl", ModifierKeyFlags.ToString(ModifierKeyFlags.Ctrl));
            Assert.Equal("Shift", ModifierKeyFlags.ToString(ModifierKeyFlags.Shift));
            Assert.Equal("Win", ModifierKeyFlags.ToString(ModifierKeyFlags.Win));
            Assert.Equal("None", ModifierKeyFlags.ToString(ModifierKeyFlags.None));
            Assert.Equal("Alt+Ctrl+Shift+Win", ModifierKeyFlags.ToString(ModifierKeyFlags.Alt | ModifierKeyFlags.Ctrl | ModifierKeyFlags.Shift | ModifierKeyFlags.Win));
            
            // Branch coverage for undefined flags
            Assert.Equal("None", ModifierKeyFlags.ToString(16)); // 16 is not a defined modifier
        }

        [Fact]
        public void ModifierKeyFlags_FromString_ReturnsCorrectValues()
        {
            Assert.Equal(ModifierKeyFlags.Alt, ModifierKeyFlags.FromString("Alt"));
            Assert.Equal(ModifierKeyFlags.Ctrl, ModifierKeyFlags.FromString("Ctrl"));
            Assert.Equal(ModifierKeyFlags.Shift, ModifierKeyFlags.FromString("Shift"));
            Assert.Equal(ModifierKeyFlags.Win, ModifierKeyFlags.FromString("Win"));
            Assert.Equal(ModifierKeyFlags.None, ModifierKeyFlags.FromString("None"));
            Assert.Equal(ModifierKeyFlags.None, ModifierKeyFlags.FromString("Unknown"));
        }

        [Fact]
        public void WindowListUpdatedEventArgs_Properties_ReturnValues()
        {
            var mockProvider = new Mock<IWindowProvider>();
            var args = new WindowListUpdatedEventArgs(mockProvider.Object, true);
            
            Assert.Same(mockProvider.Object, args.Provider);
            Assert.True(args.IsStructuralChange);
        }
    }
}
