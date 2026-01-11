using Xunit;
using SwitchBlade.Services;
using SwitchBlade.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwitchBlade.Tests.Services
{
    public class BadgeAnimationServiceTests
    {
        [Fact]
        public void BadgeAnimationService_DefaultProperties_AreSet()
        {
            var service = new BadgeAnimationService();

            Assert.Equal(150, service.AnimationDurationMs);
            Assert.Equal(75, service.StaggerDelayMs);
            Assert.Equal(-20, service.StartingOffsetX);
        }

        [Fact]
        public void ResetAnimationState_ResetsItemHasBeenAnimatedFlag()
        {
            var service = new BadgeAnimationService();
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
            var service = new BadgeAnimationService();

            // Should not throw
            service.ResetAnimationState((IEnumerable<WindowItem>?)null);
            service.ResetAnimationState(new List<WindowItem>());
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SetsHasBeenAnimated_True()
        {
            var service = new BadgeAnimationService { AnimationDurationMs = 1, StaggerDelayMs = 1 }; // Fast for test
            var item = new WindowItem { ShortcutIndex = 0, HasBeenAnimated = false };
            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.True(item.HasBeenAnimated);
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SkipsAlreadyAnimatedItems()
        {
            var service = new BadgeAnimationService { AnimationDurationMs = 1, StaggerDelayMs = 1 };
            // Item marked as animated
            var item = new WindowItem { ShortcutIndex = 0, HasBeenAnimated = true };

            // Set opacity to 0.5 to check if it gets reset to 1.0 (visible) when skipped
            item.BadgeOpacity = 0.5;

            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.True(item.HasBeenAnimated, "Should stay animated");
            Assert.Equal(1.0, item.BadgeOpacity); // Should be forced visible
            // Note: We can't easily verify AnimateItemAsync was NOT called without mocking, 
            // but checking logic paths indirectly via side effects (opacity set to 1.0 immediately vs animated)
            // is a decent proxy.
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_SupportsMultipleItems_SharedHwnd()
        {
            var service = new BadgeAnimationService { AnimationDurationMs = 1, StaggerDelayMs = 1 };
            // Two items sharing the same HWND (e.g. browser tabs)
            var item1 = new WindowItem { Hwnd = (IntPtr)12345, ShortcutIndex = 0, HasBeenAnimated = false };
            var item2 = new WindowItem { Hwnd = (IntPtr)12345, ShortcutIndex = 1, HasBeenAnimated = false };

            var items = new List<WindowItem> { item1, item2 };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.True(item1.HasBeenAnimated, "Item1 should animate");
            Assert.True(item2.HasBeenAnimated, "Item2 should animate (distinct object)");
        }

        [Fact]
        public async Task TriggerStaggeredAnimationAsync_IgnoresItems_WithoutShortcuts()
        {
            var service = new BadgeAnimationService { AnimationDurationMs = 1, StaggerDelayMs = 1 };
            var item = new WindowItem { ShortcutIndex = -1, HasBeenAnimated = false };
            var items = new List<WindowItem> { item };

            // Act
            await service.TriggerStaggeredAnimationAsync(items);

            // Assert
            Assert.False(item.HasBeenAnimated, "Should NOT animate item without shortcut");
        }
    }
}


