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

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, (IIconService?)null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            provider1.Verify(p => p.GetWindows(), Times.Once);
            provider2.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SkipsDisabledProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, (IIconService?)null, CreateMockSettingsService());

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

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, (IIconService?)null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
        }

        [Fact]
        public async Task RefreshAsync_FiresWindowListUpdatedEvent()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            int eventCount = 0;
            service.WindowListUpdated += (s, e) => eventCount++;

            await service.RefreshAsync(new HashSet<string>());

            Assert.True(eventCount > 0);
        }

        [Fact]
        public async Task RefreshAsync_ReloadsSettingsForEachProvider()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            provider.Verify(p => p.ReloadSettings(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SetsExclusionsFromHandledProcesses()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            provider1.Setup(p => p.GetHandledProcesses()).Returns(new[] { "chrome", "edge" });

            var service = new WindowOrchestrationService(new[] { provider1.Object, provider2.Object }, (IIconService?)null, CreateMockSettingsService());

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

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

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
            var service = new WindowOrchestrationService(new List<IWindowProvider>(), (IIconService?)null, CreateMockSettingsService());

            var windows1 = service.AllWindows;
            var windows2 = service.AllWindows;

            Assert.NotSame(windows1, windows2); // Different instances
        }

        [Fact]
        public async Task RefreshAsync_ProviderCrash_ClearsStaleResults()
        {
            var provider = CreateMockProvider("CrashingProvider", new List<WindowItem>
            {
                new() { Title = "StaleWindow", Hwnd = (IntPtr)1, ProcessName = "app" }
            });
            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            // 1. First run - success
            await service.RefreshAsync(new HashSet<string>());
            Assert.Single(service.AllWindows);

            // 2. Second run - crash
            provider.Setup(p => p.GetWindows()).Throws(new Exception("Crash!"));

            await service.RefreshAsync(new HashSet<string>());

            // 3. Assert - Results should be cleared despite crash
            Assert.Empty(service.AllWindows);
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

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

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
        public async Task RefreshAsync_SkipsFastPathIfAlreadyInProgress()
        {
            // Arrange: Create a slow Non-UIA provider that simulates a long-running scan
            var slowProvider = new Mock<IWindowProvider>();
            slowProvider.Setup(p => p.PluginName).Returns("SlowCoreProvider");
            slowProvider.Setup(p => p.IsUiaProvider).Returns(false); // Non-UIA
            slowProvider.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());

            var scanStarted = new ManualResetEventSlim(false);
            var scanContinue = new ManualResetEventSlim(false);
            var callCount = 0;

            slowProvider.Setup(p => p.GetWindows()).Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                scanStarted.Set();
                scanContinue.Wait(TimeSpan.FromSeconds(10)); // Wait for test to signal
                return new List<WindowItem>();
            });

            var service = new WindowOrchestrationService(new[] { slowProvider.Object }, (IIconService?)null, CreateMockSettingsService());

            // Act: Fire first refresh (will hang on scanContinue)
            var task1 = service.RefreshAsync(new HashSet<string>());
            
            // Wait for it to actually enter the provider
            Assert.True(scanStarted.Wait(TimeSpan.FromSeconds(10)), "Scan did not start in time");

            // Fire second refresh while first is still running (hitting the fast-path lock)
            var task2 = service.RefreshAsync(new HashSet<string>());

            // Signal first one to complete
            scanContinue.Set();
            await Task.WhenAll(task1, task2);

            // Assert: GetWindows should only be called once (second call was skipped)
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task RefreshAsync_AllowsCoreUpdate_WhenUiaIsSlow()
        {
            // Arrange: 
            // 1. Fast Core Provider (Non-UIA)
            var coreProvider = CreateMockProvider("CoreProvider", new List<WindowItem>());
            coreProvider.Setup(p => p.IsUiaProvider).Returns(false);
            
            // 2. Slow UIA Provider
            var slowUiaProvider = new Mock<IWindowProvider>();
            slowUiaProvider.Setup(p => p.PluginName).Returns("SlowUiaProvider");
            slowUiaProvider.Setup(p => p.IsUiaProvider).Returns(true); // UIA
            slowUiaProvider.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());

            // Signal when UIA scan actually starts to ensure lock is held
            var uiaScanStarted = new TaskCompletionSource<bool>();

            // We need to use the primary constructor to inject a mock UiaWorkerClient
            var mockUiaWorker = new Mock<IUiaWorkerClient>();
            // Simulate slow UIA streaming
            // Note: Must provide all optional arguments to Setup to avoid CS0854 (Expression tree may not contain call with optional arguments)
            mockUiaWorker.Setup(c => c.ScanStreamingAsync(
                    It.IsAny<ISet<string>?>(), 
                    It.IsAny<ISet<string>?>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((ISet<string>? d, ISet<string>? h, CancellationToken t) => 
                {
                    uiaScanStarted.TrySetResult(true);
                    return DelayedEmptyEnumerable(500, t);
                });

            var service = new WindowOrchestrationService(
                new[] { coreProvider.Object, slowUiaProvider.Object },
                new WindowReconciler(null),
                mockUiaWorker.Object,
                null, 
                CreateMockSettingsService());

            // Act:
            // 1. Start first refresh (launches slow UIA task)
            var task1 = service.RefreshAsync(new HashSet<string>());

            // Wait for UIA scan to start and hold the lock
            await Task.WhenAny(uiaScanStarted.Task, Task.Delay(10000));
            Assert.True(uiaScanStarted.Task.IsCompleted, "UIA scan did not start in time");
            
            // 2. Start second refresh IMMEDIATELY (while UIA is still "running" in bg)
            await service.RefreshAsync(new HashSet<string>());

            await task1;

            // Assert:
            // Core provider should have run TWICE (because _fastRefreshLock was free)
            coreProvider.Verify(p => p.GetWindows(), Times.Exactly(2));
            
            // UIA Worker should have been called TWICE (fire-and-forget, but verify launch)
            // Note: In logical terms, the second UIA scan might be skipped if the first is truly holding the lock.
            // Let's verify that core *did* run twice, which is the key requirement.
            // Actually, since UIA is fire-and-forget, the second RefreshAsync call *will* try to acquire UIA lock.
            // If the first UIA task is still running (500ms delay), the second attempt to acquire _uiaRefreshLock will fail (return immediately).
            // So ScanStreamingAsync should be called ONCE effectively (or twice if the first one finished super fast, but we delayed 500ms).
            
            // Verify ScanStreamingAsync called exactly ONCE confirms that the second UIA scan was properly skipped 
            // (good behavior) while Core ran twice (excellent behavior).
            mockUiaWorker.Verify(c => c.ScanStreamingAsync(
                It.IsAny<ISet<string>?>(), 
                It.IsAny<ISet<string>?>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_AllowsSequentialCalls()
        {
            // Arrange
            var provider = CreateMockProvider("Provider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            // Act: Call RefreshAsync twice sequentially (not concurrently)
            await service.RefreshAsync(new HashSet<string>());
            await service.RefreshAsync(new HashSet<string>());

            // Assert: Both calls should execute (GetWindows called twice)
            provider.Verify(p => p.GetWindows(), Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessProviderResults_PreservesExisting_WhenOnlyFallbackItemsReceived()
        {
            // Arrange: Provider returns real items on first scan
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Chat 1", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 2", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            // First scan - returns real items
            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            // Second scan - returns only a fallback item (simulates transient UIA failure)
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Microsoft Teams", Hwnd = hwnd, ProcessName = "ms-teams", IsFallback = true }
            });

            await service.RefreshAsync(new HashSet<string>());

            // Assert: LKG should preserve the previous 2 real items
            Assert.Equal(2, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 1");
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 2");
        }

        [Fact]
        public async Task ProcessProviderResults_ReplacesResults_WhenNonFallbackItemsReceived()
        {
            // Arrange: Provider returns real items on first scan
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Chat 1", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 2", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            // First scan
            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            // Second scan - returns different real items (normal update)
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Chat 3", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 4", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 5", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            await service.RefreshAsync(new HashSet<string>());

            // Assert: Results should be replaced with the new ones
            Assert.Equal(3, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 3");
            Assert.DoesNotContain(service.AllWindows, x => x.Title == "Chat 1");
        }

        [Fact]
        public async Task ProcessProviderResults_AcceptsFallback_WhenNoPriorRealResults()
        {
            // Arrange: Provider returns only fallback on FIRST scan (no prior data to preserve)
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Microsoft Teams", Hwnd = hwnd, ProcessName = "ms-teams", IsFallback = true }
            });

            var service = new WindowOrchestrationService(new[] { provider.Object }, (IIconService?)null, CreateMockSettingsService());

            await service.RefreshAsync(new HashSet<string>());

            // Assert: Fallback should be accepted since there's nothing to preserve
            Assert.Single(service.AllWindows);
            Assert.True(service.AllWindows[0].IsFallback);
        }

        private async IAsyncEnumerable<UiaPluginResult> DelayedEmptyEnumerable(int delayMs, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(delayMs, cancellationToken);
            yield break;
        }
    }
}
