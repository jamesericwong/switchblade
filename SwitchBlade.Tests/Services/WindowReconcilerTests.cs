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
            _reconciler = new WindowReconciler(_mockIconService.Object, null);
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
        public void Reconcile_DoesNotPopulateIcon_Synchronously()
        {
            var item = new WindowItem { Hwnd = new IntPtr(1), Title = "App", ExecutablePath = "app.exe" };
            _mockIconService.Setup(s => s.GetIcon("app.exe")).Returns((System.Windows.Media.ImageSource)null!);

            _reconciler.Reconcile(new List<WindowItem> { item }, _mockProvider.Object);

            // Reconcile is now fast path ONLY - no icon extraction
            _mockIconService.Verify(s => s.GetIcon("app.exe"), Times.Never);
        }

        [Fact]
        public void PopulateIcons_CallsGetIcon()
        {
            var item = new WindowItem { Hwnd = new IntPtr(1), Title = "App", ExecutablePath = "app.exe" };
            _mockIconService.Setup(s => s.GetIcon("app.exe")).Returns((System.Windows.Media.ImageSource)null!);

            _reconciler.PopulateIcons(new List<WindowItem> { item });

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

        [Fact]
        public void PopulateIcons_WhenIconServiceIsNull_ReturnsImmediately()
        {
            var reconciler = new WindowReconciler(null);
            reconciler.PopulateIcons(new List<WindowItem> { new() { ExecutablePath = "a.exe" } });
            // Should not throw
        }

        [Fact]
        public void PopulateIcons_WhenGetIconThrows_LogsAndContinues()
        {
            var item = new WindowItem { ExecutablePath = "fail.exe" };
            _mockIconService.Setup(s => s.GetIcon("fail.exe")).Throws(new Exception("Fail"));
            
            _reconciler.PopulateIcons(new List<WindowItem> { item });
            
            _mockIconService.Verify(s => s.GetIcon("fail.exe"), Times.Once);
        }

        [Fact]
        public void Reconcile_ReuseItem_FallbackToNextCandidate()
        {
            var hwnd = new IntPtr(100);
            // Cache two items for same HWND
            _reconciler.Reconcile(new List<WindowItem> 
            { 
                new() { Hwnd = hwnd, Title = "T1" },
                new() { Hwnd = hwnd, Title = "T2" }
            }, _mockProvider.Object);

            // Incoming has T2 and then another T2
            var incoming = new List<WindowItem>
            {
                new() { Hwnd = hwnd, Title = "T2" }, // Should match cached T2
                new() { Hwnd = hwnd, Title = "T2" }  // T2 already claimed, should match cached T1 and update it
            };

            var result = _reconciler.Reconcile(incoming, _mockProvider.Object);
            
            Assert.Equal(2, result.Count);
            Assert.Equal("T2", result[0].Title);
            Assert.Equal("T2", result[1].Title);
        }

        [Fact]
        public void CacheCount_ReturnsSumOfBothIndexes()
        {
            _reconciler.Reconcile(new List<WindowItem> { new() { Hwnd = (IntPtr)1, Title = "A" } }, _mockProvider.Object);
            // 1 in HWND cache, 1 in Provider cache = 2
            Assert.Equal(2, _reconciler.CacheCount);
        }

        [Fact]
        public void AddToRemoveCache_Wrappers_Work()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = _mockProvider.Object };
            _reconciler.AddToCache(item);
            Assert.Equal(1, _reconciler.GetHwndCacheCount());
            
            _reconciler.RemoveFromCache(item);
            Assert.Equal(0, _reconciler.GetHwndCacheCount());
        }
    }
}
