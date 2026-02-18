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
        public async Task ScanAsync_CorruptJson_LogsAndContinues()
        {
            var jsonResponse = "INVALID_JSON_LINE\n{\"pluginName\": \"P1\", \"windows\": [], \"isFinal\": true}";
            var reader = new StringReader(jsonResponse);
            
            // Mock ReadLine so it returns invalid line then valid line
            var sequence = new Queue<string?>(new[] { "INVALID_JSON_LINE", "{\"pluginName\": \"P1\", \"windows\": [], \"isFinal\": true}", null });
            
            _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(ct => ValueTask.FromResult(sequence.Count > 0 ? sequence.Dequeue() : null));

            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            var result = await client.ScanAsync();
            
            // Should verify logging of error
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Failed to parse"))), Times.Once());
            // Should still finish successfully
            Assert.Empty(result); 
        }

        [Fact]
        public async Task ScanAsync_ProcessExitEarly_ReturnsPartialResults()
        {
            // First line valid, then null (process exit)
             var sequence = new Queue<string?>(new[] { "{\"pluginName\": \"P1\", \"windows\": [{\"title\": \"W1\"}], \"isFinal\": false}", null });
            
            _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(ct => ValueTask.FromResult(sequence.Count > 0 ? sequence.Dequeue() : null));

            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            var result = await client.ScanAsync();
            
            Assert.Single(result);
            Assert.Equal("W1", result[0].Title);
        }

        [Fact]
        public async Task ScanAsync_Timeout_CancelsAndKillsProcess()
        {
            // Setup immediate timeout
            var shortTimeout = TimeSpan.FromMilliseconds(10);
            var client = new UiaWorkerClient(_mockLogger.Object, shortTimeout, _mockProcFactory.Object, _mockFs.Object);

            // Mock ReadLine to hang forever
            _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async ct => 
                {
                    await Task.Delay(100, ct); // Wait longer than timeout
                    return "{}";
                });
            
            // Run scan
            await client.ScanAsync();
            
            // Verify cancellation log or process kill
            _mockProcess.Verify(p => p.Kill(true), Times.AtLeastOnce());
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("cancelled/timed out"))), Times.AtLeastOnce());
        }

        [Fact]
        public async Task Dispose_DuringActiveScan_SafelyShutsDown()
        {
             var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
             var cts = new CancellationTokenSource();
             
             // Mock ReadLine to signal when called, then hang
             var tcs = new TaskCompletionSource<string>();
             _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async ct => 
                {
                    // signal we are inside
                    tcs.TrySetResult("inside");
                    await Task.Delay(500, ct); // Hang until cancel
                    return null;
                });

             var scanTask = client.ScanAsync(cancellationToken: cts.Token);
             
             // Wait for scan to start
             await tcs.Task;
             
             // Dispose client
             client.Dispose();
             
             // Scan should finish (likely empty or partial)
             var result = await scanTask;
             
             _mockProcess.Verify(p => p.Kill(true), Times.AtLeastOnce());
        }

        [Fact]
        public async Task ScanStreamingAsync_ThrowsIfDisposed()
        {
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            client.Dispose();
            
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
            {
                await foreach (var _ in client.ScanStreamingAsync()) { }
            });
        }

        [Fact]
        public async Task ScanAsync_ThrowsIfDisposed()
        {
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            client.Dispose();
            
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ScanAsync());
        }

        [Fact]
        public async Task ScanStreamingAsync_HandlesStdinFailure()
        {
            // Mock Stdin to throw
            var mockStdin = new Mock<TextWriter>();
            mockStdin.Setup(w => w.WriteLineAsync(It.IsAny<string>())).ThrowsAsync(new Exception("Stdin Fail"));
            _mockProcess.Setup(p => p.StandardInput).Returns(mockStdin.Object);
            
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            var results = new List<UiaPluginResult>();
            await foreach (var r in client.ScanStreamingAsync()) results.Add(r);
            
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to send request")), It.IsAny<Exception>()), Times.Once());
        }

        [Fact]
        public async Task ScanStreamingAsync_HandlesErrorDataReceived()
        {
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            // Capture the event handler
            DataReceivedEventHandler? handler = null;
            _mockProcess.SetupAdd(p => p.ErrorDataReceived += It.IsAny<DataReceivedEventHandler>())
                .Callback<DataReceivedEventHandler>(h => handler = h);
                
             // Run scan in background
            var scanTask = client.ScanAsync();
            
            // Trigger error data
            if (handler != null)
            {
                var args = CreateDataReceivedEventArgs("Some Error Output");
                handler.Invoke(_mockProcess.Object, args);
            }
            
            // Finish scan
             _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null); // End stream
                
            await scanTask;
            
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Some Error Output"))), Times.AtLeastOnce());
        }

        [Fact]
        public void Dispose_HandlesKillException()
        {
            _mockProcess.Setup(p => p.HasExited).Returns(false);
            _mockProcess.Setup(p => p.Kill(It.IsAny<bool>())).Throws(new Exception("Kill Fail"));
            
            var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
            
            // Overwrite StandardInput mock to capture when input is written (signaling process is active)
            var inputWritten = new ManualResetEventSlim(false);
            var mockIn = new Mock<TextWriter>();
            mockIn.Setup(w => w.WriteLineAsync(It.IsAny<string>()))
                  .Callback(() => inputWritten.Set())
                  .Returns(Task.CompletedTask);
            _mockProcess.Setup(p => p.StandardInput).Returns(mockIn.Object);

             var enumerator = client.ScanStreamingAsync().GetAsyncEnumerator();
            Task.Run(async () => await enumerator.MoveNextAsync()); 
            
            // Wait for input to be written, ensuring _activeProcess is set
            Assert.True(inputWritten.Wait(2000), "Timed out waiting for process to become active");
             
             client.Dispose();
             
             _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Failed to kill active process"))), Times.Once());
        }
        
        [Fact]
        public async Task ScanAsync_HandlesGenericException()
        {
             _mockProcess.Setup(p => p.StandardOutput.ReadLineAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Generic Fail"));
                
             var client = new UiaWorkerClient(_mockLogger.Object, null, _mockProcFactory.Object, _mockFs.Object);
             var result = await client.ScanAsync();
             
             Assert.Empty(result);
             _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("ScanAsync failed mid-stream")), It.IsAny<Exception>()), Times.Once());
        }

        private static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
        {
            var ctor = typeof(DataReceivedEventArgs).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            Assert.NotNull(ctor);
            return (DataReceivedEventArgs)ctor.Invoke(new object[] { data });
        }
    }
}
