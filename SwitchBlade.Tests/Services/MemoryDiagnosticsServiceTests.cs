using System;
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
            
            // Wait a bit for the background task to spin (impl detail: StartAsync returns immediately)
            // But since we mocked Timer to return immediately, the loop should run fast.
            // We need to wait for the loop task to finish if we want to check side effects *after* loop.
            // But we don't expose the task.
            // We can verify calls happening eventually.

            await Task.Delay(50); // Yield to background task

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
            await Task.Delay(50); // Let loop run

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
    }
}
