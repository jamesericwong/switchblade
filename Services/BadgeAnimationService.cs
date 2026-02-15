using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Coordinates staggered badge animations for Alt+Number shortcuts.
    /// Delegates the actual animation execution to an IBadgeAnimator strategy.
    /// Uses debouncing to prevent animation fighting during rapid input.
    /// </summary>
    public class BadgeAnimationService
    {
        private readonly IBadgeAnimator _animator;
        private CancellationTokenSource? _animationCts;

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
        /// Debounce interval: how long to wait after the last trigger before
        /// actually starting animations. Prevents wasteful animation starts
        /// during rapid typing. Matched to one stagger step for natural feel.
        /// </summary>
        public int DebounceMs { get; set; } = 75;

        public BadgeAnimationService(IBadgeAnimator animator)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
        }

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
        /// Uses debouncing: if called again within DebounceMs, the previous call is cancelled.
        /// This ensures animations only play once typing settles, preventing jitter.
        /// </summary>
        /// <param name="items">The items to animate.</param>
        /// <param name="skipDebounce">When true, skips the debounce delay (e.g., for hotkey/initial load).</param>
        public async Task TriggerStaggeredAnimationAsync(IEnumerable<WindowItem>? items, bool skipDebounce = false)
        {
            SwitchBlade.Core.Logger.Log($"[BadgeAnimation] TriggerStaggeredAnimationAsync: Starting");
            if (items == null) return;

            // Cancel any pending animation cycle from a previous call
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = new CancellationTokenSource();
            var ct = _animationCts.Token;

            // Immediately hide all badges that need animating (so they don't show stale state)
            foreach (var item in items)
            {
                if (item.IsShortcutVisible && !item.HasBeenAnimated)
                {
                    item.ResetBadgeAnimation();
                }
            }

            // Debounce: wait for typing to settle before starting the animation cycle.
            // If another call arrives during this window, this one is cancelled.
            // Skip debounce for hotkey/initial load so the animation feels responsive.
            if (!skipDebounce)
            {
                try
                {
                    await Task.Delay(DebounceMs, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (ct.IsCancellationRequested) return;

            int maxShortcutIndex = -1;
            int animatedCount = 0;
            int skippedCount = 0;

            foreach (var item in items)
            {
                if (ct.IsCancellationRequested) return;

                if (!item.IsShortcutVisible)
                {
                    continue;
                }

                bool shouldAnimate = !item.HasBeenAnimated;

                if (shouldAnimate)
                {
                    int delay = item.ShortcutIndex * StaggerDelayMs;

                    // Delegate execution to the strategy
                    _animator.Animate(item, delay, AnimationDurationMs, StartingOffsetX);

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

            // Wait for all animations to complete (approximate based on max duration)
            if (maxShortcutIndex >= 0)
            {
                int maxDelay = (maxShortcutIndex + 1) * StaggerDelayMs + AnimationDurationMs;
                try
                {
                    await Task.Delay(maxDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    // Animation cycle was superseded — that's fine
                }
            }
        }
    }
}
