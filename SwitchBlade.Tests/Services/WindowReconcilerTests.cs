using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class WindowReconcilerTests
    {
        private readonly Mock<IIconService> _mockIconService;
        private readonly Mock<IWindowProvider> _mockProvider;
        private readonly WindowReconciler _reconciler;

        public WindowReconcilerTests()
        {
            _mockIconService = new Mock<IIconService>();
            _mockProvider = new Mock<IWindowProvider>();
            _reconciler = new WindowReconciler(_mockIconService.Object);
        }

        [Fact]
        public void Reconcile_AddsNewItems()
        {
            var item = new WindowItem { Hwnd = new IntPtr(123), Title = "Test" };
            var list = new List<WindowItem> { item };

            var result = _reconciler.Reconcile(list, _mockProvider.Object);

            Assert.Single(result);
            Assert.Same(item, result[0]);
            Assert.Equal(1, _reconciler.GetHwndCacheCount());
        }

        [Fact]
        public void Reconcile_UpdatesExistingItem_WhenTitleChanges()
        {
            var hwnd = new IntPtr(123);
            var item1 = new WindowItem { Hwnd = hwnd, Title = "Old Title" };
            _reconciler.Reconcile(new List<WindowItem> { item1 }, _mockProvider.Object);

            var item2 = new WindowItem { Hwnd = hwnd, Title = "New Title" };
            var result = _reconciler.Reconcile(new List<WindowItem> { item2 }, _mockProvider.Object);

            Assert.Single(result);
            Assert.Same(item1, result[0]); // Should reuse the existing object
            Assert.Equal("New Title", result[0].Title);
        }

        [Fact]
        public void Reconcile_RemovesStaleItems_NotInIncoming()
        {
            var item1 = new WindowItem { Hwnd = new IntPtr(1), Title = "Keep" };
            var item2 = new WindowItem { Hwnd = new IntPtr(2), Title = "Remove" };
            _reconciler.Reconcile(new List<WindowItem> { item1, item2 }, _mockProvider.Object);

            var result = _reconciler.Reconcile(new List<WindowItem> { item1 }, _mockProvider.Object);

            Assert.Single(result);
            Assert.Equal("Keep", result[0].Title);
            Assert.Equal(1, _reconciler.GetHwndCacheCount());
        }

        [Fact]
        public void Reconcile_PopulatesIcon_WhenMissing()
        {
            var item = new WindowItem { Hwnd = new IntPtr(1), Title = "App", ExecutablePath = "app.exe" };
            _mockIconService.Setup(s => s.GetIcon("app.exe")).Returns((System.Windows.Media.ImageSource)null!); // Just verifying call

            _reconciler.Reconcile(new List<WindowItem> { item }, _mockProvider.Object);

            _mockIconService.Verify(s => s.GetIcon("app.exe"), Times.Once);
        }

        [Fact]
        public void CacheIndexes_ShouldBeSymmetrical()
        {
            var items = new List<WindowItem>
            {
                new WindowItem { Hwnd = new IntPtr(100), Title = "A" },
                new WindowItem { Hwnd = new IntPtr(101), Title = "B" }
            };

            _reconciler.Reconcile(items, _mockProvider.Object);

            Assert.Equal(2, _reconciler.GetHwndCacheCount());
            Assert.Equal(2, _reconciler.GetProviderCacheCount());
        }

        [Fact]
        public void CacheIndexes_RemainSymmetricalAfterItemRemoval()
        {
            var items = new List<WindowItem>
            {
                new WindowItem { Hwnd = new IntPtr(100), Title = "A" },
                new WindowItem { Hwnd = new IntPtr(101), Title = "B" }
            };
            _reconciler.Reconcile(items, _mockProvider.Object);

            // Remove one
            _reconciler.Reconcile(new List<WindowItem> { items[0] }, _mockProvider.Object);

            Assert.Equal(1, _reconciler.GetHwndCacheCount());
            Assert.Equal(1, _reconciler.GetProviderCacheCount());
        }
    }
}
