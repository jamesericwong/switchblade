using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using Moq;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class BadgeAnimationServiceTests
    {
        private readonly Mock<IBadgeAnimator> _mockAnimator;
        private readonly Mock<IDelayProvider> _mockDelayProvider;
        private readonly BadgeAnimationService _service;

        public BadgeAnimationServiceTests()
        {
            _mockAnimator = new Mock<IBadgeAnimator>();
            _mockDelayProvider = new Mock<IDelayProvider>();
            
            // Mock delay to complete immediately by default
            _mockDelayProvider.Setup(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _service = new BadgeAnimationService(_mockAnimator.Object, _mockDelayProvider.Object);

            // Simulate the real animator: applying an entry animation settles the badge (fully visible), exactly as
            // the production Completed handler does. Without this, a fake that never settles would make every later
            // trigger re-animate "interrupted" items forever — which is the corrected behavior, not a bug.
            _mockAnimator.Setup(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Callback<WindowItem, int, int, double, CancellationToken>((item, _, _, _, _) => item.BadgeOpacity = 1.0);

            // Even with mock delay, we set these low for safety
            _service.DebounceMs = 10;
            _service.StaggerDelayMs = 10;
        }

        [Fact]
        public async Task Trigger_NullItems_ReturnsImmediately()
        {
            await _service.TriggerStaggeredAnimationAsync(null);
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Trigger_Debounce_DelaysExecution()
        {
            var items = new List<WindowItem> { new WindowItem { Title = "T1", ShortcutIndex = 1 } };
            
            // Verify delay is called
            await _service.TriggerStaggeredAnimationAsync(items);
            
            _mockDelayProvider.Verify(d => d.Delay(10, It.IsAny<CancellationToken>()), Times.Once); // Debounce
            // And final wait
            _mockDelayProvider.Verify(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task Trigger_SkipDebounce_SkipsFirstDelay()
        {
            var items = new List<WindowItem> { new WindowItem { Title = "T1", ShortcutIndex = 1 } };
            
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            // Should NOT call delay for debounce (10ms), but WILL call delay for final wait
            _mockDelayProvider.Verify(d => d.Delay(10, It.IsAny<CancellationToken>()), Times.Never);
            _mockDelayProvider.Verify(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once); // Only final wait
        }

        [Fact]
        public async Task Trigger_DebounceCancellation_Works()
        {
            var items = new List<WindowItem> { new WindowItem { Title = "T1", ShortcutIndex = 1 } };
            
            // Setup delay to hold execution so we can overlap
            var tcs = new TaskCompletionSource();
            _mockDelayProvider.Setup(d => d.Delay(10, It.IsAny<CancellationToken>()))
                .Returns(async (int ms, CancellationToken ct) => {
                    await tcs.Task; // Wait for manual release
                    ct.ThrowIfCancellationRequested();
                });

            // Start first triggers
            var task1 = _service.TriggerStaggeredAnimationAsync(items);
            
            // Start second trigger immediately - should cancel the first
            // We need to ensure the first one has entered the delay.
            // But since this is single-threaded test without real strict concurrency control on `tcs`, 
            // the second call will cancel the CTS of the first.
            
            // Release the delay NOW
            tcs.SetResult();
            
            var task2 = _service.TriggerStaggeredAnimationAsync(items);

            try 
            {
                await task1;
            }
            catch (OperationCanceledException) { } // it might throw or just return
            
            await task2;
            
            // Should have cancelled the first one inside the delay, or before animation
            // Verification is tricky with loose mocks, but we expect only 1 animation cycle
            // actually validating distinct cancellation is hard without checking token
            
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.AtMost(1));
        }

        [Fact]
        public void ResetAnimationState_ResetsFlag()
        {
            var items = new List<WindowItem> { new WindowItem { HasBeenAnimated = true } };
            _service.ResetAnimationState(items);
            Assert.False(items[0].HasBeenAnimated);

            _service.ResetAnimationState(null); // Should not throw
        }

        [Fact]
        public async Task Trigger_EntryPendingBadge_IsLeftUndisturbed()
        {
            // Regression: re-hiding or re-delaying a badge whose entry animation is still running made rapid trigger
            // passes read as flash and lag. In-flight entries must finish undisturbed via their own completion handler.
            var item = new WindowItem { Title = "in flight", ShortcutIndex = 2, HasBeenAnimated = true, EntryPending = true };

            await _service.TriggerStaggeredAnimationAsync([item], skipDebounce: true);

            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Trigger_JoiningLiveCascade_UsesCappedDelay()
        {
            // Regression: joiners into an already-running wave waited for their nominal stagger slot (long gone) and
            // read as lag. Their entry delay must be capped while other badges are still settling.
            _service.StaggerDelayMs = 50;

            var inFlight = new WindowItem { Title = "settling", ShortcutIndex = 1, HasBeenAnimated = true, EntryPending = true };
            var joiner = new WindowItem { Title = "late tab", ShortcutIndex = 4 };

            await _service.TriggerStaggeredAnimationAsync([inFlight, joiner], skipDebounce: true);

            // Nominal slot for index 4 is 200ms; capped to two stagger steps (100ms) because the wave is live.
            _mockAnimator.Verify(a => a.Animate(joiner, 2 * 50, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
            _mockAnimator.Verify(a => a.Animate(inFlight, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Trigger_AlreadyAnimatedItems_SkipsAnimationButSetsVisibility()
        {
            var item = new WindowItem { Title = "T1", ShortcutIndex = 1, HasBeenAnimated = true, BadgeOpacity = 1.0 };
            var items = new List<WindowItem> { item };
            
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
            Assert.Equal(1.0, item.BadgeOpacity);
            Assert.Equal(0.0, item.BadgeTranslateX);
        }

        [Fact]
        public async Task Trigger_AnimatedButHiddenBadge_ReAnimatesInsteadOfPoppingIn()
        {
            // Regression: a badge marked animated but left hidden by a superseded cycle used to pop in instantly on
            // the next trigger. Rapid toggles must still see the stagger, not skip it.
            var item = new WindowItem { Title = "interrupted", ShortcutIndex = 2, HasBeenAnimated = true, BadgeOpacity = 0.0 };

            await _service.TriggerStaggeredAnimationAsync([item], skipDebounce: true);

            _mockAnimator.Verify(a => a.Animate(item, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Trigger_StaggerDelay_CalculatesCorrectly()
        {
            _service.StaggerDelayMs = 50;
            _service.AnimationDurationMs = 100;
            
            var items = new List<WindowItem> 
            { 
                new WindowItem { Title = "T1", ShortcutIndex = 0 }, 
                new WindowItem { Title = "T2", ShortcutIndex = 4 } 
            };

            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            // Verify animations were triggered with correct delays passed to Animator
            _mockAnimator.Verify(a => a.Animate(items[0], 0, 100, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
            _mockAnimator.Verify(a => a.Animate(items[1], 4 * 50, 100, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Trigger_WaitsForCompletion()
        {
             _service.StaggerDelayMs = 10;
             _service.AnimationDurationMs = 100;
             var items = new List<WindowItem> { new WindowItem { ShortcutIndex = 1 } };
             
             await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

             // Expected max delay: (1+1)*10 + 100 = 120
             _mockDelayProvider.Verify(d => d.Delay(120, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Trigger_ResetsBadgeAnimation_ForItemsAboutToAnimate()
        {
             var item = new WindowItem { Title = "T1", ShortcutIndex = 1, BadgeOpacity = 1.0 };
             var items = new List<WindowItem> { item };

             await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

             // Before animating, it should have reset Opacity to 0 (via ResetBadgeAnimation)
             // and then animator handles it. 
             _mockAnimator.Verify(a => a.Animate(item, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
             Assert.True(item.HasBeenAnimated);
        }

        [Fact]
        public void Constructor_Throws_WhenAnimatorNull()
        {
            Assert.Throws<ArgumentNullException>(() => new BadgeAnimationService(null!));
        }

        [Fact]
        public void Constructor_UsesDefaultDelayProvider_WhenNull()
        {
            // Act
            var service = new BadgeAnimationService(_mockAnimator.Object, null);
            
            // Assert
            Assert.NotNull(service);
            // We can't easily check the private field _delayProvider without reflection,
            // but the fact it doesn't throw and initializes is the primary branch.
        }

        [Fact]
        public async Task Trigger_CancellationDuringDebounce_ReturnsImmediately()
        {
            var items = new List<WindowItem> { new WindowItem { ShortcutIndex = 1 } };
            
            // Mock delay to throw OCE
            _mockDelayProvider.Setup(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: false);

            // Assert
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task Trigger_CancellationDuringFinalDelay_HandlesGracefully()
        {
            var items = new List<WindowItem> { new WindowItem { ShortcutIndex = 1 } };
            
            _mockDelayProvider.SetupSequence(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask) // Debounce succeeds
                .ThrowsAsync(new OperationCanceledException()); // Final delay fails

            // Act
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: false);

            // Assert
            // Animation should have still been triggered
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Trigger_MixedVisibilityItems_HandlesCorrectly()
        {
            var item1 = new WindowItem { Title = "Visible", ShortcutIndex = 1 };
            var item2 = new WindowItem { Title = "Hidden", ShortcutIndex = -1 }; // ShortcutIndex -1 means IsShortcutVisible = false
            
            var items = new List<WindowItem> { item1, item2 };

            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            // item1 should be animated (HasBeenAnimated = true)
            _mockAnimator.Verify(a => a.Animate(item1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once());
            Assert.True(item1.HasBeenAnimated);

            // item2 should be skipped (continue branch)
            _mockAnimator.Verify(a => a.Animate(item2, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never());
            Assert.False(item2.HasBeenAnimated);
        }

        [Fact]
        public async Task Trigger_CancellationInsideLoop_ExitsEarly()
        {
            var items = new List<WindowItem> 
            { 
                new WindowItem { ShortcutIndex = 1 },
                new WindowItem { ShortcutIndex = 2 }
            };

            // First call to start a cycle
            var task1 = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            // Second call immediately cancels the first CTS
            var task2 = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            await Task.WhenAll(task1, task2);

            // One of them should have been cancelled before completing all items
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.AtMost(3)); 
            }

        [Fact]
        public async Task Trigger_CancellationVisibleAfterDebounce_HitsBranch()
        {
            var items = new List<WindowItem> { new WindowItem { ShortcutIndex = 1 } };
            
            var tcs = new TaskCompletionSource();
            _mockDelayProvider.Setup(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            // Start task1 - it will wait on the mock delay
            var task1 = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: false);
            
            // Start task2 - this cancels task1's CTS
            var task2 = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            // Resolve task1's delay WITHOUT throwing OCE
            // This allows task1 to proceed to line 109 where it checks the token
            tcs.SetResult(); 
            
            await task1;
            await task2;

            // task1 should have returned at line 109, so animate was never called for it by its own execution
            // (task2 might have called it though)
            // But we primarily care that line 109 was hit with true.
        }

        [Fact]
        public async Task Trigger_CancellationVisibleInsideLoop_HitsBranch()
        {
            var item1 = new WindowItem { ShortcutIndex = 1 };
            var item2 = new WindowItem { ShortcutIndex = 2 };
            var items = new List<WindowItem> { item1, item2 };
            
            int callCount = 0;
            // Mock animator to trigger cancellation of task1 when item1 is "animated"
            _mockAnimator.Setup(a => a.Animate(item1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Callback(() => {
                    // Only trigger cancellation once to avoid infinite recursion
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        // Start task2 to cancel task1
                        _ = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
                    }
                });

            // Act
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            // This should hit line 117 after item1 but before item2
        }

        [Fact]
        public async Task Trigger_SupersededCycle_AnimatorReceivesCancelledToken()
        {
            // C contract: a superseded trigger cycle must hand its animator a token that is cancelled, so the
            // animator stops before touching badges it never started. Regression test for overlapping cascades.
            var item = new WindowItem { ShortcutIndex = 1 };
            var items = new List<WindowItem> { item };

            CancellationToken firstCycleToken = default;
            bool recorded = false;
            _mockAnimator.Setup(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Callback<WindowItem, int, int, double, CancellationToken>((_, _, _, _, ct) =>
                {
                    if (!recorded)
                    {
                        firstCycleToken = ct;
                        recorded = true;
                    }
                });

            // Cycle 1: skipDebounce, so its only delay is the final wait — hold it pending.
            var tcs = new TaskCompletionSource();
            _mockDelayProvider.SetupSequence(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task)          // cycle 1's final wait: held
                .Returns(Task.CompletedTask); // cycle 2's final wait: completes

            var task1 = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            Assert.True(recorded);
            Assert.False(firstCycleToken.IsCancellationRequested);

            // Cycle 2 supersedes cycle 1 -> cancels its CTS.
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            Assert.True(firstCycleToken.IsCancellationRequested, "cycle-1 token must be cancelled once cycle 2 supersedes it");

            tcs.SetResult();
            await task1; // completes gracefully (OCE path is handled inside the service)
        }

        [Fact]
        public async Task ResetAnimationState_WithLogger_LogsReset()
        {
            var mockDelay = new Mock<IDelayProvider>();
            mockDelay.Setup(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var mockLogger = new Mock<ILogger>();
            var service = new BadgeAnimationService(_mockAnimator.Object, mockDelay.Object, mockLogger.Object);
            service.DebounceMs = 10;
            service.StaggerDelayMs = 10;

            var item = new WindowItem { Title = "T1", ShortcutIndex = 1 };
            await service.TriggerStaggeredAnimationAsync(new List<WindowItem> { item }, skipDebounce: true); // leaves the item animated

            Assert.Null(Record.Exception(() => service.ResetAnimationState(new List<WindowItem> { item })));

            mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("HasBeenAnimated"))), Times.Once());
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_WithLogger_LogsStartAndSummary()
        {
            var mockDelay = new Mock<IDelayProvider>();
            mockDelay.Setup(d => d.Delay(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var mockLogger = new Mock<ILogger>();
            var service = new BadgeAnimationService(_mockAnimator.Object, mockDelay.Object, mockLogger.Object);
            service.DebounceMs = 10;
            service.StaggerDelayMs = 10;

            var items = new List<WindowItem>
            {
                new WindowItem { Title = "A", ShortcutIndex = 1 },
                new WindowItem { Title = "B", ShortcutIndex = 2 }
            };

            await service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Starting"))), Times.Once());
            mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Animated="))), Times.Once());
        }
    }
}
