using System;
using System.Collections.Generic;
using System.Linq;
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

        [Fact]
        public async Task RefreshAsync_CallsGetWindowsOnAllProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object });

            await service.RefreshAsync(new HashSet<string>());

            provider1.Verify(p => p.GetWindows(), Times.Once);
            provider2.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SkipsDisabledProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object });

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

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
        }

        [Fact]
        public async Task RefreshAsync_FiresWindowListUpdatedEvent()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object });

            int eventCount = 0;
            service.WindowListUpdated += (s, e) => eventCount++;

            await service.RefreshAsync(new HashSet<string>());

            Assert.True(eventCount > 0);
        }

        [Fact]
        public async Task RefreshAsync_ReloadsSettingsForEachProvider()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());

            provider.Verify(p => p.ReloadSettings(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SetsExclusionsFromHandledProcesses()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            provider1.Setup(p => p.GetHandledProcesses()).Returns(new[] { "chrome", "edge" });

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object });

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

            var service = new WindowOrchestrationService(new[] { provider.Object });

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
            var service = new WindowOrchestrationService(new List<IWindowProvider>());

            var windows1 = service.AllWindows;
            var windows2 = service.AllWindows;

            Assert.NotSame(windows1, windows2); // Different instances
        }
    }
}
