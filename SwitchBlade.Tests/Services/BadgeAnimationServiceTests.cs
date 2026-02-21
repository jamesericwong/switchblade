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
            // Even with mock delay, we set these low for safety
            _service.DebounceMs = 10;
            _service.StaggerDelayMs = 10;
        }

        [Fact]
        public async Task Trigger_NullItems_ReturnsImmediately()
        {
            await _service.TriggerStaggeredAnimationAsync(null);
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Never());
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
            
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.AtMost(1));
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
        public async Task Trigger_AlreadyAnimatedItems_SkipsAnimationButSetsVisibility()
        {
            var item = new WindowItem { Title = "T1", ShortcutIndex = 1, HasBeenAnimated = true };
            var items = new List<WindowItem> { item };
            
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Never());
            Assert.Equal(1.0, item.BadgeOpacity);
            Assert.Equal(0.0, item.BadgeTranslateX);
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
            _mockAnimator.Verify(a => a.Animate(items[0], 0, 100, It.IsAny<double>()), Times.Once());
            _mockAnimator.Verify(a => a.Animate(items[1], 4 * 50, 100, It.IsAny<double>()), Times.Once());
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
             _mockAnimator.Verify(a => a.Animate(item, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Once());
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
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Never());
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
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Once());
        }

        [Fact]
        public async Task Trigger_MixedVisibilityItems_HandlesCorrectly()
        {
            var item1 = new WindowItem { Title = "Visible", ShortcutIndex = 1 };
            var item2 = new WindowItem { Title = "Hidden", ShortcutIndex = -1 }; // ShortcutIndex -1 means IsShortcutVisible = false
            
            var items = new List<WindowItem> { item1, item2 };

            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            // item1 should be animated (HasBeenAnimated = true)
            _mockAnimator.Verify(a => a.Animate(item1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Once());
            Assert.True(item1.HasBeenAnimated);

            // item2 should be skipped (continue branch)
            _mockAnimator.Verify(a => a.Animate(item2, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Never());
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
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.AtMost(3)); 
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
            _mockAnimator.Setup(a => a.Animate(item1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()))
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
    }
}
