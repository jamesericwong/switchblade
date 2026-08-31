using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class MemoryDiagnosticsServiceTests
    {
        private readonly Mock<IWindowOrchestrationService> _mockOrch;
        private readonly Mock<IIconService> _mockIcon;
        private readonly Mock<IWindowSearchService> _mockSearch;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IProcessFactory> _mockProcFactory;
        private readonly Mock<IProcess> _mockProcess;
        private readonly Mock<IMemoryInfoProvider> _mockMemInfo;
        private readonly Mock<IPeriodicTimer> _mockTimer;

        public MemoryDiagnosticsServiceTests()
        {
            _mockOrch = new Mock<IWindowOrchestrationService>();
            _mockIcon = new Mock<IIconService>();
            _mockSearch = new Mock<IWindowSearchService>();
            _mockLogger = new Mock<ILogger>();
            _mockProcFactory = new Mock<IProcessFactory>();
            _mockProcess = new Mock<IProcess>();
            _mockMemInfo = new Mock<IMemoryInfoProvider>();
            _mockTimer = new Mock<IPeriodicTimer>();

            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Returns(_mockProcess.Object);
            _mockProcFactory.Setup(f => f.Start(It.IsAny<System.Diagnostics.ProcessStartInfo>())).Returns(_mockProcess.Object);
        }

        private MemoryDiagnosticsService CreateService(
            IMemoryInfoProvider? memInfo = null,
            Func<TimeSpan, IPeriodicTimer>? timerFactory = null)
        {
            return new MemoryDiagnosticsService(
                _mockOrch.Object,
                _mockIcon.Object,
                _mockSearch.Object,
                _mockLogger.Object,
                _mockProcFactory.Object,
                memInfo ?? _mockMemInfo.Object,
                timerFactory ?? ((_) => _mockTimer.Object),
                TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Constructor_NullArguments_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryDiagnosticsService(null!, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new MemoryDiagnosticsService(_mockOrch.Object, null!, _mockSearch.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, _mockSearch.Object, null!));
        }

        [Fact]
        public void Constructor_DefaultDependencies_CreatedSuccessfully()
        {
             // Test the path where optional args are null
             var service = new MemoryDiagnosticsService(
                _mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object);
             Assert.NotNull(service);
             service.Dispose();
        }

        [Fact]
        public void Constructor_PartialDependencies_CreatedSuccessfully()
        {
             // Test some optional args provided, others null
             var service = new MemoryDiagnosticsService(
                _mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, 
                _mockProcFactory.Object, null, null, null);
             Assert.NotNull(service);
             service.Dispose();
        }

        [Fact]
        public void ForceLogMemoryStats_LogsMessage()
        {
            using var service = CreateService();
            _mockMemInfo.Setup(m => m.GetTotalMemory(false)).Returns(1024 * 1024 * 10); // 10MB
            _mockProcess.Setup(p => p.WorkingSet64).Returns(1024 * 1024 * 20);
            _mockProcess.Setup(p => p.PrivateMemorySize64).Returns(1024 * 1024 * 30);
            _mockProcess.Setup(p => p.HandleCount).Returns(100);
            _mockProcess.Setup(p => p.ThreadCount).Returns(5);
            _mockOrch.Setup(o => o.CacheCount).Returns(50);
            _mockIcon.Setup(i => i.CacheCount).Returns(10);

            service.ForceLogMemoryStats();
            
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("[MEM-DIAG]") && s.Contains("10 MB"))), Times.Once());
            _mockProcess.Verify(p => p.Refresh(), Times.Once());
        }

        [Fact]
        public void ForceLogMemoryStats_WhenError_LogsError()
        {
            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Throws(new Exception("Fail"));
            using var service = CreateService();
            
            service.ForceLogMemoryStats();
            
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to log memory stats")), It.IsAny<Exception>()), Times.Once());
        }

        [Fact]
        public async Task StartAndStop_runsLoop()
        {
            _mockTimer.SetupSequence(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(true)
                      .ReturnsAsync(false); // Stop loop

            using var service = CreateService();
            
            await service.StartAsync(CancellationToken.None);
            
            // StartAsync returns immediately; wait until the loop has executed its first tick.
            await WaitForAsync(() => _mockProcess.Invocations.Count >= 1);

            _mockLogger.Verify(l => l.Log("MemoryDiagnosticsService starting..."), Times.Once());
            _mockProcess.Verify(p => p.Refresh(), Times.Once()); // Called once inside loop

            await service.StopAsync(CancellationToken.None);
            _mockLogger.Verify(l => l.Log("MemoryDiagnosticsService stopping..."), Times.Once());
        }

        [Fact]
        public async Task DiagnosticsLoop_WhenTickThrows_LogsAndContinues()
        {
            // First tick throws, second returns false to exit
            _mockTimer.SetupSequence(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new Exception("Loop Fail"))
                      .ReturnsAsync(false);

            using var service = CreateService();

            await service.StartAsync(CancellationToken.None);
            await WaitForAsync(() => _mockLogger.Invocations.Any(i => i.Arguments.Count > 0 && i.Arguments[0] is string s && s.Contains("loop error")));

            _mockLogger.Verify(l => l.LogError("MemoryDiagnosticsService loop error", It.IsAny<Exception>()), Times.Once());
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_WhenLoopRunning_CancelsTimer()
        {
            var cts = new CancellationTokenSource();
            _mockTimer.Setup(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                      .Returns(async (CancellationToken token) => {
                          await Task.Delay(500, token); // Simulate waiting
                          return true; 
                      });

            using var service = CreateService();
            await service.StartAsync(CancellationToken.None);

            var stopTask = service.StopAsync(CancellationToken.None);
            
            await stopTask; // Should complete when cancellation triggers

            _mockLogger.Verify(l => l.Log("MemoryDiagnosticsService stopping..."), Times.Once());
        }

        [Fact]
        public async Task RunDiagnosticsLoop_HandlesOperationCanceledException_ByBubblingToStopAsync()
        {
             _mockTimer.Setup(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new OperationCanceledException());

             using var service = CreateService();
             await service.StartAsync(CancellationToken.None);
             
             // StopAsync should await the loop and catch the TCE
             await service.StopAsync(CancellationToken.None);
             
             // Verify no error log (loop's catch block was bypassed)
             _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never());
        }
        
        [Fact]
        public async Task StopAsync_SafeIfTaskNull()
        {
            using var service = CreateService();
            // StartAsync NOT called
            await service.StopAsync(CancellationToken.None);
            // Should not throw
        }

        [Fact]
        public void Dispose_DisposesTimerAndCts()
        {
            using var service = CreateService();
            service.Dispose();
            
            _mockTimer.Verify(t => t.Dispose(), Times.Once());
        }

        [Fact]
        public async Task Dispose_WhileLoopRunning_StopsInFlightWaitWithoutErrorLog()
        {
            // Production path: App starts the loop, then DI-container disposal calls Dispose()
            // directly (no StopAsync). The pending timer wait is registered on _cts.Token, so
            // Dispose must cancel that token — otherwise the wait outlives the service and a
            // disposed-timer fault surfaces as a spurious "loop error" at shutdown.
            var started = new ManualResetEventSlim(false);
            var heldRegistrations = new List<CancellationTokenRegistration>();
            Task<bool>? pendingWait = null;

            _mockTimer.Setup(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken ct) =>
                {
                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (ct.CanBeCanceled && !ct.IsCancellationRequested)
                    {
                        // Faithful to PeriodicTimer: a cancelled pending wait faults with OperationCanceledException.
                        heldRegistrations.Add(ct.Register(() => tcs.TrySetException(new OperationCanceledException())));
                    }
                    pendingWait = tcs.Task;
                    started.Set();
                    return new ValueTask<bool>(tcs.Task);
                });

            var service = CreateService();
            await service.StartAsync(CancellationToken.None);

            if (!started.Wait(5000))
            {
                throw new Exception("Timed out waiting for the diagnostics loop to enter its timer wait");
            }

            // Dispose directly — exactly what App.OnExit's container disposal does.
            service.Dispose();

            var completedInTime = await Task.WhenAny(pendingWait!, Task.Delay(3000)) == pendingWait;
            Assert.True(completedInTime, "Dispose did not cancel the token driving the in-flight timer wait");

            var waitTask = pendingWait!;
            if (waitTask.IsFaulted)
            {
                Assert.IsAssignableFrom<OperationCanceledException>(waitTask.Exception!.InnerExceptions.First());
            }

            // The loop's quiet-OCE contract: cancellation must not be logged as a shutdown error.
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("loop error")), It.IsAny<Exception>()), Times.Never());
            _mockTimer.Verify(t => t.Dispose(), Times.Once());
        }

        [Fact]
        public void Dispose_Twice_DoesNotThrow()
        {
            var service = CreateService();

            service.Dispose();
            Assert.Null(Record.Exception(() => service.Dispose()));
            _mockTimer.Verify(t => t.Dispose(), Times.Once()); // double dispose must not re-dispose the timer
        }

        private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (!condition())
            {
                if (Environment.TickCount64 >= deadline)
                {
                    throw new TimeoutException($"Condition not met within {timeoutMs}ms");
                }

                await Task.Delay(10);
            }
        }
    }
}
