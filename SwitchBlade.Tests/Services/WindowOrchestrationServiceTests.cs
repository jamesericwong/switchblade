using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using SwitchBlade.Contracts;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class WindowOrchestrationServiceTests
    {
        private Mock<IWindowProvider> CreateMockProvider(string name, List<WindowItem> items)
        {
            var mock = new Mock<IWindowProvider>();
            mock.Setup(p => p.PluginName).Returns(name);
            mock.Setup(p => p.GetWindows()).Returns(items);
            mock.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());
            return mock;
        }

        private ISettingsService CreateMockSettingsService()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new UserSettings());
            return mockSettings.Object;
        }

        [Fact]
        public async Task RefreshAsync_CallsGetWindowsOnAllProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            provider1.Verify(p => p.GetWindows(), Times.Once);
            provider2.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SkipsDisabledProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string> { "Provider1" });

            provider1.Verify(p => p.GetWindows(), Times.Never);
            provider2.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_CollectsWindowsFromAllProviders()
        {
            var items1 = new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "proc1" }
            };
            var items2 = new List<WindowItem>
            {
                new() { Title = "Window2", Hwnd = (IntPtr)2, ProcessName = "proc2" }
            };

            var provider1 = CreateMockProvider("Provider1", items1);
            var provider2 = CreateMockProvider("Provider2", items2);
            provider1.Setup(p => p.PluginName).Returns("Provider1");
            provider2.Setup(p => p.PluginName).Returns("Provider2");
            items1[0].Source = provider1.Object;
            items2[0].Source = provider2.Object;

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
        }

        [Fact]
        public async Task RefreshAsync_FiresWindowListUpdatedEvent()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            int eventCount = 0;
            service.WindowListUpdated += (s, e) => eventCount++;

            await service.RefreshAsync(new HashSet<string>());

            Assert.True(eventCount > 0);
        }

        [Fact]
        public async Task RefreshAsync_ReloadsSettingsForEachProvider()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            provider.Verify(p => p.ReloadSettings(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SetsExclusionsFromHandledProcesses()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            provider1.Setup(p => p.GetHandledProcesses()).Returns(new[] { "chrome", "edge" });

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            provider2.Verify(p => p.SetExclusions(It.Is<IEnumerable<string>>(
                ex => ex.Contains("chrome") && ex.Contains("edge"))), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_PreservesMultipleItemsWithSameHwnd()
        {
            // Simulate two Chrome tabs sharing the same HWND
            var hwnd = (IntPtr)555;
            var provider = CreateMockProvider("Chrome", new List<WindowItem>
            {
                new() { Title = "Tab 1", Hwnd = hwnd, ProcessName = "chrome" },
                new() { Title = "Tab 2", Hwnd = hwnd, ProcessName = "chrome" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            // First refresh
            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Tab 1");
            Assert.Contains(service.AllWindows, x => x.Title == "Tab 2");

            // Second refresh (update titles)
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Tab 1 Updated", Hwnd = hwnd, ProcessName = "chrome" },
                new() { Title = "Tab 2 Updated", Hwnd = hwnd, ProcessName = "chrome" }
            });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Tab 1 Updated");
            Assert.Contains(service.AllWindows, x => x.Title == "Tab 2 Updated");
        }

        [Fact]
        public void AllWindows_ReturnsImmutableCopy()
        {
            var service = new WindowOrchestrationService(new List<IWindowProvider>(), null, CreateMockSettingsService());

            var windows1 = service.AllWindows;
            var windows2 = service.AllWindows;

            Assert.NotSame(windows1, windows2); // Different instances
        }

        [Fact]
        public async Task CacheIndexes_ShouldBeSymmetrical()
        {
            // Arrange: Create a provider with multiple items (including shared HWND)
            var sharedHwnd = (IntPtr)100;
            var items = new List<WindowItem>
            {
                new() { Title = "App1", Hwnd = (IntPtr)1, ProcessName = "app1" },
                new() { Title = "App2", Hwnd = (IntPtr)2, ProcessName = "app2" },
                new() { Title = "Tab1", Hwnd = sharedHwnd, ProcessName = "chrome" },
                new() { Title = "Tab2", Hwnd = sharedHwnd, ProcessName = "chrome" },
            };
            var provider = CreateMockProvider("TestProvider", items);

            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            // Act: Refresh to populate caches
            await service.RefreshAsync(new HashSet<string>());

            // Assert: Cache counts should be equal
            int hwndCacheCount = service.GetInternalHwndCacheCount();
            int providerCacheCount = service.GetInternalProviderCacheCount();

            Assert.Equal(4, service.AllWindows.Count);
            Assert.Equal(hwndCacheCount, providerCacheCount);
        }

        [Fact]
        public async Task CacheIndexes_RemainSymmetricalAfterItemRemoval()
        {
            // Arrange: Create provider with items
            var provider = CreateMockProvider("TestProvider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app1" },
                new() { Title = "Window2", Hwnd = (IntPtr)2, ProcessName = "app2" },
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            // First refresh: populate caches
            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            // Second refresh: remove one window
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app1" }
            });

            await service.RefreshAsync(new HashSet<string>());

            // Assert: Caches should still be symmetrical
            int hwndCacheCount = service.GetInternalHwndCacheCount();
            int providerCacheCount = service.GetInternalProviderCacheCount();

            Assert.Single(service.AllWindows);
            Assert.Equal(hwndCacheCount, providerCacheCount);
        }

        [Fact]
        public async Task RefreshAsync_CompletesMultipleTimesWithBlockingGC()
        {
            // This test verifies that the blocking GC + WaitForPendingFinalizers 
            // doesn't cause issues when RefreshAsync is called repeatedly.
            var provider = CreateMockProvider("TestProvider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app1" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            // Run multiple refreshes in succession (simulating background polling)
            for (int i = 0; i < 5; i++)
            {
                await service.RefreshAsync(new HashSet<string>());
            }

            // Should complete without exception and maintain correct state
            Assert.Single(service.AllWindows);
            Assert.Equal("Window1", service.AllWindows[0].Title);
        }

        [Fact]
        public async Task RefreshAsync_SkipsIfAlreadyInProgress()
        {
            // Arrange: Create a slow provider that simulates a long-running scan
            var slowProvider = new Mock<IWindowProvider>();
            slowProvider.Setup(p => p.PluginName).Returns("SlowProvider");
            slowProvider.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());

            var callCount = 0;
            slowProvider.Setup(p => p.GetWindows()).Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                Thread.Sleep(200); // Simulate slow scan
                return new List<WindowItem>();
            });

            var service = new WindowOrchestrationService(new[] { slowProvider.Object }, null, CreateMockSettingsService());

            // Act: Fire two concurrent refreshes
            var task1 = service.RefreshAsync(new HashSet<string>());
            await Task.Delay(50); // Let first one start
            var task2 = service.RefreshAsync(new HashSet<string>()); // Should be skipped

            await Task.WhenAll(task1, task2);

            // Assert: GetWindows should only be called once (second call was skipped)
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task RefreshAsync_AllowsSequentialCalls()
        {
            // Arrange
            var provider = CreateMockProvider("Provider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, null, CreateMockSettingsService());

            // Act: Call RefreshAsync twice sequentially (not concurrently)
            await service.RefreshAsync(new HashSet<string>());
            await service.RefreshAsync(new HashSet<string>());

            // Assert: Both calls should execute (GetWindows called twice)
            provider.Verify(p => p.GetWindows(), Times.Exactly(2));
        }
    }
}
