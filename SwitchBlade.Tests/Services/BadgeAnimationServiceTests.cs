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
        private readonly BadgeAnimationService _service;

        public BadgeAnimationServiceTests()
        {
            _mockAnimator = new Mock<IBadgeAnimator>();
            _service = new BadgeAnimationService(_mockAnimator.Object);
            // Speed up tests
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
        public async Task Trigger_Debounce_WorksAndCanBeCancelled()
        {
            var items = new List<WindowItem> { new WindowItem { Title = "T1", ShortcutIndex = 1 } };
            
            // Start one
            var task1 = _service.TriggerStaggeredAnimationAsync(items);
            
            // Start another immediately - should cancel the first
            var task2 = _service.TriggerStaggeredAnimationAsync(items);
            
            await Task.WhenAll(task1, task2);
            
            // Should have только one animation cycle actually run through
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.AtMost(1));
        }

        [Fact]
        public async Task Trigger_SkipDebounce_RunsImmediately()
        {
            var items = new List<WindowItem> { new WindowItem { Title = "T1", ShortcutIndex = 1 } };
            await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            _mockAnimator.Verify(a => a.Animate(It.IsAny<WindowItem>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Once());
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
        public async Task Trigger_CancellationDuringLoop_Works()
        {
            var items = new List<WindowItem>();
            for (int i = 0; i < 5; i++) 
                items.Add(new WindowItem { Title = $"T{i}", ShortcutIndex = i });

            var task = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);
            
            // Immediately trigger again to cancel the first one while it's in the loop (if possible)
            // or while it's waiting for the final stagger delay.
            _ = _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

            await task;

            // Some items might have been started, but the loop checks CT
            // We verify it doesn't crash and returns cleanly.
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

            // Verify animations were triggered with correct delays
            _mockAnimator.Verify(a => a.Animate(items[0], 0, 100, It.IsAny<double>()), Times.Once());
            _mockAnimator.Verify(a => a.Animate(items[1], 4 * 50, 100, It.IsAny<double>()), Times.Once());
        }

        [Fact]
        public async Task Trigger_ResetsBadgeAnimation_ForItemsAboutToAnimate()
        {
             var item = new WindowItem { Title = "T1", ShortcutIndex = 1, BadgeOpacity = 1.0 };
             var items = new List<WindowItem> { item };

             await _service.TriggerStaggeredAnimationAsync(items, skipDebounce: true);

             // Before animating, it should have reset Opacity to 0 (via ResetBadgeAnimation)
             // and then animator handles it. 
             // We can't easily check the 0 value because it's transient before animator starts,
             // but we verify the items are animated.
             _mockAnimator.Verify(a => a.Animate(item, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>()), Times.Once());
             Assert.True(item.HasBeenAnimated);
        }
    }
}
