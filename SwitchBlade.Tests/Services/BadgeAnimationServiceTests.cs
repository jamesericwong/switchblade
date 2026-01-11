using Xunit;
using SwitchBlade.Services;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Services
{
    public class BadgeAnimationServiceTests
    {
        [Fact]
        public void BadgeAnimationService_DefaultAnimationDuration_Is150Ms()
        {
            var service = new BadgeAnimationService();

            Assert.Equal(150, service.AnimationDurationMs);
        }

        [Fact]
        public void BadgeAnimationService_DefaultStaggerDelay_Is75Ms()
        {
            var service = new BadgeAnimationService();

            Assert.Equal(75, service.StaggerDelayMs);
        }

        [Fact]
        public void BadgeAnimationService_DefaultStartingOffsetX_IsNegative20()
        {
            var service = new BadgeAnimationService();

            Assert.Equal(-20, service.StartingOffsetX);
        }

        [Fact]
        public void ResetAnimationState_ClearsAnimatedHwnds()
        {
            var service = new BadgeAnimationService();
            var hwnd = (IntPtr)12345;

            // Mark as animated
            service.MarkAsAnimated(hwnd);
            Assert.False(service.ShouldAnimateItem(hwnd));

            // Reset
            service.ResetAnimationState();
            Assert.True(service.ShouldAnimateItem(hwnd));
        }

        [Fact]
        public void ShouldAnimateItem_ReturnsTrueForNewHwnd()
        {
            var service = new BadgeAnimationService();
            var hwnd = (IntPtr)12345;

            Assert.True(service.ShouldAnimateItem(hwnd));
        }

        [Fact]
        public void ShouldAnimateItem_ReturnsFalseAfterMarkedAsAnimated()
        {
            var service = new BadgeAnimationService();
            var hwnd = (IntPtr)12345;

            service.MarkAsAnimated(hwnd);

            Assert.False(service.ShouldAnimateItem(hwnd));
        }

        [Fact]
        public void MarkAsAnimated_AllowsMultipleHwnds()
        {
            var service = new BadgeAnimationService();
            var hwnd1 = (IntPtr)11111;
            var hwnd2 = (IntPtr)22222;
            var hwnd3 = (IntPtr)33333;

            service.MarkAsAnimated(hwnd1);
            service.MarkAsAnimated(hwnd2);

            Assert.False(service.ShouldAnimateItem(hwnd1));
            Assert.False(service.ShouldAnimateItem(hwnd2));
            Assert.True(service.ShouldAnimateItem(hwnd3));
        }

        [Fact]
        public void MarkAsAnimated_IsIdempotent()
        {
            var service = new BadgeAnimationService();
            var hwnd = (IntPtr)12345;

            // Mark the same HWND multiple times
            service.MarkAsAnimated(hwnd);
            service.MarkAsAnimated(hwnd);
            service.MarkAsAnimated(hwnd);

            // Should not throw and should still return false
            Assert.False(service.ShouldAnimateItem(hwnd));
        }

        [Fact]
        public void ResetAnimationState_IsIdempotent()
        {
            var service = new BadgeAnimationService();

            // Multiple resets should not throw
            service.ResetAnimationState();
            service.ResetAnimationState();

            // New HWNDs should still animate
            Assert.True(service.ShouldAnimateItem((IntPtr)12345));
        }
    }
}

