using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class MemoryDiagnosticsServiceTests : IDisposable
    {
        private readonly Mock<IWindowOrchestrationService> _mockOrchestration;
        private readonly Mock<IIconService> _mockIconService;
        private readonly Mock<IWindowSearchService> _mockSearchService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly MemoryDiagnosticsService _service;

        public MemoryDiagnosticsServiceTests()
        {
            _mockOrchestration = new Mock<IWindowOrchestrationService>();
            _mockIconService = new Mock<IIconService>();
            _mockSearchService = new Mock<IWindowSearchService>();
            _mockLogger = new Mock<ILogger>();

            _service = new MemoryDiagnosticsService(
                _mockOrchestration.Object,
                _mockIconService.Object,
                _mockSearchService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task StartAsync_LogsStartingMessage()
        {
            // Act
            await _service.StartAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(x => x.Log(It.Is<string>(s => s.Contains("starting"))), Times.Once);
        }

        [Fact]
        public async Task StopAsync_LogsStoppingMessage()
        {
            // Arrange
            await _service.StartAsync(CancellationToken.None);

            // Act
            await _service.StopAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(x => x.Log(It.Is<string>(s => s.Contains("stopping"))), Times.Once);
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => _service.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void ForceLogMemoryStats_LogsMemoryUsage()
        {
            // Arrange
            var mockProcess = new Mock<IProcess>();
            mockProcess.Setup(p => p.WorkingSet64).Returns(1024 * 1024 * 50); // 50MB
            mockProcess.Setup(p => p.PrivateMemorySize64).Returns(1024 * 1024 * 60); // 60MB
            mockProcess.Setup(p => p.HandleCount).Returns(123);
            mockProcess.Setup(p => p.ThreadCount).Returns(10);
            
            var mockProcessFactory = new Mock<IProcessFactory>();
            mockProcessFactory.Setup(f => f.GetCurrentProcess()).Returns(mockProcess.Object);
            
            // Re-create service with mock factory
            var service = new MemoryDiagnosticsService(
                _mockOrchestration.Object,
                _mockIconService.Object,
                _mockSearchService.Object,
                _mockLogger.Object,
                mockProcessFactory.Object);

            _mockOrchestration.Setup(o => o.CacheCount).Returns(5);
            _mockIconService.Setup(i => i.CacheCount).Returns(10);

            // Act
            service.ForceLogMemoryStats();

            // Assert
            _mockLogger.Verify(x => x.Log(It.Is<string>(s => 
                s.Contains("WorkingSet: 50 MB") && 
                s.Contains("Private: 60 MB") &&
                s.Contains("Handles: 123") &&
                s.Contains("Threads: 10"))), Times.Once);
        }

        public void Dispose()
        {
            _service.Dispose();
        }
    }
}
