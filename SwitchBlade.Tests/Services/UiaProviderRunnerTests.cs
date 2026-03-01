using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class UiaProviderRunnerTests : IDisposable
    {
        private readonly Mock<IUiaWorkerClient> _mockClient;
        private readonly Mock<ILogger> _mockLogger;
        private readonly UiaProviderRunner _runner;

        public UiaProviderRunnerTests()
        {
            _mockClient = new Mock<IUiaWorkerClient>();
            _mockLogger = new Mock<ILogger>();
            _runner = new UiaProviderRunner(_mockClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_NullClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UiaProviderRunner(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_Works()
        {
            var noLoggerRunner = new UiaProviderRunner(_mockClient.Object, null);
            Assert.NotNull(noLoggerRunner);
            noLoggerRunner.Dispose();
        }

        [Fact]
        public async Task RunAsync_SkipsIfAlreadyRunning()
        {
            var field = typeof(UiaProviderRunner).GetField("_uiaRefreshLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var semaphore = (SemaphoreSlim)field!.GetValue(_runner)!;
            await semaphore.WaitAsync();

            try
            {
                await _runner.RunAsync(new List<IWindowProvider>(), new HashSet<string>(), new HashSet<string>(), (p, items) => { });
                _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("skipped"))), Times.Once);
            }
            finally
            {
                semaphore.Release();
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async IAsyncEnumerable<UiaPluginResult> GetResults(params UiaPluginResult[] results)
        {
            foreach (var r in results)
            {
                yield return r;
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        [Fact]
        public async Task RunAsync_SuccessfulStream_MapsByPluginName()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("P1");
            mockProvider.Setup(p => p.GetHandledProcesses()).Returns(Array.Empty<string>());

            var tcs = new TaskCompletionSource<bool>();
            var results = new[] {
                new UiaPluginResult { PluginName = "P1", Windows = new List<UiaWindowResult> { new UiaWindowResult { Title = "W1", Hwnd = 123 } }, Error = "Some error" }
            };

            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Returns(GetResults(results));

            await _runner.RunAsync(new[] { mockProvider.Object }, new HashSet<string>(), new HashSet<string>(), (p, items) =>
            {
                Assert.Same(mockProvider.Object, p);
                Assert.Single(items);
                Assert.Equal("W1", items[0].Title);
                tcs.SetResult(true);
            });

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(tcs.Task.IsCompletedSuccessfully);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("error: Some error"))), Times.Once);
        }

        [Fact]
        public async Task RunAsync_FallbackByProcessName()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("P1"); // different from result PluginName
            mockProvider.Setup(p => p.GetHandledProcesses()).Returns(new[] { "chrome.exe" });

            var tcs = new TaskCompletionSource<bool>();
            var results = new[] {
                new UiaPluginResult { PluginName = "UnknownPlugin", Windows = new List<UiaWindowResult> { new UiaWindowResult { ProcessName = "chrome.exe" } } }
            };

            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Returns(GetResults(results));

            await _runner.RunAsync(new[] { mockProvider.Object }, new HashSet<string>(), new HashSet<string>(), (p, items) =>
            {
                Assert.Same(mockProvider.Object, p);
                tcs.SetResult(true);
            });

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(tcs.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task RunAsync_ProviderNotFound_Skips()
        {
            var results = new[] {
                new UiaPluginResult { PluginName = "UnknownPlugin", Windows = new List<UiaWindowResult> { new UiaWindowResult { ProcessName = "unknown.exe" } } }
            };
            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Returns(GetResults(results));

            await _runner.RunAsync(new List<IWindowProvider>(), new HashSet<string>(), new HashSet<string>(), (p, items) => { });

            await WaitForBackgroundTaskAsync(_runner);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("No provider found"))), Times.Once);
        }

        [Fact]
        public async Task RunAsync_NullWindows_Handled()
        {
            var mockProvider = new Mock<IWindowProvider>();
            mockProvider.Setup(p => p.PluginName).Returns("P1");
            mockProvider.Setup(p => p.GetHandledProcesses()).Returns(Array.Empty<string>());

            var tcs = new TaskCompletionSource<bool>();
            var results = new[] {
                new UiaPluginResult { PluginName = "P1", Windows = null }
            };

            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Returns(GetResults(results));

            await _runner.RunAsync(new[] { mockProvider.Object }, new HashSet<string>(), new HashSet<string>(), (p, items) =>
            {
                Assert.Empty(items);
                tcs.SetResult(true);
            });

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(tcs.Task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task RunAsync_ExceptionInStream_Logged()
        {
            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Throws(new Exception("Stream failure"));

            await _runner.RunAsync(new List<IWindowProvider>(), new HashSet<string>(), new HashSet<string>(), (p, items) => { });

            await WaitForBackgroundTaskAsync(_runner);
            _mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("UIA Worker streaming error")), It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_NullLogger_Exception_Handled()
        {
            var noLoggerRunner = new UiaProviderRunner(_mockClient.Object, null);
            _mockClient.Setup(c => c.ScanStreamingAsync(It.IsAny<ISet<string>>(), It.IsAny<ISet<string>>(), default))
                       .Throws(new Exception("Stream failure"));

            await noLoggerRunner.RunAsync(new List<IWindowProvider>(), new HashSet<string>(), new HashSet<string>(), (p, items) => { });

            await WaitForBackgroundTaskAsync(noLoggerRunner);
            noLoggerRunner.Dispose();
        }

        public void Dispose()
        {
            _runner.Dispose();
            // Call again to test if (_disposed) return;
            _runner.Dispose();
        }

        private async Task WaitForBackgroundTaskAsync(UiaProviderRunner runner)
        {
            var field = typeof(UiaProviderRunner).GetField("_uiaRefreshLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var semaphore = (SemaphoreSlim)field!.GetValue(runner)!;
            await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
            semaphore.Release();
        }
    }
}
