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

        public void Dispose()
        {
            _service.Dispose();
        }
    }
}
