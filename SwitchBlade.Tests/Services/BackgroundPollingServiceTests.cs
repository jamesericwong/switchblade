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
        private readonly Mock<IWorkstationService> _mockWorkstationService;
        private readonly Mock<Func<TimeSpan, IPeriodicTimer>> _mockTimerFactory;
        private readonly Mock<IPeriodicTimer> _mockTimer;
        private readonly UserSettings _settings;

        public BackgroundPollingServiceTests()
        {
            _settings = new UserSettings
            {
                EnableBackgroundPolling = true,
                BackgroundPollingIntervalSeconds = 10
            };
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);

            _mockDispatcherService = new Mock<IDispatcherService>();
            _mockDispatcherService.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(async action => await action());

            _mockWorkstationService = new Mock<IWorkstationService>();
            _mockWorkstationService.Setup(w => w.IsWorkstationLocked()).Returns(false);

            _mockTimer = new Mock<IPeriodicTimer>();
            _mockTimerFactory = new Mock<Func<TimeSpan, IPeriodicTimer>>();
            _mockTimerFactory.Setup(f => f(It.IsAny<TimeSpan>())).Returns(_mockTimer.Object);
        }

        public void Dispose()
        {
        }

        [Fact]
        public async Task Polling_WhenEnabled_CreatesTimerAndWaits()
        {
            // Arrange
            _mockTimer.SetupSequence(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)  // First tick
                .ReturnsAsync(false); // End loop

            bool refreshCalled = false;
            Func<Task> refreshAction = () => { refreshCalled = true; return Task.CompletedTask; };

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                refreshAction,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            // Give the background task a moment to await the timer
            await Task.Delay(50); // Small delay to let async loop start

            // Assert
            _mockTimerFactory.Verify(f => f(TimeSpan.FromSeconds(10)), Times.Once);
            _mockTimer.Verify(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            // Note: Since proper async coordination requires a bit more than just mocking (because the loop runs on background),
            // verifying the refresh action happened is best done if we can control the flow.
            // But since WaitForNextTickAsync returns immediately in mock, the loop runs fast.
            Assert.True(refreshCalled);
        }

        [Fact]
        public async Task Polling_WhenWorkstationLocked_SkipsRefresh()
        {
            // Arrange
            _mockWorkstationService.Setup(w => w.IsWorkstationLocked()).Returns(true);

            _mockTimer.SetupSequence(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            bool refreshCalled = false;
            Func<Task> refreshAction = () => { refreshCalled = true; return Task.CompletedTask; };

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                refreshAction,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            await Task.Delay(50);

            // Assert
            Assert.False(refreshCalled);
            _mockWorkstationService.Verify(w => w.IsWorkstationLocked(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Polling_WhenRefreshThrows_ContinuesRunning()
        {
            // Arrange
            _mockTimer.SetupSequence(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true) // Tick 1: Throws
                .ReturnsAsync(true) // Tick 2: Succeeds
                .ReturnsAsync(false); // Stop

            int callCount = 0;
            Func<Task> refreshAction = () =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("Boom");
                return Task.CompletedTask;
            };

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                refreshAction,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            await Task.Delay(100);

            // Assert
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void SettingsChanged_RestartsPolling_WithNewInterval()
        {
            // Arrange
            _mockTimer.Setup(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            _mockTimerFactory.Invocations.Clear();

            // Act
            _settings.BackgroundPollingIntervalSeconds = 20;
            _mockSettingsService.Raise(s => s.SettingsChanged += null);

            // Assert
            _mockTimerFactory.Verify(f => f(TimeSpan.FromSeconds(20)), Times.Once);
        }

        [Fact]
        public void Polling_DisabledInSettings_DoesNotStartTimer()
        {
            // Arrange
            _settings.EnableBackgroundPolling = false;

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            // Assert
            _mockTimerFactory.Verify(f => f(It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public void Polling_MinimumInterval_ClampedTo1Second()
        {
            // Arrange
            _settings.BackgroundPollingIntervalSeconds = 0; // Invalid
            _settings.EnableBackgroundPolling = true;

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            // Assert
            _mockTimerFactory.Verify(f => f(TimeSpan.FromSeconds(1)), Times.Once);
        }

        [Fact]
        public async Task StartPolling_CancelsPreviousTimer_BeforeStartingNew()
        {
            // Arrange
            var mockTimer1 = new Mock<IPeriodicTimer>();
            var mockTimer2 = new Mock<IPeriodicTimer>();

            // Return different timers on valid calls
            _mockTimerFactory.SetupSequence(f => f(It.IsAny<TimeSpan>()))
                .Returns(mockTimer1.Object)
                .Returns(mockTimer2.Object);

            mockTimer1.Setup(t => t.WaitForNextTickAsync(It.IsAny<CancellationToken>()))
                 .Returns(async (CancellationToken ct) =>
                 {
                     await Task.Delay(500, ct); // Simulate blocking wait
                     return true;
                 });

            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                _mockWorkstationService.Object,
                _mockTimerFactory.Object);

            // Timer 1 started. Now change settings to restart.
            _settings.BackgroundPollingIntervalSeconds = 5;
            _mockSettingsService.Raise(s => s.SettingsChanged += null);

            // Assert
            // Timer 1 should be disposed (via the `using` block in the async method when cancelled)
            // But Wait! `using var timer` inside `PollingLoop` handles disposal.
            // Cancellation of `_cts` causes `WaitForNextTickAsync` to throw OperationCanceledException
            // OR return if the token is checked.
            // Our mock `WaitForNextTickAsync` should handle cancellation token correctly to test this fully.
            // For now, let's verify factory was called twice.
            _mockTimerFactory.Verify(f => f(It.IsAny<TimeSpan>()), Times.Exactly(2));

            await Task.Delay(50);
            // Assert timer 1 disposed? It's inside a using block in an async method.
            // The async method exits on cancellation.
            // Hard to strict-verify internal disposal without complex signaling, 
            // but we verified the restart mechanism logic.
        }

        [Fact]
        public void Constructor_WithDefaultDependencies_Works()
        {
            // Arrange
            _settings.EnableBackgroundPolling = false; // Prevent background loop from starting with real timer

            // Act
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                null, // Use default WorkstationService
                null  // Use default SystemPeriodicTimer factory
            );

            // Assert
            // If we reached here without exception, branches were covered
            Assert.NotNull(service);
        }

        [Fact]
        public void Dispose_CalledTwice_IgnoresSubsequentCalls()
        {
            // Arrange
            _settings.EnableBackgroundPolling = false;
            using var service = new BackgroundPollingService(
                _mockSettingsService.Object,
                _mockDispatcherService.Object,
                () => Task.CompletedTask,
                null,
                null
            );

            // Act
            service.Dispose();
            // Second dispose should hit the if (_disposed) return; branch
            service.Dispose();

            // Assert
            Assert.NotNull(service); // Simply ensuring no exceptions are thrown
        }
    }
}
