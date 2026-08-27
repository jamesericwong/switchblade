using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Contracts;
using SwitchBlade.Core;

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
        private readonly IDelayProvider _delayProvider;
        private readonly ILogger? _logger;
        private CancellationTokenSource? _animationCts;

        // Grace beyond (delay + duration) covering container polling and settle slack for a healthy entry. Bounds how
        // long a dead-but-flagged animation can keep a badge hidden before a later pass force-settles it.
        private const int EntrySettleGraceMs = 800;

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

        public BadgeAnimationService(IBadgeAnimator animator, IDelayProvider? delayProvider = null, ILogger? logger = null)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _delayProvider = delayProvider ?? new SystemDelayProvider();
            _logger = logger;
        }

        /// <summary>
        /// Resets the animation state for the provided items.
        /// Use this when you want to force re-animation (e.g. on new search or window open).
        /// </summary>
        public void ResetAnimationState(IEnumerable<WindowItem>? items)
        {
            if (items == null)
            {
                return;
            }

            // We just reset the flag. We do NOT reset the visual Opacity/TranslateX here.
            // Pushing visual state to hidden happens just-in-time in TriggerStaggeredAnimationAsync.
            foreach (var item in items)
            {
                item.HasBeenAnimated = false;
            }
            _logger?.Log($"[BadgeAnimation] ResetAnimationState: Reset HasBeenAnimated flag for items");
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
            _logger?.Log($"[BadgeAnimation] TriggerStaggeredAnimationAsync: Starting");
            if (items == null)
            {
                return;
            }

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
                    await _delayProvider.Delay(DebounceMs, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            int maxShortcutIndex = -1;
            int animatedCount = 0;
            int skippedCount = 0;

            // A wave is in flight when any badge's entry animation has been applied but not finished. Joiners into a
            // running wave use a capped delay instead of their nominal slot (that moment already passed — waiting for
            // it only reads as lag).
            bool cascadeLive = items.Any(i => i.EntryPending);

            foreach (var item in items)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (!item.IsShortcutVisible)
                {
                    continue;
                }

                // An entry animation already applied to this badge must finish undisturbed: re-hiding or re-delaying
                // it mid-animation is what made rapid trigger passes read as flash and lag. Within its protection window
                // it settles itself via the animator's completion handler.
                if (item.EntryPending)
                {
                    if (Environment.TickCount64 < item.EntryProtectionTicks)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Window elapsed with the flag still set: this entry's animation died before completing (its row
                    // container was recycled mid re-sort, or a superseded cycle released it). Force-settle it and clear
                    // the stale marker — skipping such badges forever is how they end up missing.
                    item.EntryPending = false;
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                    skippedCount++;
                }

                // An already-animated badge that is currently hidden was interrupted by a superseded cycle before its
                // animation applied — re-run it so rapid toggles still see the stagger instead of an instant pop-in.
                bool shouldAnimate = !item.HasBeenAnimated || item.BadgeOpacity < 0.5;

                if (shouldAnimate)
                {
                    int nominalDelay = item.ShortcutIndex * StaggerDelayMs;
                    int delay = cascadeLive ? Math.Min(nominalDelay, 2 * StaggerDelayMs) : nominalDelay;

                    // Delegate execution to the strategy. The token ties the animation to this cycle: if a newer
                    // trigger supersedes it, the animator must stop before touching badges that haven't started yet.
                    _animator.Animate(item, delay, AnimationDurationMs, StartingOffsetX, ct);

                    // Mark as animated immediately so we don't re-animate on next pass
                    item.HasBeenAnimated = true;

                    // Stamp this entry's protection window: stagger delay + duration + polling/grace slack. If
                    // EntryPending is still set past this point, the animation can no longer be alive and a later pass
                    // force-settles it instead of skipping forever (see WindowItem.EntryProtectionTicks).
                    item.EntryProtectionTicks = Environment.TickCount64 + delay + AnimationDurationMs + EntrySettleGraceMs;
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

            _logger?.Log($"[BadgeAnimation] TriggerStaggeredAnimationAsync: Animated={animatedCount}, Skipped={skippedCount}");

            // Wait for all animations to complete (approximate based on max duration)
            if (maxShortcutIndex >= 0)
            {
                int maxDelay = (maxShortcutIndex + 1) * StaggerDelayMs + AnimationDurationMs;
                try
                {
                    await _delayProvider.Delay(maxDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    // Animation cycle was superseded — that's fine
                }
            }
        }
    }
}
