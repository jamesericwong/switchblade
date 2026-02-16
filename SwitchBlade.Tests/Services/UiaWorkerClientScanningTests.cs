using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using System.Text.Json;

namespace SwitchBlade.Tests.Services
{
    public class UiaWorkerClientScanningTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IProcessFactory> _processFactoryMock;
        private readonly Mock<IFileSystem> _fileSystemMock;
        private readonly Mock<IProcess> _processMock;
        private readonly Mock<System.IO.TextWriter> _stdinMock;
        private readonly Mock<System.IO.TextReader> _stdoutMock;
        private readonly Mock<System.IO.TextReader> _stderrMock;
        private readonly UiaWorkerClient _client;

        public UiaWorkerClientScanningTests()
        {
            _loggerMock = new Mock<ILogger>();
            _processFactoryMock = new Mock<IProcessFactory>();
            _fileSystemMock = new Mock<IFileSystem>();
            _processMock = new Mock<IProcess>();
            _stdinMock = new Mock<System.IO.TextWriter>();
            _stdoutMock = new Mock<System.IO.TextReader>();
            _stderrMock = new Mock<System.IO.TextReader>();

            // Setup Process Mock
            _processMock.Setup(p => p.Id).Returns(123);
            _processMock.Setup(p => p.StandardInput).Returns(_stdinMock.Object);
            _processMock.Setup(p => p.StandardOutput).Returns(_stdoutMock.Object);
            _processMock.Setup(p => p.StandardError).Returns(_stderrMock.Object);
            _processMock.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            // Stdin Write/Flush returns Task
            _stdinMock.Setup(w => w.WriteLineAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _stdinMock.Setup(w => w.FlushAsync()).Returns(Task.CompletedTask);

            // Setup Factory
            _processFactoryMock.Setup(f => f.Start(It.IsAny<ProcessStartInfo>()))
                               .Returns(_processMock.Object);
            
            var currentProcessMock = new Mock<IProcess>();
            currentProcessMock.Setup(p => p.Id).Returns(1);
            _processFactoryMock.Setup(f => f.GetCurrentProcess())
                               .Returns(currentProcessMock.Object);

            // Setup FileSystem
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);

            _client = new UiaWorkerClient(
                _loggerMock.Object, 
                TimeSpan.FromSeconds(5), 
                _processFactoryMock.Object, 
                _fileSystemMock.Object);
        }

        [Fact]
        public async Task ScanStreamingAsync_StartsProcessAndSendsRequest()
        {
            // Arrange
            _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .Returns(ValueTask.FromResult<string?>(null)); // Immediately ends

            // Act
            await foreach (var _ in _client.ScanStreamingAsync()) { }

            // Assert
            _processFactoryMock.Verify(f => f.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
            _stdinMock.Verify(w => w.WriteLineAsync(It.Is<string>(s => s.Contains("scan"))), Times.Once);
        }

        [Fact]
        public async Task ScanStreamingAsync_YieldsResultsFromStdout()
        {
            // Arrange
            var result1 = new SwitchBlade.Contracts.UiaPluginResult 
            { 
                PluginName = "TestPlugin", 
                Windows = new List<SwitchBlade.Contracts.UiaWindowResult> 
                { 
                    new SwitchBlade.Contracts.UiaWindowResult { Title = "Test Window", Hwnd = 12345 } 
                } 
            };
            var json1 = JsonSerializer.Serialize(result1, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

             _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .Returns(ValueTask.FromResult<string?>(json1))
                       .Returns(ValueTask.FromResult<string?>(null));

            // Act
            var results = new List<SwitchBlade.Contracts.UiaPluginResult>();
            await foreach (var res in _client.ScanStreamingAsync())
            {
                results.Add(res);
            }

            // Assert
            Assert.Single(results);
            Assert.Equal("TestPlugin", results[0].PluginName);
            Assert.Single(results[0].Windows!);
            Assert.Equal("Test Window", results[0].Windows![0].Title);
        }

        [Fact]
        public async Task ScanStreamingAsync_HandlesJsonErrors_Gracefully()
        {
            // Arrange
             _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .Returns(ValueTask.FromResult<string?>("INVALID JSON"))
                       .Returns(ValueTask.FromResult<string?>(null));

            // Act
            var results = new List<SwitchBlade.Contracts.UiaPluginResult>();
            await foreach (var res in _client.ScanStreamingAsync())
            {
                results.Add(res);
            }

            // Assert
            Assert.Empty(results);
            _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Failed to parse"))), Times.Once);
        }

        [Fact]
        public async Task ScanStreamingAsync_StopsOnFinalMarker()
        {
            // Arrange
            var result1 = new SwitchBlade.Contracts.UiaPluginResult { PluginName = "P1" };
            var final = new SwitchBlade.Contracts.UiaPluginResult { IsFinal = true };
            
            var json1 = JsonSerializer.Serialize(result1, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var jsonFinal = JsonSerializer.Serialize(final, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

             _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .Returns(ValueTask.FromResult<string?>(json1))
                       .Returns(ValueTask.FromResult<string?>(jsonFinal))
                       .Returns(ValueTask.FromResult<string?>("Should Not Be Read")); // Should stop before this

            // Act
            var results = new List<SwitchBlade.Contracts.UiaPluginResult>();
            await foreach (var res in _client.ScanStreamingAsync())
            {
                results.Add(res);
            }

            // Assert
            Assert.Single(results); // Final marker is not yielded
            Assert.Equal("P1", results[0].PluginName);
        }

        [Fact]
        public async Task Dispose_KillsProcess()
        {
            // Arrange
            // Use a mock that responds to cancellation to avoid hanging
            _stdoutMock.Setup(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .Returns(async (CancellationToken ct) => 
                       {
                           await Task.Delay(-1, ct);
                           return null;
                       });
            
            _processMock.Setup(p => p.HasExited).Returns(false);

            // Act
            var enumerator = _client.ScanStreamingAsync().GetAsyncEnumerator();
            
            var moveNextTask = enumerator.MoveNextAsync();
            
            await Task.Delay(50);
            
            _client.Dispose(); // Kills process and cancels token
            
            try { await moveNextTask; } catch { }

            // Assert
            _processMock.Verify(p => p.Kill(true), Times.AtLeastOnce());
        }

        [Fact]
        public async Task ScanAsync_CollectsAllResults()
        {
            // Arrange
            var results = new System.Collections.Generic.List<UiaPluginResult>
            {
                new UiaPluginResult 
                { 
                    PluginName = "Plugin1", 
                    Windows = new System.Collections.Generic.List<UiaWindowResult> 
                    { 
                        new UiaWindowResult { Title = "Window1", Hwnd = 123 } 
                    } 
                },
                new UiaPluginResult 
                { 
                    PluginName = "Plugin2", 
                    Windows = new System.Collections.Generic.List<UiaWindowResult> 
                    { 
                        new UiaWindowResult { Title = "Window2", Hwnd = 456 } 
                    } 
                }
            };
            
            // We need to mock ScanStreamingAsync behavior. 
            // Since we can't easily mock yield return with Moq on the same class, 
            // we will test through the real ScanAsync which calls the real ScanStreamingAsync.
            // But we already have mocks for the process.
            
            _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync("{\"pluginName\":\"P1\",\"windows\":[{\"title\":\"W1\",\"hwnd\":1}]}")
                       .ReturnsAsync("{\"pluginName\":\"P2\",\"windows\":[{\"title\":\"W2\",\"hwnd\":2}]}")
                       .ReturnsAsync("{\"isFinal\":true}")
                       .ReturnsAsync((string?)null);

            // Act
            var windows = await _client.ScanAsync();

            // Assert
            Assert.Equal(2, windows.Count);
            Assert.Equal("W1", windows[0].Title);
            Assert.Equal("W2", windows[1].Title);
        }

        [Fact]
        public async Task ScanAsync_WithPluginError_StillReturnsOtherResults()
        {
            // Arrange
            _stdoutMock.SetupSequence(r => r.ReadLineAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync("{\"pluginName\":\"P1\",\"error\":\"Something went wrong\"}")
                       .ReturnsAsync("{\"pluginName\":\"P2\",\"windows\":[{\"title\":\"W2\",\"hwnd\":2}]}")
                       .ReturnsAsync("{\"isFinal\":true}")
                       .ReturnsAsync((string?)null);

            // Act
            var windows = await _client.ScanAsync();

            // Assert
            Assert.Single(windows);
            Assert.Equal("W2", windows[0].Title);
        }
    }
}
