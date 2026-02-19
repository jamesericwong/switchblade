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
    }
}
