using System.ComponentModel;
using SwitchBlade.Contracts;
using Xunit;

namespace SwitchBlade.Tests.Contracts
{
    public class WindowItemTests
    {
        [Fact]
        public void Title_Normalization_IsCached()
        {
            var item = new WindowItem { Title = "Test Title" };
            var normalized1 = item.NormalizedTitle;
            var normalized2 = item.NormalizedTitle;

            Assert.Equal("testtitle", normalized1);
            Assert.Same(normalized1, normalized2); // Should be same string reference due to caching logic
        }

        [Fact]
        public void Title_Change_InvalidatesNormalizationCache()
        {
            var item = new WindowItem { Title = "Test Title" };
            var normalized1 = item.NormalizedTitle;
            
            item.Title = "New Title";
            var normalized2 = item.NormalizedTitle;

            Assert.NotEqual(normalized1, normalized2);
            Assert.Equal("newtitle", normalized2);
        }

        [Fact]
        public void NormalizeForSearch_HandlesEdgeCases()
        {
            var item = new WindowItem { Title = "  Foo - Bar_Baz  " };
            Assert.Equal("foobarbaz", item.NormalizedTitle);

            item.Title = "";
            Assert.Equal("", item.NormalizedTitle);

            item.Title = null!; // Should handle null if property allows it, though Title is non-nullable string. 
                               // Setter check might throw or implementation might handle. 
                               // Implementation uses _title != value. If we set null, _title becomes null. 
                               // NormalizeForSearch checks checking string.IsNullOrEmpty.
            
            // However, the property is defined as string (non-nullable). 
            // Let's stick to valid inputs for now or empty string.
            item.Title = "-_";
            Assert.Equal("", item.NormalizedTitle);
        }

        [Theory]
        [InlineData(0, "1")]
        [InlineData(8, "9")]
        [InlineData(9, "0")]
        [InlineData(-1, "")]
        [InlineData(10, "")]
        public void ShortcutDisplay_ReturnsCorrectString(int index, string expected)
        {
            var item = new WindowItem { ShortcutIndex = index };
            Assert.Equal(expected, item.ShortcutDisplay);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(9, true)]
        [InlineData(-1, false)]
        [InlineData(10, false)]
        public void IsShortcutVisible_CalculatedCorrectly(int index, bool expected)
        {
            var item = new WindowItem { ShortcutIndex = index };
            Assert.Equal(expected, item.IsShortcutVisible);
        }

        [Fact]
        public void PropertyChanged_Fires_OnChanges()
        {
            var item = new WindowItem();
            
            Assert.PropertyChanged(item, nameof(WindowItem.Title), () => item.Title = "New");
            Assert.PropertyChanged(item, nameof(WindowItem.ShortcutIndex), () => item.ShortcutIndex = 1);
            Assert.PropertyChanged(item, nameof(WindowItem.BadgeOpacity), () => item.BadgeOpacity = 0.5);
            Assert.PropertyChanged(item, nameof(WindowItem.BadgeTranslateX), () => item.BadgeTranslateX = -10);
            Assert.PropertyChanged(item, nameof(WindowItem.Icon), () => item.Icon = new object());
        }

        [Fact]
        public void ShortcutIndex_Change_FiresDependentProperties()
        {
            var item = new WindowItem();
            
            // Changing ShortcutIndex should fire ShortcutDisplay and IsShortcutVisible
            var firedDisplay = false;
            var firedVisible = false;
            item.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(WindowItem.ShortcutDisplay)) firedDisplay = true;
                if (e.PropertyName == nameof(WindowItem.IsShortcutVisible)) firedVisible = true;
            };

            item.ShortcutIndex = 1;

            Assert.True(firedDisplay);
            Assert.True(firedVisible);
        }

        [Fact]
        public void ResetBadgeAnimation_ResetsValues()
        {
            var item = new WindowItem 
            { 
                BadgeOpacity = 1.0, 
                BadgeTranslateX = 0 
            };

            item.ResetBadgeAnimation();

            Assert.Equal(0, item.BadgeOpacity);
            Assert.Equal(-20, item.BadgeTranslateX);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var item = new WindowItem { Title = "MyWindow", ProcessName = "notepad" };
            Assert.Equal("MyWindow (notepad)", item.ToString());
        }
    }
}
