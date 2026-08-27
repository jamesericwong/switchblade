using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SwitchBlade.Contracts;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Animates badges using WPF BeginAnimation for GPU-accelerated, compositor-driven rendering.
    /// Uses BeginAnimation directly on visual elements instead of Storyboard to avoid
    /// naming-scope conflicts with data-bound TranslateTransform properties.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class StoryboardBadgeAnimator : IBadgeAnimator
    {
        private readonly IDispatcherService _dispatcherService;

        // View-owned mapping from item to its realized ListBoxItem container.
        // Injected by the composition root so this service never reaches into view internals.
        private readonly Func<WindowItem, ListBoxItem?> _containerResolver;

        public StoryboardBadgeAnimator(IDispatcherService dispatcherService, Func<WindowItem, ListBoxItem?> containerResolver)
        {
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _containerResolver = containerResolver ?? throw new ArgumentNullException(nameof(containerResolver));
        }

        public void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX, CancellationToken cancellationToken = default)
        {
            _dispatcherService.InvokeAsync(async () =>
            {
                // A superseded cycle must not touch badges at all — the newer trigger owns them now.
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 1. Immediately reset ViewModel state so the badge is 'ready' for animation
                // This ensures it stays hidden during any potential layout wait or stagger delay.
                item.BadgeOpacity = 0.0;
                item.BadgeTranslateX = startingOffsetX;

                // 2. Find the visual container for this item
                // We use a small polling retry because the item might not be realized yet (e.g. on startup)
                ListBoxItem? container = null;
                int attempts = 0;
                while (attempts < 20) // Max 20 attempts (approx 500ms total)
                {
                    container = _containerResolver(item);
                    if (container != null)
                    {
                        break;
                    }

                    attempts++;
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(25, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return; // Superseded mid-poll: the newer cycle owns these badges.
                    }
                }

                if (container == null)
                {
                    // If we still can't find it, just snap to final state
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                    return;
                }

                // 3. Find the "NumberBadge" border within the container template
                var badgeBorder = FindChild<Border>(container, "NumberBadge");
                if (badgeBorder == null)
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;
                    return;
                }

                // 4. Ensure RenderTransform is a TranslateTransform
                if (badgeBorder.RenderTransform is not TranslateTransform)
                {
                    badgeBorder.RenderTransform = new TranslateTransform();
                }
                var transform = (TranslateTransform)badgeBorder.RenderTransform;

                // 5. Clear any previous animations to prevent conflicts (essential for rapid typing)
                badgeBorder.BeginAnimation(UIElement.OpacityProperty, null);
                transform.BeginAnimation(TranslateTransform.XProperty, null);

                // 6. Create animations
                var duration = TimeSpan.FromMilliseconds(durationMs);
                var beginTime = TimeSpan.FromMilliseconds(delayMs);
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

                var opacityAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = duration,
                    BeginTime = beginTime,
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };

                var translateAnim = new DoubleAnimation
                {
                    From = startingOffsetX,
                    To = 0.0,
                    Duration = duration,
                    BeginTime = beginTime,
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd
                };

                // 7. On completion, sync ViewModel to final state and release animation hold
                opacityAnim.Completed += (s, e) =>
                {
                    item.BadgeOpacity = 1.0;
                    item.BadgeTranslateX = 0;

                    // Entry is complete: clear the in-flight marker so later passes treat this badge as settled.
                    item.EntryPending = false;

                    // Release animation hold - binding reasserts with the values we just set
                    badgeBorder.BeginAnimation(UIElement.OpacityProperty, null);
                    transform.BeginAnimation(TranslateTransform.XProperty, null);
                };

                // Cancellation may arrive between container resolution and apply — check once more so a superseded
                // cycle never starts animations the newer trigger has already replaced.
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 8. Apply animations directly to visual elements. Mark the entry in-flight from this moment until
                // completion: later trigger passes must leave it undisturbed rather than re-hiding or re-delaying it.
                badgeBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                transform.BeginAnimation(TranslateTransform.XProperty, translateAnim);
                item.EntryPending = true;
            });
        }

        private static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (child as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }

                var result = FindChild<T>(child, childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
