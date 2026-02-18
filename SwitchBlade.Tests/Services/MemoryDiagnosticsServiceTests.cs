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

        public MemoryDiagnosticsServiceTests()
        {
            _mockOrch = new Mock<IWindowOrchestrationService>();
            _mockIcon = new Mock<IIconService>();
            _mockSearch = new Mock<IWindowSearchService>();
            _mockLogger = new Mock<ILogger>();
            _mockProcFactory = new Mock<IProcessFactory>();
            _mockProcess = new Mock<IProcess>();

            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Returns(_mockProcess.Object);
            _mockProcFactory.Setup(f => f.Start(It.IsAny<System.Diagnostics.ProcessStartInfo>())).Returns(_mockProcess.Object);
        }

        [Fact]
        public void ForceLogMemoryStats_LogsMessage()
        {
            using var service = new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, _mockProcFactory.Object);
            
            service.ForceLogMemoryStats();
            
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("[MEM-DIAG]"))), Times.Once());
            _mockProcess.Verify(p => p.Refresh(), Times.AtLeastOnce());
        }

        [Fact]
        public void ForceLogMemoryStats_WhenError_LogsError()
        {
            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Throws(new Exception("Fail"));
            using var service = new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, _mockProcFactory.Object);
            
            service.ForceLogMemoryStats();
            
            _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once());
        }

        [Fact]
        public async Task StartAndStop_Works()
        {
            using var service = new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, _mockProcFactory.Object);
            
            await service.StartAsync(CancellationToken.None);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("starting"))), Times.Once());
            
            await service.StopAsync(CancellationToken.None);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("stopping"))), Times.Once());
        }

        [Fact]
        public void Constructor_DefaultProcessFactory_Works()
        {
            using var service = new MemoryDiagnosticsService(_mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, null);
            Assert.NotNull(service);
        }

        [Fact]
        public async Task DiagnosticsLoop_WhenTickFails_LogsAndContinues()
        {
            // We can't easily mock PeriodicTimer, but we can mock the inner action 
            // and verify it's called multiple times even if it fails once.
            
            int callCount = 0;
            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Returns(() => {
                callCount++;
                if (callCount == 1) throw new Exception("Transient loop failure");
                return _mockProcess.Object;
            });

            // Use very short interval
            using var service = new MemoryDiagnosticsService(
                _mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, _mockProcFactory.Object,
                TimeSpan.FromMilliseconds(50));

            await service.StartAsync(CancellationToken.None);
            
            // Wait for 2-3 ticks
            await Task.Delay(250);
            
            await service.StopAsync(CancellationToken.None);

            // Verify error was logged once
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to log memory stats")), It.IsAny<Exception>()), Times.Once());
            // Verify loop continued and called stats again
            Assert.True(callCount > 1);
        }

        [Fact]
        public async Task StopAsync_WhenLoopActuallyRunning_CancelsAndWaits()
        {
             using var service = new MemoryDiagnosticsService(
                _mockOrch.Object, _mockIcon.Object, _mockSearch.Object, _mockLogger.Object, _mockProcFactory.Object,
                TimeSpan.FromSeconds(1));

             await service.StartAsync(CancellationToken.None);
             
             // Stop immediately
             var stopTask = service.StopAsync(CancellationToken.None);
             
             await stopTask; // Should complete quickly
        }
    }
}
