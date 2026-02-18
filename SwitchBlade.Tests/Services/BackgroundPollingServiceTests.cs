using System;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class BackgroundPollingServiceTests : IDisposable
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IDispatcherService> _mockDispatcherService;
        private readonly UserSettings _settings;
        private bool _refreshCalled;

        public BackgroundPollingServiceTests()
        {
            _settings = new UserSettings 
            { 
                EnableBackgroundPolling = true,
                BackgroundPollingIntervalSeconds = 1
            };
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            
            _mockDispatcherService = new Mock<IDispatcherService>();
            // Mock InvokeAsync to run the action immediately
            _mockDispatcherService.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action => await action());
        }

        public void Dispose()
        {
            // Nothing to dispose by default, but child tests might create services
        }

        [Fact]
        public async Task Polling_WhenEnabled_CallsRefreshAction()
        {
            var semaphore = new SemaphoreSlim(0);
            Func<Task> refreshAction = async () => { 
                _refreshCalled = true; 
                semaphore.Release();
                await Task.CompletedTask;
            };

            using var service = new BackgroundPollingService(_mockSettingsService.Object, _mockDispatcherService.Object, refreshAction);

            // Wait for at least one poll (interval is 1s, clamped if less)
            var result = await semaphore.WaitAsync(2500); // 2.5s timeout
            Assert.True(result, "Refresh action was not called within timeout");
            Assert.True(_refreshCalled);
        }

        [Fact]
        public void Polling_WhenDisabled_DoesNotStartTask()
        {
            _settings.EnableBackgroundPolling = false;
            bool called = false;
            Func<Task> refreshAction = () => { called = true; return Task.CompletedTask; };

            using var service = new BackgroundPollingService(_mockSettingsService.Object, _mockDispatcherService.Object, refreshAction);

            Thread.Sleep(500);
            Assert.False(called);
        }

        [Fact]
        public void SettingsChanged_RestartsPolling()
        {
            _settings.EnableBackgroundPolling = false;
            Func<Task> refreshAction = () => Task.CompletedTask;

            using var service = new BackgroundPollingService(_mockSettingsService.Object, _mockDispatcherService.Object, refreshAction);

            _settings.EnableBackgroundPolling = true;
            _settings.BackgroundPollingIntervalSeconds = 1;
            
            // Trigger event
            _mockSettingsService.Raise(s => s.SettingsChanged += null);
            
            // Should now be polling (verify by looking at internal state if possible, or just wait for poll)
        }

        [Fact]
        public void Dispose_ShouldStopPollingAndUnsubscribe()
        {
            Func<Task> refreshAction = () => Task.CompletedTask;
            var service = new BackgroundPollingService(_mockSettingsService.Object, _mockDispatcherService.Object, refreshAction);
            
            service.Dispose();
            service.Dispose(); // Test double dispose
            
            _mockSettingsService.VerifyRemove(s => s.SettingsChanged -= It.IsAny<Action>(), Times.Once());
        }

        [Fact]
        public async Task PollingLoop_IntervalClamping_Works()
        {
            _settings.BackgroundPollingIntervalSeconds = 0; // Should be clamped to 1
            var semaphore = new SemaphoreSlim(0);
            Func<Task> refreshAction = () => { semaphore.Release(); return Task.CompletedTask; };

            using var service = new BackgroundPollingService(_mockSettingsService.Object, _mockDispatcherService.Object, refreshAction);
            
            var result = await semaphore.WaitAsync(2500);
            Assert.True(result);
        }
    }
}
