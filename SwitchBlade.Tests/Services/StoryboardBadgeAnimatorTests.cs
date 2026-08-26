using System;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class StoryboardBadgeAnimatorTests
    {
        private readonly SwitchBlade.Tests.SynchronousDispatcherService _dispatcher = new();

        [Fact]
        public void Constructor_NullDispatcher_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StoryboardBadgeAnimator(null!, item => null));
        }

        [Fact]
        public void Constructor_NullContainerResolver_ThrowsArgumentNullException()
        {
            var ex = Record.Exception(() => new StoryboardBadgeAnimator(_dispatcher, null!)) as ArgumentNullException;

            Assert.NotNull(ex);
            Assert.Equal("containerResolver", ex!.ParamName);
        }

        [Fact(Timeout = 15000)]
        public async Task Animate_ContainerNotFound_SnapsToFinalState()
        {
            var item = new WindowItem { Hwnd = new IntPtr(42), Title = "orphan", Source = null };
            var animator = new StoryboardBadgeAnimator(_dispatcher, _ => null);

            item.BadgeOpacity = 0.0;
            animator.Animate(item, delayMs: 0, durationMs: 150, startingOffsetX: -20);

            // The polling retry loop (up to ~500ms) must give up and snap the badge fully visible.
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (item.BadgeOpacity != 1.0 && DateTime.UtcNow < deadline)
            {
                await System.Threading.Tasks.Task.Delay(10);
            }

            Assert.Equal(1.0, item.BadgeOpacity);
            Assert.Equal(0.0, item.BadgeTranslateX);
        }
    }
}
