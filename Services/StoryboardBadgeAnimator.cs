using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SwitchBlade.Contracts;
using SwitchBlade.Views.Components;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Animates badges using WPF BeginAnimation for GPU-accelerated, compositor-driven rendering.
    /// Uses BeginAnimation directly on visual elements instead of Storyboard to avoid
    /// naming-scope conflicts with data-bound TranslateTransform properties.
    /// </summary>
    public class StoryboardBadgeAnimator : IBadgeAnimator
    {
        private readonly IDispatcherService _dispatcherService;

        public StoryboardBadgeAnimator(IDispatcherService dispatcherService)
        {
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        }

        public void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX)
        {
            _dispatcherService.InvokeAsync(async () =>
            {
                // 1. Immediately reset ViewModel state so the badge is 'ready' for animation
                // This ensures it stays hidden during any potential layout wait or stagger delay.
                item.BadgeOpacity = 0.0;
                item.BadgeTranslateX = startingOffsetX;

                // 2. Find the visual container for this item
                // We use a small polling retry because the item might not be realized yet (e.g. on startup)
                ListBoxItem? container = null;
                int attempts = 0;
                while (attempts < 10) // Max 10 attempts (approx 100-200ms total)
                {
                    container = FindContainer(item);
                    if (container != null) break;

                    attempts++;
                    await System.Threading.Tasks.Task.Delay(15); 
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

                    // Release animation hold - binding reasserts with the values we just set
                    badgeBorder.BeginAnimation(UIElement.OpacityProperty, null);
                    transform.BeginAnimation(TranslateTransform.XProperty, null);
                };

                // 8. Apply animations directly to visual elements
                badgeBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                transform.BeginAnimation(TranslateTransform.XProperty, translateAnim);
            });
        }

        private ListBoxItem? FindContainer(WindowItem item)
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return null;

            var resultListControl = mainWindow.FindName("ResultsConfig") as ResultList;
            if (resultListControl == null) return null;

            var listBox = resultListControl.InnerListBox;
            return listBox?.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        }

        private static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (child as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }

                var result = FindChild<T>(child, childName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
