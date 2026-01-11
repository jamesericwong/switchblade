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
        // Removed: private readonly HashSet<IntPtr> _animatedHwnds = new();
        // Removed: private readonly object _lock = new();

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
        /// Resets the animation state for the provided items.
        /// Use this when you want to force re-animation (e.g. on new search or window open).
        /// </summary>
        public void ResetAnimationState(IEnumerable<WindowItem>? items)
        {
            if (items == null) return;

            // We just reset the flag. We do NOT reset the visual Opacity/TranslateX here.
            // Pushing visual state to hidden happens just-in-time in TriggerStaggeredAnimationAsync.
            foreach (var item in items)
            {
                item.HasBeenAnimated = false;
            }
            SwitchBlade.Core.Logger.Log($"[BadgeAnimation] ResetAnimationState: Reset HasBeenAnimated flag for items");
        }

        /// <summary>
        /// Triggers staggered animations for the given window items.
        /// Only items with shortcuts (index 0-9) and not previously animated will animate.
        /// </summary>
        public async Task TriggerStaggeredAnimationAsync(IEnumerable<WindowItem>? items)
        {
            int maxShortcutIndex = -1;
            int animatedCount = 0;
            int skippedCount = 0;

            SwitchBlade.Core.Logger.Log($"[BadgeAnimation] TriggerStaggeredAnimationAsync: Starting");
            if (items == null) return;

            foreach (var item in items)
            {
                if (!item.IsShortcutVisible)
                {
                    continue;
                }

                // Check item-level state instead of global HWND tracking
                // This allows distinct tabs with same HWND to animate independently
                bool shouldAnimate = !item.HasBeenAnimated;
                SwitchBlade.Core.Logger.Log($"[BadgeAnimation] Item '{item.Title}' HWND={item.Hwnd}, ShortcutIndex={item.ShortcutIndex}, ShouldAnimate={shouldAnimate}");

                if (shouldAnimate)
                {
                    // Use ShortcutIndex for stagger order (0-9)
                    // This ensures Alt+1 (index 0) animates first, Alt+0 (index 9) animates last
                    int delay = item.ShortcutIndex * StaggerDelayMs;

                    // Reset item to initial animation state (hidden) just-in-time
                    item.ResetBadgeAnimation();

                    // Schedule the animation
                    _ = AnimateItemAsync(item, delay);

                    // Mark as animated immediately so we don't re-animate on next pass
                    item.HasBeenAnimated = true;
                    animatedCount++;

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
                    skippedCount++;
                }
            }

            SwitchBlade.Core.Logger.Log($"[BadgeAnimation] TriggerStaggeredAnimationAsync: Animated={animatedCount}, Skipped={skippedCount}");

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
