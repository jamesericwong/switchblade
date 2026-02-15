using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SwitchBlade.Services;
using SwitchBlade.Core;
using SwitchBlade.Contracts;
using Moq;

namespace SwitchBlade.Tests.Services
{
    public class UiaWorkerClientTests
    {
        [Fact]
        public void Dispose_ShouldNotThrow_WhenProcessIsNotStarted()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            var client = new UiaWorkerClient(loggerMock.Object);

            // Act & Assert
            var exception = Record.Exception(() => client.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_ShouldNotThrow_WhenCalledMultipleTimes()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            var client = new UiaWorkerClient(loggerMock.Object);

            // Act & Assert
            client.Dispose();
            var exception = Record.Exception(() => client.Dispose());
            Assert.Null(exception);
        }
        
        // Note: functionality that involves actual process spawning is covered by integration verifications 
        // rather than unit tests to avoid flakiness and OS dependency in the CI/test runner.
    }
}
