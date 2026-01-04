using Xunit;
using SwitchBlade.Contracts;

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
    }
}
