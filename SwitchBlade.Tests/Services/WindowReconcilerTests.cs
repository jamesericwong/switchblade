using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class WindowReconcilerTests
    {
        private readonly Mock<IIconService> _mockIconService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IWindowProvider> _mockProvider;
        private readonly WindowReconciler _reconciler;

        public WindowReconcilerTests()
        {
            _mockIconService = new Mock<IIconService>();
            _mockLogger = new Mock<ILogger>();
            _mockProvider = new Mock<IWindowProvider>();
            _reconciler = new WindowReconciler(_mockIconService.Object, _mockLogger.Object);
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
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to populate icon")), It.IsAny<Exception>()), Times.Once);
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

        [Fact]
        public void AddToCache_DoesNotAddDuplicates()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = _mockProvider.Object };
            _reconciler.AddToCache(item);
            _reconciler.AddToCache(item); // Add same item again
            
            Assert.Equal(1, _reconciler.GetHwndCacheCount());
            // Verify internal list usage
            // We can't access private fields easily, but cache count remaining 1 implies no duplicate added
        }

        [Fact]
        public void RemoveFromCache_RemovesProviderEntry_WhenLastItemRemoved()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = _mockProvider.Object };
            _reconciler.AddToCache(item);
            Assert.Equal(1, _reconciler.GetProviderCacheCount());

            _reconciler.RemoveFromCache(item);
            Assert.Equal(0, _reconciler.GetProviderCacheCount());
        }

        [Fact]
        public void Reconcile_ReuseItem_SourceAlreadySet_BranchCoverage()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = _mockProvider.Object };
            _reconciler.AddToCache(item);
            
            // Reconcile with same HWND but different provider
            var mockOtherProvider = new Mock<IWindowProvider>();
            var incoming = new List<WindowItem> { new() { Hwnd = (IntPtr)1, Title = "A" } };
            
            var result = _reconciler.Reconcile(incoming, mockOtherProvider.Object);
            
            Assert.Same(item, result[0]);
            Assert.Same(_mockProvider.Object, item.Source); // Should NOT change if already set
        }

        [Fact]
        public void PopulateIcons_Coverage_Gaps()
        {
            var dummyIcon = new BitmapImage();
            var itemWithIcon = new WindowItem { Icon = dummyIcon, ExecutablePath = "a.exe" };
            var itemNoPath = new WindowItem { ExecutablePath = "" };
            var itemToPopulate = new WindowItem { ExecutablePath = "b.exe" };
            
            _mockIconService.Setup(s => s.GetIcon("b.exe")).Returns(new BitmapImage());
            
            // Branch 1: IsDebugEnabled = true to hit perf logging
            Logger.IsDebugEnabled = true;
            try
            {
                _reconciler.PopulateIcons(new List<WindowItem> { itemWithIcon, itemNoPath, itemToPopulate });
            }
            finally
            {
                Logger.IsDebugEnabled = false;
            }

            _mockIconService.Verify(s => s.GetIcon("a.exe"), Times.Never);
            _mockIconService.Verify(s => s.GetIcon("b.exe"), Times.Once);

            // Branch 2: count == 0 skip branch
            _mockIconService.Invocations.Clear();
            _mockLogger.Invocations.Clear();
            _reconciler.PopulateIcons(new List<WindowItem> { itemWithIcon });
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("[Perf]"))), Times.Never);
        }

        [Fact]
        public void RemoveFromCache_SourceIsNull_BranchCoverage()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = null };
            _reconciler.AddToCache(item);
            
            _reconciler.RemoveFromCache(item);
            // Should not throw or fail HWND removal
            Assert.Equal(0, _reconciler.GetHwndCacheCount());
        }

        [Fact]
        public void Reconcile_ExistingItemHasSource_BranchCoverage()
        {
            var initialSource = new Mock<IWindowProvider>().Object;
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "Original", Source = initialSource };
            _reconciler.AddToCache(item);
            
            var incoming = new WindowItem { Hwnd = (IntPtr)1, Title = "Updated" };
            var newProvider = new Mock<IWindowProvider>().Object;
            
            var results = _reconciler.Reconcile(new List<WindowItem> { incoming }, newProvider);
            
            Assert.Single(results);
            Assert.Same(item, results[0]);
            Assert.Same(initialSource, results[0].Source); // Should NOT be overwritten
        }

        [Fact]
        public void Reconcile_ExistingItemHasNullSource_BranchCoverage()
        {
            var item = new WindowItem { Hwnd = (IntPtr)1234, Title = "NoSource", Source = null };
            _reconciler.AddToCache(item);
            
            var incoming = new WindowItem { Hwnd = (IntPtr)1234, Title = "NoSource" };
            var provider = new Mock<IWindowProvider>().Object;
            
            var results = _reconciler.Reconcile(new List<WindowItem> { incoming }, provider);
            
            Assert.Single(results);
            Assert.Same(item, results[0]);
            Assert.Same(provider, results[0].Source); // Should be set now
        }

        [Fact]
        public void PopulateIcons_NullLogger_ExceptionBranchCoverage()
        {
            var reconcilerNoLogger = new WindowReconciler(_mockIconService.Object, null!);
            var item = new WindowItem { ExecutablePath = "fail.exe" };
            _mockIconService.Setup(s => s.GetIcon("fail.exe")).Throws(new Exception("Fail"));
            
            // Should not throw or crash even if _logger is null
            reconcilerNoLogger.PopulateIcons(new List<WindowItem> { item });
            
            _mockIconService.Verify(s => s.GetIcon("fail.exe"), Times.Once);
        }

        [Fact]
        public void RemoveFromCache_ItemNotInProviderSet_BranchCoverage()
        {
            var provider1 = new Mock<IWindowProvider>().Object;
            var provider2 = new Mock<IWindowProvider>().Object;
            var item = new WindowItem { Hwnd = (IntPtr)1, Title = "A", Source = provider1 };
            
            _reconciler.AddToCache(item);
            
            // Manipulate item to have different source before removal to hit edge case 
            // where it's in HWND cache but not in the NEW provider's set
            item.Source = provider2;
            
            _reconciler.RemoveFromCache(item);
            
            Assert.Equal(0, _reconciler.GetHwndCacheCount());
            // Provider1 set should still have the item (internal inconsistency simulation for coverage)
            // But the method should handle it gracefully
        }
    }
}
