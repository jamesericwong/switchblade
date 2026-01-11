using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates staggered badge animations for Alt+Number shortcuts.
    /// Tracks animated HWNDs to prevent re-animation on title changes.
    /// </summary>
    public class BadgeAnimationService
    {
        private readonly HashSet<IntPtr> _animatedHwnds = new();
        private readonly object _lock = new();

        /// <summary>
        /// Duration of each badge's animation in milliseconds.
        /// </summary>
        public int AnimationDurationMs { get; set; } = 150;

        /// <summary>
        /// Delay between each badge's animation start in milliseconds.
        /// </summary>
        public int StaggerDelayMs { get; set; } = 75;

        /// <summary>
        /// Starting X offset for slide-in animation (negative = from left).
        /// </summary>
        public double StartingOffsetX { get; set; } = -20;

        /// <summary>
        /// Resets the animation state, clearing all tracked HWNDs.
        /// Call this when the window is shown to allow fresh animations.
        /// </summary>
        public void ResetAnimationState()
        {
            lock (_lock)
            {
                _animatedHwnds.Clear();
            }
        }

        /// <summary>
        /// Checks if an item should be animated based on its HWND.
        /// Returns true only if this HWND hasn't been animated yet.
        /// </summary>
        public bool ShouldAnimateItem(IntPtr hwnd)
        {
            lock (_lock)
            {
                return !_animatedHwnds.Contains(hwnd);
            }
        }

        /// <summary>
        /// Marks an HWND as animated to prevent re-animation on title changes.
        /// </summary>
        public void MarkAsAnimated(IntPtr hwnd)
        {
            lock (_lock)
            {
                _animatedHwnds.Add(hwnd);
            }
        }

        /// <summary>
        /// Triggers staggered animations for the given window items.
        /// Only items with shortcuts (index 0-9) and not previously animated will animate.
        /// </summary>
        public async Task TriggerStaggeredAnimationAsync(IEnumerable<WindowItem> items)
        {
            int maxShortcutIndex = -1;
            foreach (var item in items)
            {
                if (!item.IsShortcutVisible)
                {
                    continue;
                }

                bool shouldAnimate = ShouldAnimateItem(item.Hwnd);

                if (shouldAnimate)
                {
                    // Use ShortcutIndex for stagger order (0-9)
                    // This ensures Alt+1 (index 0) animates first, Alt+0 (index 9) animates last
                    int delay = item.ShortcutIndex * StaggerDelayMs;

                    // Schedule the animation
                    _ = AnimateItemAsync(item, delay);

                    MarkAsAnimated(item.Hwnd);

                    if (item.ShortcutIndex > maxShortcutIndex)
                    {
                        maxShortcutIndex = item.ShortcutIndex;
                    }
                }
                else
                {
                    // Already animated - ensure it's visible
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                }
            }

            // Wait for all animations to complete
            if (maxShortcutIndex >= 0)
            {
                int maxDelay = (maxShortcutIndex + 1) * StaggerDelayMs + AnimationDurationMs;
                await Task.Delay(maxDelay);
            }
        }

        private async Task AnimateItemAsync(WindowItem item, int delayMs)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            // Animate using simple linear interpolation over time
            // WPF animations must run on UI thread, but we're doing property-based animation
            int steps = AnimationDurationMs / 16; // ~60fps
            if (steps < 1) steps = 1;

            double opacityStep = 1.0 / steps;
            double translateStep = 10.0 / steps; // From -10 to 0

            for (int i = 0; i <= steps; i++)
            {
                double progress = (double)i / steps;
                // Ease-out cubic for smoother deceleration
                double easedProgress = 1 - Math.Pow(1 - progress, 3);

                item.BadgeOpacity = easedProgress;
                item.BadgeTranslateX = StartingOffsetX * (1 - easedProgress);

                if (i < steps)
                {
                    await Task.Delay(16);
                }
            }

            // Ensure final values
            item.BadgeOpacity = 1.0;
            item.BadgeTranslateX = 0;
        }
    }
}
