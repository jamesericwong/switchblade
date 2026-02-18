using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class UiaWorkerClientTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IProcessFactory> _mockProcFactory;
        private readonly Mock<IFileSystem> _mockFs;
        private readonly Mock<IProcess> _mockProcess;

        public UiaWorkerClientTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockProcFactory = new Mock<IProcessFactory>();
            _mockFs = new Mock<IFileSystem>();
            _mockProcess = new Mock<IProcess>();

            _mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockProcFactory.Setup(f => f.GetCurrentProcess()).Returns(new Mock<IProcess>().Object);
            _mockProcFactory.Setup(f => f.Start(It.IsAny<ProcessStartInfo>())).Returns(_mockProcess.Object);
            
            var mockIn = new Mock<TextWriter>();
            var mockOut = new Mock<TextReader>();
            _mockProcess.Setup(p => p.StandardInput).Returns(mockIn.Object);
            _mockProcess.Setup(p => p.StandardOutput).Returns(mockOut.Object);
        }

        [Fact]
        public async Task ScanAsync_WorkerMissing_ReturnsEmptyAndLogs()
        {
            _mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            var result = await client.ScanAsync();
            
            Assert.Empty(result);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Worker not found"))), Times.AtLeastOnce());
        }

        [Fact]
        public async Task ScanStreamingAsync_ProcessStartFails_LogsAndReturnsEmpty()
        {
            _mockProcFactory.Setup(f => f.Start(It.IsAny<ProcessStartInfo>())).Throws(new Exception("Fail"));
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            var results = new List<UiaPluginResult>();
            await foreach (var r in client.ScanStreamingAsync()) results.Add(r);
            
            Assert.Empty(results);
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to start")), It.IsAny<Exception>()), Times.Once());
        }

        [Fact]
        public async Task ScanAsync_SuccessFlow()
        {
            var jsonResponse = "{\"pluginName\": \"P1\", \"windows\": [{\"title\": \"W1\", \"hwnd\": 100}], \"isFinal\": false}\n{\"isFinal\": true}";
            var reader = new StringReader(jsonResponse);
            _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(ct => ValueTask.FromResult(reader.ReadLine()));

            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            var result = await client.ScanAsync();

            Assert.Single(result);
            Assert.Equal("W1", result[0].Title);
        }

        [Fact]
        public void Dispose_KillsActiveProcess()
        {
            // Setup HasExited to false so Kill is called
            _mockProcess.Setup(p => p.HasExited).Returns(false);
            
            // Setup ReadLineAsync to hang so process stays "active" in the client
            _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<string?>(new TaskCompletionSource<string?>().Task));

            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            var enumerator = client.ScanStreamingAsync().GetAsyncEnumerator();
            Task.Run(async () => await enumerator.MoveNextAsync());
            Thread.Sleep(100); 

            client.Dispose();
            
            _mockProcess.Verify(p => p.Kill(true), Times.AtLeastOnce());
        }

        [Fact]
        public async Task ScanAsync_WhenDisposed_Throws()
        {
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            client.Dispose();
            
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ScanAsync());
        }
    }
}
