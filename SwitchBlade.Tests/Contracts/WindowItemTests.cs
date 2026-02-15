using Xunit;
using SwitchBlade.Contracts;
using System.Collections.Generic;

namespace SwitchBlade.Tests.Contracts
{
    public class WindowItemTests
    {
        [Fact]
        public void WindowItem_DefaultTitle_IsEmptyString()
        {
            var item = new WindowItem();

            Assert.Equal(string.Empty, item.Title);
        }

        [Fact]
        public void WindowItem_DefaultProcessName_IsEmptyString()
        {
            var item = new WindowItem();

            Assert.Equal(string.Empty, item.ProcessName);
        }

        [Fact]
        public void WindowItem_DefaultHwnd_IsZero()
        {
            var item = new WindowItem();

            Assert.Equal(IntPtr.Zero, item.Hwnd);
        }

        [Fact]
        public void WindowItem_DefaultSource_IsNull()
        {
            var item = new WindowItem();

            Assert.Null(item.Source);
        }

        [Fact]
        public void WindowItem_SetTitle_ReturnsCorrectValue()
        {
            var item = new WindowItem { Title = "Test Window" };

            Assert.Equal("Test Window", item.Title);
        }

        [Fact]
        public void WindowItem_SetProcessName_ReturnsCorrectValue()
        {
            var item = new WindowItem { ProcessName = "notepad" };

            Assert.Equal("notepad", item.ProcessName);
        }

        [Fact]
        public void WindowItem_SetHwnd_ReturnsCorrectValue()
        {
            var item = new WindowItem { Hwnd = (IntPtr)12345 };

            Assert.Equal((IntPtr)12345, item.Hwnd);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var item = new WindowItem
            {
                Title = "Document.txt - Notepad",
                ProcessName = "notepad"
            };

            var result = item.ToString();

            Assert.Equal("Document.txt - Notepad (notepad)", result);
        }

        [Fact]
        public void ToString_WithEmptyTitle_ReturnsEmptyTitleFormat()
        {
            var item = new WindowItem
            {
                Title = "",
                ProcessName = "explorer"
            };

            var result = item.ToString();

            Assert.Equal(" (explorer)", result);
        }

        [Fact]
        public void ToString_WithEmptyProcessName_ReturnsEmptyProcessFormat()
        {
            var item = new WindowItem
            {
                Title = "Test",
                ProcessName = ""
            };

            var result = item.ToString();

            Assert.Equal("Test ()", result);
        }

        [Fact]
        public void WindowItem_DefaultBadgeOpacity_IsZero()
        {
            var item = new WindowItem();

            Assert.Equal(0.0, item.BadgeOpacity);
        }

        [Fact]
        public void WindowItem_DefaultBadgeTranslateX_IsZero()
        {
            var item = new WindowItem();

            Assert.Equal(0.0, item.BadgeTranslateX);
        }

        [Fact]
        public void WindowItem_SetBadgeOpacity_ReturnsCorrectValue()
        {
            var item = new WindowItem { BadgeOpacity = 0.5 };

            Assert.Equal(0.5, item.BadgeOpacity);
        }

        [Fact]
        public void WindowItem_SetBadgeTranslateX_ReturnsCorrectValue()
        {
            var item = new WindowItem { BadgeTranslateX = -10.0 };

            Assert.Equal(-10.0, item.BadgeTranslateX);
        }

        [Fact]
        public void ResetBadgeAnimation_ResetsToHiddenState()
        {
            var item = new WindowItem { BadgeOpacity = 1.0, BadgeTranslateX = 0 };

            item.ResetBadgeAnimation();

            Assert.Equal(0.0, item.BadgeOpacity);
            Assert.Equal(-20.0, item.BadgeTranslateX);
        }

        [Fact]
        public void Title_PropertyChanged_FiresCorrectEvent()
        {
            // Arrange - Tests that PropertyChangedEventArgs caching works correctly
            var item = new WindowItem();
            var firedPropertyNames = new List<string>();
            item.PropertyChanged += (s, e) => firedPropertyNames.Add(e.PropertyName!);

            // Act
            item.Title = "New Title";

            // Assert
            Assert.Single(firedPropertyNames);
            Assert.Equal("Title", firedPropertyNames[0]);
        }

        [Fact]
        public void ShortcutIndex_PropertyChanged_FiresMultipleEvents()
        {
            // Arrange - ShortcutIndex should fire events for ShortcutIndex, ShortcutDisplay, and IsShortcutVisible
            var item = new WindowItem();
            var firedPropertyNames = new List<string>();
            item.PropertyChanged += (s, e) => firedPropertyNames.Add(e.PropertyName!);

            // Act
            item.ShortcutIndex = 5;

            // Assert
            Assert.Equal(3, firedPropertyNames.Count);
            Assert.Contains("ShortcutIndex", firedPropertyNames);
            Assert.Contains("ShortcutDisplay", firedPropertyNames);
            Assert.Contains("IsShortcutVisible", firedPropertyNames);
        }

        [Fact]
        public void BadgeOpacity_PropertyChanged_FiresCorrectEvent()
        {
            // Arrange
            var item = new WindowItem();
            var firedPropertyNames = new List<string>();
            item.PropertyChanged += (s, e) => firedPropertyNames.Add(e.PropertyName!);

            // Act
            item.BadgeOpacity = 0.5;

            // Assert
            Assert.Single(firedPropertyNames);
            Assert.Equal("BadgeOpacity", firedPropertyNames[0]);
        }

        [Fact]
        public void BadgeTranslateX_PropertyChanged_FiresCorrectEvent()
        {
            // Arrange
            var item = new WindowItem();
            var firedPropertyNames = new List<string>();
            item.PropertyChanged += (s, e) => firedPropertyNames.Add(e.PropertyName!);

            // Act
            item.BadgeTranslateX = -15.0;

            // Assert
            Assert.Single(firedPropertyNames);
            Assert.Equal("BadgeTranslateX", firedPropertyNames[0]);
        }

        [Fact]
        public void ResetBadgeAnimation_PropertyChanged_FiresBothEvents()
        {
            // Arrange
            var item = new WindowItem { BadgeOpacity = 1.0, BadgeTranslateX = 0 };
            var firedPropertyNames = new List<string>();
            item.PropertyChanged += (s, e) => firedPropertyNames.Add(e.PropertyName!);

            // Act
            item.ResetBadgeAnimation();

            // Assert
            Assert.Equal(2, firedPropertyNames.Count);
            Assert.Contains("BadgeOpacity", firedPropertyNames);
            Assert.Contains("BadgeTranslateX", firedPropertyNames);
        }

        [Fact]
        public void Title_NoChange_DoesNotFireEvent()
        {
            // Arrange - Same value should not fire PropertyChanged
            var item = new WindowItem { Title = "Same" };
            var firedCount = 0;
            item.PropertyChanged += (s, e) => firedCount++;

            // Act
            item.Title = "Same"; // No change

            // Assert
            Assert.Equal(0, firedCount);
        }
        [Fact]
        public void Setting_Icon_Raises_PropertyChanged()
        {
            var item = new WindowItem();
            var icon = new object();
            bool eventRaised = false;

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WindowItem.Icon))
                    eventRaised = true;
            };

            item.Icon = icon;

            Assert.True(eventRaised);
            Assert.Same(icon, item.Icon);
        }
    }
}

