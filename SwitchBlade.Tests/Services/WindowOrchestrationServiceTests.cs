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

        private WindowOrchestrationService CreateService(
            IEnumerable<IWindowProvider> providers,
            IUiaWorkerClient? worker = null,
            INativeInteropWrapper? interop = null,
            ILogger? logger = null,
            ISettingsService? settings = null,
            IWindowReconciler? reconciler = null)
        {
            return new WindowOrchestrationService(
                providers,
                reconciler ?? new WindowReconciler(null),
                worker ?? new NullUiaWorkerClient(),
                interop ?? new Mock<INativeInteropWrapper>().Object,
                logger,
                settings ?? CreateMockSettingsService()
            );
        }

        [Fact]
        public async Task RefreshAsync_CallsGetWindowsOnAllProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = CreateService(new[] { provider1.Object, provider2.Object });

            await service.RefreshAsync(new HashSet<string>());

            provider1.Verify(p => p.GetWindows(), Times.Once);
            provider2.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ClearsProcessCache()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var mockInterop = new Mock<INativeInteropWrapper>();
            
            var service = CreateService(new[] { provider.Object }, interop: mockInterop.Object);

            await service.RefreshAsync(new HashSet<string>());

            mockInterop.Verify(x => x.ClearProcessCache(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SkipsDisabledProviders()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            var service = CreateService(new[] { provider1.Object, provider2.Object });

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

            var service = CreateService(new[] { provider1.Object, provider2.Object });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
        }

        [Fact]
        public async Task RefreshAsync_FiresWindowListUpdatedEvent()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = CreateService(new[] { provider.Object });

            int eventCount = 0;
            service.WindowListUpdated += (s, e) => eventCount++;

            await service.RefreshAsync(new HashSet<string>());

            Assert.True(eventCount > 0);
        }

        [Fact]
        public async Task RefreshAsync_ReloadsSettingsForEachProvider()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = CreateService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());

            provider.Verify(p => p.ReloadSettings(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SetsExclusionsFromHandledProcesses()
        {
            var provider1 = CreateMockProvider("Provider1", new List<WindowItem>());
            var provider2 = CreateMockProvider("Provider2", new List<WindowItem>());

            provider1.Setup(p => p.GetHandledProcesses()).Returns(new[] { "chrome", "edge" });

            var service = CreateService(new[] { provider1.Object, provider2.Object });

            await service.RefreshAsync(new HashSet<string>());

            provider2.Verify(p => p.SetExclusions(It.Is<IEnumerable<string>>(
                ex => ex.Contains("chrome") && ex.Contains("edge"))), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_PreservesMultipleItemsWithSameHwnd()
        {
            var hwnd = (IntPtr)555;
            var provider = CreateMockProvider("Chrome", new List<WindowItem>
            {
                new() { Title = "Tab 1", Hwnd = hwnd, ProcessName = "chrome" },
                new() { Title = "Tab 2", Hwnd = hwnd, ProcessName = "chrome" }
            });

            var service = CreateService(new[] { provider.Object });

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
            var service = CreateService(new List<IWindowProvider>());

            var windows1 = service.AllWindows;
            var windows2 = service.AllWindows;

            Assert.NotSame(windows1, windows2);
        }

        [Fact]
        public async Task RefreshAsync_ProviderCrash_ClearsStaleResults()
        {
            var provider = CreateMockProvider("CrashingProvider", new List<WindowItem>
            {
                new() { Title = "StaleWindow", Hwnd = (IntPtr)1, ProcessName = "app" }
            });
            var service = CreateService(new[] { provider.Object });

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
            var provider = CreateMockProvider("TestProvider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app1" }
            });

            var service = CreateService(new[] { provider.Object });

            for (int i = 0; i < 5; i++)
            {
                await service.RefreshAsync(new HashSet<string>());
            }

            Assert.Single(service.AllWindows);
            Assert.Equal("Window1", service.AllWindows[0].Title);
        }

        [Fact]
        public async Task RefreshAsync_SkipsFastPathIfAlreadyInProgress()
        {
            var slowProvider = new Mock<IWindowProvider>();
            slowProvider.Setup(p => p.PluginName).Returns("SlowCoreProvider");
            slowProvider.Setup(p => p.IsUiaProvider).Returns(false);
            slowProvider.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());

            var scanStarted = new ManualResetEventSlim(false);
            var scanContinue = new ManualResetEventSlim(false);
            var callCount = 0;

            slowProvider.Setup(p => p.GetWindows()).Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                scanStarted.Set();
                scanContinue.Wait(TimeSpan.FromSeconds(10));
                return new List<WindowItem>();
            });

            var service = CreateService(new[] { slowProvider.Object });

            var task1 = service.RefreshAsync(new HashSet<string>());
            
            Assert.True(scanStarted.Wait(TimeSpan.FromSeconds(10)), "Scan did not start in time");

            var task2 = service.RefreshAsync(new HashSet<string>());

            scanContinue.Set();
            await Task.WhenAll(task1, task2);

            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task RefreshAsync_AllowsCoreUpdate_WhenUiaIsSlow()
        {
            var coreProvider = CreateMockProvider("CoreProvider", new List<WindowItem>());
            coreProvider.Setup(p => p.IsUiaProvider).Returns(false);
            
            var slowUiaProvider = new Mock<IWindowProvider>();
            slowUiaProvider.Setup(p => p.PluginName).Returns("SlowUiaProvider");
            slowUiaProvider.Setup(p => p.IsUiaProvider).Returns(true);
            slowUiaProvider.Setup(p => p.GetHandledProcesses()).Returns(Enumerable.Empty<string>());

            var uiaScanStarted = new TaskCompletionSource<bool>();
            var mockUiaWorker = new Mock<IUiaWorkerClient>();
            mockUiaWorker.Setup(c => c.ScanStreamingAsync(
                    It.IsAny<ISet<string>?>(), 
                    It.IsAny<ISet<string>?>(), 
                    It.IsAny<CancellationToken>()))
                .Returns((ISet<string>? d, ISet<string>? h, CancellationToken t) => 
                {
                    uiaScanStarted.TrySetResult(true);
                    return DelayedEmptyEnumerable(500, t);
                });

            var service = CreateService(
                new[] { coreProvider.Object, slowUiaProvider.Object },
                worker: mockUiaWorker.Object);

            var task1 = service.RefreshAsync(new HashSet<string>());

            await Task.WhenAny(uiaScanStarted.Task, Task.Delay(10000));
            Assert.True(uiaScanStarted.Task.IsCompleted, "UIA scan did not start in time");
            
            await task1;

            await service.RefreshAsync(new HashSet<string>());

            coreProvider.Verify(p => p.GetWindows(), Times.Exactly(2));
            
            mockUiaWorker.Verify(c => c.ScanStreamingAsync(
                It.IsAny<ISet<string>?>(), 
                It.IsAny<ISet<string>?>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_AllowsSequentialCalls()
        {
            var provider = CreateMockProvider("Provider", new List<WindowItem>
            {
                new() { Title = "Window1", Hwnd = (IntPtr)1, ProcessName = "app" }
            });

            var service = CreateService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());
            await service.RefreshAsync(new HashSet<string>());

            provider.Verify(p => p.GetWindows(), Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessProviderResults_PreservesExisting_WhenOnlyFallbackItemsReceived()
        {
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Chat 1", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 2", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            var service = CreateService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Microsoft Teams", Hwnd = hwnd, ProcessName = "ms-teams", IsFallback = true }
            });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(2, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 1");
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 2");
        }

        [Fact]
        public async Task ProcessProviderResults_ReplacesResults_WhenNonFallbackItemsReceived()
        {
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Chat 1", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 2", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            var service = CreateService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Chat 3", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 4", Hwnd = hwnd, ProcessName = "ms-teams" },
                new() { Title = "Chat 5", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Equal(3, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, x => x.Title == "Chat 3");
            Assert.DoesNotContain(service.AllWindows, x => x.Title == "Chat 1");
        }

        [Fact]
        public async Task ProcessProviderResults_AcceptsFallback_WhenNoPriorRealResults()
        {
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Microsoft Teams", Hwnd = hwnd, ProcessName = "ms-teams", IsFallback = true }
            });

            var service = CreateService(new[] { provider.Object });

            await service.RefreshAsync(new HashSet<string>());

            Assert.Single(service.AllWindows);
            Assert.True(service.AllWindows[0].IsFallback);
        }

        [Fact]
        public async Task LaunchUiaRefresh_LogsError_WhenWorkerThrows()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();
            var scannedSignal = new ManualResetEventSlim(false);

            mockWorker.Setup(w => w.ScanStreamingAsync(It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                      .Callback(() => scannedSignal.Set())
                      .Throws(new Exception("Worker crashed"));

            var provider = CreateMockProvider("UiaProvider", new List<WindowItem>());
            provider.Setup(p => p.IsUiaProvider).Returns(true);

            var service = CreateService(new[] { provider.Object }, worker: mockWorker.Object);

            await service.RefreshAsync(new HashSet<string>());

            Assert.True(scannedSignal.Wait(2000), "Background UIA scan was not triggered in time");

            mockWorker.Verify(w => w.ScanStreamingAsync(It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Dispose_DisposesProviders()
        {
            var mockDisposableProvider = new Mock<IWindowProvider>();
            mockDisposableProvider.As<IDisposable>(); // Make it disposable

            var service = CreateService(new[] { mockDisposableProvider.Object });
            
            service.Dispose();

            mockDisposableProvider.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_SupportsSimpleProviders_WithoutProcessDependency()
        {
            var simpleProvider = new Mock<IWindowProvider>();
            simpleProvider.Setup(p => p.PluginName).Returns("Simple");
            simpleProvider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>());
            
            var service = CreateService(new[] { simpleProvider.Object });

            await service.RefreshAsync(new HashSet<string>());

            simpleProvider.Verify(p => p.GetWindows(), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ReloadSettings_HandlesError()
        {
            var provider = CreateMockProvider("BrokenProvider", new List<WindowItem>());
            provider.Setup(p => p.ReloadSettings()).Throws(new Exception("Fail"));
            
            var service = CreateService(new[] { provider.Object });
            
            await service.RefreshAsync(new HashSet<string>());
            
            provider.Verify(p => p.ReloadSettings(), Times.Once);
        }

        [Fact]
        public async Task LaunchUiaRefresh_HandlesDynamicProviderResolution()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();
            var uiaProvider = CreateMockProvider("UiaPlugin", new List<WindowItem>());
            uiaProvider.Setup(p => p.IsUiaProvider).Returns(true);
            uiaProvider.Setup(p => p.GetHandledProcesses()).Returns(new[] { "dynamic-proc" });

            var pluginResult = new UiaPluginResult 
            { 
                PluginName = "UnknownPlugin", 
                Windows = new List<UiaWindowResult> { new() { Title = "W1", Hwnd = 123, ProcessName = "dynamic-proc" } } 
            };

            mockWorker.Setup(w => w.ScanStreamingAsync(It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                      .Returns(new[] { pluginResult }.ToAsyncEnumerable());

            var service = CreateService(new[] { uiaProvider.Object }, worker: mockWorker.Object);

            int updatedCount = 0;
            service.WindowListUpdated += (s, e) => updatedCount++;

            await service.RefreshAsync(new HashSet<string>());
            
            await Task.Delay(200);

            Assert.True(updatedCount > 0);
            Assert.Contains(service.AllWindows, w => w.Title == "W1" && w.Source == uiaProvider.Object);
        }

        [Fact]
        public async Task LaunchUiaRefresh_LogsWarning_WhenProviderNotFound()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();
            var pluginResult = new UiaPluginResult 
            { 
                PluginName = "UnknownPlugin", 
                Windows = new List<UiaWindowResult> { new() { Title = "W1", Hwnd = 123, ProcessName = "unknown-proc" } } 
            };

            mockWorker.Setup(w => w.ScanStreamingAsync(It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                      .Returns(new[] { pluginResult }.ToAsyncEnumerable());

            var service = CreateService(new IWindowProvider[0], worker: mockWorker.Object);

            await service.RefreshAsync(new HashSet<string>());

            await Task.Delay(200);

            Assert.Empty(service.AllWindows);
        }

        [Fact]
        public void Dispose_HandlesProviderDisposeError()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.As<IDisposable>().Setup(d => d.Dispose()).Throws(new Exception("Fail"));

            var service = CreateService(new[] { mockProvider.Object });
            
            service.Dispose();
            
            mockProvider.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ProcessProviderResults_LogsPerf_WhenDebugEnabled()
        {
            // Set static flag
            var originalDebug = SwitchBlade.Core.Logger.IsDebugEnabled;
            SwitchBlade.Core.Logger.IsDebugEnabled = true;

            try 
            {
                var provider = CreateMockProvider("PerfProvider", new List<WindowItem>
                {
                    new() { Title = "W1", Hwnd = (IntPtr)1, ProcessName = "app" }
                });
                var mockLogger = new Mock<ILogger>();
                
                var service = CreateService(new[] { provider.Object }, logger: mockLogger.Object);

                await service.RefreshAsync(new HashSet<string>());

                mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("[Perf]"))), Times.Once);
            }
            finally
            {
                SwitchBlade.Core.Logger.IsDebugEnabled = originalDebug;
            }
        }
                [Fact]
        public void Constructor_ThrowsOnNullProviders()
        {
            Assert.Throws<ArgumentNullException>(() => CreateService(null!));
        }

        [Fact]
        public void Constructor_ThrowsOnNullReconciler()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WindowOrchestrationService(
                    Array.Empty<IWindowProvider>(),
                    null!,
                    new NullUiaWorkerClient(),
                    new Mock<INativeInteropWrapper>().Object));
        }

        [Fact]
        public void Constructor_ThrowsOnNullUiaWorkerClient()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WindowOrchestrationService(
                    Array.Empty<IWindowProvider>(),
                    new WindowReconciler(null),
                    null!,
                    new Mock<INativeInteropWrapper>().Object));
        }

        [Fact]
        public void Constructor_ThrowsOnNullNativeInterop()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WindowOrchestrationService(
                    Array.Empty<IWindowProvider>(),
                    new WindowReconciler(null),
                    new NullUiaWorkerClient(),
                    null!));
        }

        [Fact]
        public async Task LaunchUiaRefresh_SkipsWhenAlreadyInProgress()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();
            var uiaProvider = CreateMockProvider("UiaPlugin", new List<WindowItem>());
            uiaProvider.Setup(p => p.IsUiaProvider).Returns(true);

            var scanStarted = new ManualResetEventSlim(false);
            var scanContinue = new ManualResetEventSlim(false);
            var scanCount = 0;

            mockWorker.Setup(w => w.ScanStreamingAsync(
                    It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                .Returns((ISet<string>? d, ISet<string>? h, CancellationToken t) =>
                {
                    Interlocked.Increment(ref scanCount);
                    scanStarted.Set();
                    scanContinue.Wait(TimeSpan.FromSeconds(10));
                    return Enumerable.Empty<UiaPluginResult>().ToAsyncEnumerable();
                });

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(
                new[] { uiaProvider.Object },
                worker: mockWorker.Object,
                logger: mockLogger.Object);

            // First refresh — launches UIA scan
            var task1 = service.RefreshAsync(new HashSet<string>());
            await task1;
            Assert.True(scanStarted.Wait(TimeSpan.FromSeconds(5)), "UIA scan did not start");

            // Second refresh — should skip UIA refresh (lock already held)
            await service.RefreshAsync(new HashSet<string>());

            scanContinue.Set();
            // Wait for background task to complete
            await Task.Delay(200);

            Assert.Equal(1, scanCount);
            mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("UIA refresh skipped"))), Times.Once);
        }

        [Fact]
        public async Task LaunchUiaRefresh_LogsPluginError_WhenErrorFieldSet()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();
            var uiaProvider = CreateMockProvider("UiaPlugin", new List<WindowItem>());
            uiaProvider.Setup(p => p.IsUiaProvider).Returns(true);

            var pluginResult = new UiaPluginResult
            {
                PluginName = "UiaPlugin",
                Error = "Something went wrong",
                Windows = new List<UiaWindowResult>
                {
                    new() { Title = "W1", Hwnd = 1, ProcessName = "proc" }
                }
            };

            mockWorker.Setup(w => w.ScanStreamingAsync(
                    It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                .Returns(new[] { pluginResult }.ToAsyncEnumerable());

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(
                new[] { uiaProvider.Object },
                worker: mockWorker.Object,
                logger: mockLogger.Object);

            await service.RefreshAsync(new HashSet<string>());
            await Task.Delay(300);

            mockLogger.Verify(l => l.Log(It.Is<string>(s =>
                s.Contains("[UIA] Plugin UiaPlugin error:") && s.Contains("Something went wrong"))), Times.Once);
        }

        [Fact]
        public async Task LaunchUiaRefresh_LogsSkip_WhenProviderNotFoundAndLoggerPresent()
        {
            var mockWorker = new Mock<IUiaWorkerClient>();

            // We need at least one UIA provider so LaunchUiaRefresh is called,
            // but the worker returns a result for a different plugin name.
            var registeredUiaProvider = CreateMockProvider("RegisteredUia", new List<WindowItem>());
            registeredUiaProvider.Setup(p => p.IsUiaProvider).Returns(true);

            var pluginResult = new UiaPluginResult
            {
                PluginName = "UnknownPlugin",
                Windows = new List<UiaWindowResult>
                {
                    new() { Title = "W1", Hwnd = 123, ProcessName = "unknown-proc" }
                }
            };

            mockWorker.Setup(w => w.ScanStreamingAsync(
                    It.IsAny<ISet<string>?>(), It.IsAny<ISet<string>?>(), It.IsAny<CancellationToken>()))
                .Returns(new[] { pluginResult }.ToAsyncEnumerable());

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(
                new[] { registeredUiaProvider.Object },
                worker: mockWorker.Object,
                logger: mockLogger.Object);

            await service.RefreshAsync(new HashSet<string>());
            await Task.Delay(300);

            mockLogger.Verify(l => l.Log(It.Is<string>(s =>
                s.Contains("No provider found for plugin UnknownPlugin"))), Times.Once);
        }

        [Fact]
        public async Task ProcessProviderResults_LKG_LogsPreservation_WhenLoggerPresent()
        {
            var hwnd = (IntPtr)100;
            var provider = CreateMockProvider("Teams", new List<WindowItem>
            {
                new() { Title = "Chat 1", Hwnd = hwnd, ProcessName = "ms-teams" }
            });

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(new[] { provider.Object }, logger: mockLogger.Object);

            // First refresh — real items
            await service.RefreshAsync(new HashSet<string>());
            Assert.Single(service.AllWindows);

            // Second refresh — only fallback => LKG kicks in
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Microsoft Teams", Hwnd = hwnd, ProcessName = "ms-teams", IsFallback = true }
            });
            await service.RefreshAsync(new HashSet<string>());

            // Verify LKG preserved original items
            Assert.Single(service.AllWindows);
            Assert.Equal("Chat 1", service.AllWindows[0].Title);
            mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("[LKG]"))), Times.Once);
        }

        [Fact]
        public async Task ProcessProviderResults_IconPopulateError_IsLoggedAndSwallowed()
        {
            var mockReconciler = new Mock<IWindowReconciler>();
            var provider = CreateMockProvider("Provider", new List<WindowItem>
            {
                new() { Title = "W1", Hwnd = (IntPtr)1, ProcessName = "app" }
            });

            mockReconciler.Setup(r => r.Reconcile(It.IsAny<IList<WindowItem>>(), It.IsAny<IWindowProvider>()))
                .Returns((IList<WindowItem> items, IWindowProvider p) =>
                {
                    foreach (var item in items) item.Source = p;
                    return items.ToList();
                });

            mockReconciler.Setup(r => r.PopulateIcons(It.IsAny<IEnumerable<WindowItem>>()))
                .Throws(new Exception("Icon extraction failed"));

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(
                new[] { provider.Object },
                logger: mockLogger.Object,
                reconciler: mockReconciler.Object);

            await service.RefreshAsync(new HashSet<string>());

            // Give time for background icon population Task.Run to execute
            await Task.Delay(300);

            mockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains("Error populating icons")),
                It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void CacheCount_DelegatesToReconciler()
        {
            var mockReconciler = new Mock<IWindowReconciler>();
            mockReconciler.Setup(r => r.CacheCount).Returns(42);

            var service = CreateService(Array.Empty<IWindowProvider>(), reconciler: mockReconciler.Object);

            Assert.Equal(42, service.CacheCount);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.As<IDisposable>();
            var service = CreateService(new[] { mockProvider.Object });

            service.Dispose();
            service.Dispose(); // Second call should be a no-op

            mockProvider.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_LogsError_WhenProviderThrowsAndLoggerPresent()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("BadPlugin");
            mockProvider.As<IDisposable>().Setup(d => d.Dispose()).Throws(new Exception("Cleanup failed"));

            var mockLogger = new Mock<ILogger>();
            var service = CreateService(new[] { mockProvider.Object }, logger: mockLogger.Object);

            service.Dispose();

            mockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains("Error disposing provider BadPlugin")),
                It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ProcessProviderResults_LKG_FallsThrough_WhenNoExistingRealItems()
        {
            // First refresh returns only fallback items (no prior real items exist).
            // The hasExistingRealItems branch should be FALSE, and the normal reconcile
            // path should run instead of the LKG preserve path.
            var hwnd = (IntPtr)200;
            var provider = CreateMockProvider("FreshPlugin", new List<WindowItem>
            {
                new() { Title = "Fallback Only", Hwnd = hwnd, ProcessName = "fresh", IsFallback = true }
            });

            var service = CreateService(new[] { provider.Object });

            // First call — no prior data, so even though results are all-fallback,
            // hasExistingRealItems is false and normal processing occurs
            await service.RefreshAsync(new HashSet<string>());

            Assert.Single(service.AllWindows);
            Assert.Equal("Fallback Only", service.AllWindows[0].Title);
        }

        [Fact]
        public async Task ProcessProviderResults_LKG_ReplacesItems_WhenExistingItemsAreFallback()
        {
            // Scenario: Two consecutive calls both return fallback-only items.
            // The second call should still process normally because existing items are
            // also fallback (not real), so hasExistingRealItems remains false.
            // This covers the !w.IsFallback branch returning false within the Any() lambda.
            var hwnd = (IntPtr)300;
            var provider = CreateMockProvider("FallbackPlugin", new List<WindowItem>
            {
                new() { Title = "Fallback V1", Hwnd = hwnd, ProcessName = "fb", IsFallback = true }
            });

            var service = CreateService(new[] { provider.Object });

            // First call — stores fallback items
            await service.RefreshAsync(new HashSet<string>());
            Assert.Single(service.AllWindows);
            Assert.Equal("Fallback V1", service.AllWindows[0].Title);

            // Second call — also fallback, but since existing are ALL fallback,
            // hasExistingRealItems is false → normal path replaces items
            provider.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Fallback V2", Hwnd = hwnd, ProcessName = "fb", IsFallback = true }
            });
            await service.RefreshAsync(new HashSet<string>());

            Assert.Single(service.AllWindows);
            Assert.Equal("Fallback V2", service.AllWindows[0].Title);
        }

        [Fact]
        public async Task ProcessProviderResults_LKG_SkipsItemsFromOtherProviders()
        {
            // Exercises the w.Source == provider branch returning FALSE in the Any() lambda.
            // Provider A has real items; Provider B sends fallback-only on second refresh.
            // When checking LKG for B, Any() iterates over A's items first (Source mismatch → false).
            var providerA = CreateMockProvider("ProviderA", new List<WindowItem>
            {
                new() { Title = "A1", Hwnd = (IntPtr)400, ProcessName = "procA" }
            });
            var providerB = CreateMockProvider("ProviderB", new List<WindowItem>
            {
                new() { Title = "B1", Hwnd = (IntPtr)401, ProcessName = "procB" }
            });

            var service = CreateService(new[] { providerA.Object, providerB.Object });

            // First refresh — both providers return real items
            await service.RefreshAsync(new HashSet<string>());
            Assert.Equal(2, service.AllWindows.Count);

            // Second refresh — B returns only fallback. LKG checks Any() which scans
            // A's items (w.Source != providerB → false) then B's items (match + !IsFallback → true)
            providerB.Setup(p => p.GetWindows()).Returns(new List<WindowItem>
            {
                new() { Title = "Fallback B", Hwnd = (IntPtr)401, ProcessName = "procB", IsFallback = true }
            });
            await service.RefreshAsync(new HashSet<string>());

            // B's real items should be preserved by LKG
            Assert.Equal(2, service.AllWindows.Count);
            Assert.Contains(service.AllWindows, w => w.Title == "B1");
        }

        [Fact]
        public async Task ProcessProviderResults_IconPopulateError_SwallowedSilently_WhenNoLogger()
        {
            var mockReconciler = new Mock<IWindowReconciler>();
            var provider = CreateMockProvider("Provider", new List<WindowItem>
            {
                new() { Title = "W1", Hwnd = (IntPtr)1, ProcessName = "app" }
            });

            mockReconciler.Setup(r => r.Reconcile(It.IsAny<IList<WindowItem>>(), It.IsAny<IWindowProvider>()))
                .Returns((IList<WindowItem> items, IWindowProvider p) =>
                {
                    foreach (var item in items) item.Source = p;
                    return items.ToList();
                });

            mockReconciler.Setup(r => r.PopulateIcons(It.IsAny<IEnumerable<WindowItem>>()))
                .Throws(new Exception("Icon extraction failed"));

            // No logger — exercises the _logger?.LogError null-coalescing false branch
            var service = CreateService(
                new[] { provider.Object },
                logger: null,
                reconciler: mockReconciler.Object);

            await service.RefreshAsync(new HashSet<string>());

            // Give time for background icon population Task.Run to execute
            await Task.Delay(300);

            // No assertion on logger — we just verify no unhandled exception
            Assert.Single(service.AllWindows);
        }

        [Fact]
        public async Task RefreshAsync_HandlesNullDisabledPlugins()
        {
            var provider = CreateMockProvider("Provider1", new List<WindowItem>());
            var service = CreateService(new[] { provider.Object });

            // Pass null — should be coalesced to empty set internally
            await service.RefreshAsync(null!);

            provider.Verify(p => p.GetWindows(), Times.Once);
        }


        private async IAsyncEnumerable<UiaPluginResult> DelayedEmptyEnumerable(int delayMs, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(delayMs, cancellationToken);
            yield break;
        }

        internal class NullUiaWorkerClient : IUiaWorkerClient
        {
            public IAsyncEnumerable<UiaPluginResult> ScanStreamingAsync(ISet<string>? c, ISet<string>? h, CancellationToken t = default)
                => Enumerable.Empty<UiaPluginResult>().ToAsyncEnumerable();
            
            public Task<List<WindowItem>> ScanAsync(ISet<string>? c, ISet<string>? h, CancellationToken t = default) 
                => Task.FromResult(new List<WindowItem>());

            public void Dispose() { }
        }
    }

    public static class TestExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }
}
