using System;
using System.Threading;
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

        [Fact]
        public void Animate_AlreadyCancelledToken_LeavesVisibleBadgeUntouched()
        {
            // C contract: a superseded cycle must not hide or re-apply state to badges the newer trigger owns —
            // an already-visible badge stays exactly as-is when its animator work is cancelled.
            var item = new WindowItem { Hwnd = new IntPtr(43), Title = "orphan", Source = null };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var animator = new StoryboardBadgeAnimator(_dispatcher, _ => null);
            item.BadgeOpacity = 1.0; // visible state from the superseding cycle
            item.BadgeTranslateX = 0.0;

            var ex = Record.Exception(() => animator.Animate(item, delayMs: 0, durationMs: 150, startingOffsetX: -20, cts.Token));

            Assert.Null(ex);
            Assert.Equal(1.0, item.BadgeOpacity);
            Assert.Equal(0.0, item.BadgeTranslateX);
        }
    }
}
