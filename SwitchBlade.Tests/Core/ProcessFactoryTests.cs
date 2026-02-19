using System;
using System.Diagnostics;
using Moq;
using SwitchBlade.Core;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class ProcessFactoryTests
    {
        private readonly Mock<ISystemProcessProvider> _mockProvider;
        private readonly ProcessFactory _factory;

        public ProcessFactoryTests()
        {
            _mockProvider = new Mock<ISystemProcessProvider>();
            _factory = new ProcessFactory(_mockProvider.Object);
        }

        [Fact]
        public void Start_ReturnsWrappedProcess_WhenProviderReturnsProcess()
        {
            // Arrange
            var startInfo = new ProcessStartInfo("notepad.exe");
            var process = new Process();
            _mockProvider.Setup(p => p.Start(startInfo)).Returns(process);

            // Act
            var result = _factory.Start(startInfo);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void Start_ReturnsNull_WhenProviderReturnsNull()
        {
            // Arrange
            var startInfo = new ProcessStartInfo("notepad.exe");
            _mockProvider.Setup(p => p.Start(startInfo)).Returns((Process?)null);

            // Act
            var result = _factory.Start(startInfo);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCurrentProcess_ReturnsWrappedProcess()
        {
            // Arrange
            var process = new Process();
            _mockProvider.Setup(p => p.GetCurrentProcess()).Returns(process);

            // Act
            var result = _factory.GetCurrentProcess();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void ProcessPath_ReturnsPathFromProvider()
        {
            // Arrange
            var expectedPath = @"C:\Windows\System32\notepad.exe";
            _mockProvider.Setup(p => p.ProcessPath).Returns(expectedPath);

            // Act
            var result = _factory.ProcessPath;

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void DefaultConstructor_InitializesWithoutException()
        {
            // Act & Assert
            var factory = new ProcessFactory();
            Assert.NotNull(factory);
        }
    }
}
