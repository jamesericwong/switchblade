using Xunit;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SwitchBlade.Tests.Services
{
    public class BadgeAnimationServiceTests
    {
        // Simple manual mock for IBadgeAnimator to verify calls
        public class MockBadgeAnimator : IBadgeAnimator
        {
            public List<(WindowItem Item, int Delay, int Duration, double Offset)> AnimatedCalls { get; } = new();

            public void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX)
            {
                AnimatedCalls.Add((item, delayMs, durationMs, startingOffsetX));
            }
        }

        [Fact]
        public void BadgeAnimationService_DefaultProperties_AreSet()
        {
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);

            Assert.Equal(150, service.AnimationDurationMs);
            Assert.Equal(75, service.StaggerDelayMs);
            Assert.Equal(-20, service.StartingOffsetX);
        }

        [Fact]
        public void ResetAnimationState_ResetsItemHasBeenAnimatedFlag()
        {
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);
            var item = new WindowItem { HasBeenAnimated = true }; // Simulate already animated
            var items = new List<WindowItem> { item };

            // Act
            service.ResetAnimationState(items);

            // Assert
            Assert.False(item.HasBeenAnimated);
        }

        [Fact]
        public void ResetAnimationState_HandlesNullOrEmptyListGracefully()
        {
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);

            // Should not throw
            service.ResetAnimationState((IEnumerable<WindowItem>?)null);
            service.ResetAnimationState(new List<WindowItem>());
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SetsHasBeenAnimated_True_And_CallsAnimator()
        {
            // Arrange
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator) { AnimationDurationMs = 100, StaggerDelayMs = 50, StartingOffsetX = -15 };
            var item = new WindowItem { ShortcutIndex = 0, HasBeenAnimated = false };
            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.True(item.HasBeenAnimated, "Should mark item as animated");
            
            // Verify Animator was called with correct parameters
            Assert.Single(animator.AnimatedCalls);
            var call = animator.AnimatedCalls[0];
            Assert.Same(item, call.Item);
            Assert.Equal(0, call.Delay); // Index 0 * 50ms = 0
            Assert.Equal(100, call.Duration);
            Assert.Equal(-15, call.Offset);
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_AppliesStaggerDelay_BasedOnShortcutIndex()
        {
            // Arrange
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator) { StaggerDelayMs = 10 };
            
            var item1 = new WindowItem { ShortcutIndex = 0, HasBeenAnimated = false };
            var item2 = new WindowItem { ShortcutIndex = 1, HasBeenAnimated = false };
            var item3 = new WindowItem { ShortcutIndex = 5, HasBeenAnimated = false }; // Gap in index

            var items = new List<WindowItem> { item1, item2, item3 };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.Equal(3, animator.AnimatedCalls.Count);

            // Verify delays: Index * StaggerDelayMs (10)
            Assert.Equal(0, animator.AnimatedCalls[0].Delay);  // Index 0
            Assert.Equal(10, animator.AnimatedCalls[1].Delay); // Index 1
            Assert.Equal(50, animator.AnimatedCalls[2].Delay); // Index 5
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SkipsAlreadyAnimatedItems()
        {
            // Arrange
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);
            
            // Item marked as animated
            var item = new WindowItem { ShortcutIndex = 0, HasBeenAnimated = true };
            
            // Set opacity to 0.5 to check if it gets forced to 1.0 (visible) when skipped
            item.BadgeOpacity = 0.5;

            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.Empty(animator.AnimatedCalls); // Should NOT call animate
            Assert.True(item.HasBeenAnimated, "Should stay animated");
            Assert.Equal(1.0, item.BadgeOpacity); // Should be forced visible
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SupportsMultipleItems_SharedHwnd()
        {
            // Arrange
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);
            
            // Two items sharing the same HWND (e.g. browser tabs)
            var item1 = new WindowItem { Hwnd = (IntPtr)12345, ShortcutIndex = 0, HasBeenAnimated = false };
            var item2 = new WindowItem { Hwnd = (IntPtr)12345, ShortcutIndex = 1, HasBeenAnimated = false };

            var items = new List<WindowItem> { item1, item2 };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.Equal(2, animator.AnimatedCalls.Count);
            Assert.True(item1.HasBeenAnimated);
            Assert.True(item2.HasBeenAnimated);
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_IgnoresItems_WithoutShortcuts()
        {
            // Arrange
            var animator = new MockBadgeAnimator();
            var service = new BadgeAnimationService(animator);
            
            var item = new WindowItem { ShortcutIndex = -1, HasBeenAnimated = false }; // -1 = No shortcut
            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.Empty(animator.AnimatedCalls);
            Assert.False(item.HasBeenAnimated, "Should NOT mark as animated");
        }
    }
}
