using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation;

namespace SwitchBlade.Contracts
{
    /// <summary>
    /// Activation strategies for a UIA element, tried in caller-specified order. Each plugin keeps its
    /// historically verified preference (e.g., Teams invokes before selecting; tab plugins select first).
    /// </summary>
    public enum UiaActivationStrategy
    {
        SelectionItem,
        Invoke,
        ExpandCollapse
    }

    /// <summary>
    /// Shared activation cascade for UIA elements: tries each strategy in order (first success wins),
    /// then falls back to SetFocus — the final step every plugin historically performed.
    /// A transient failure of one strategy falls through to the next; a non-transient exception propagates
    /// so genuine bugs surface instead of being silently swallowed (§11 rule 1).
    /// </summary>
    public static class ElementActivator
    {
        /// <summary>
        /// Activates <paramref name="element"/> using the given strategies in order, falling back to SetFocus.
        /// Returns true when any strategy or the focus fallback completed; false only when every attempt
        /// failed transiently (or no strategies were supplied and SetFocus failed).
        /// </summary>
        [ExcludeFromCodeCoverage] // Requires a live UIA element; transient semantics are covered via UiaSafe tests.
        public static bool TryActivate(AutomationElement element, params UiaActivationStrategy[] strategies)
        {
            foreach (var strategy in strategies)
            {
                var activated = strategy switch
                {
                    UiaActivationStrategy.SelectionItem => TrySelect(element),
                    UiaActivationStrategy.Invoke => TryInvoke(element),
                    UiaActivationStrategy.ExpandCollapse => TryExpand(element),
                    _ => false
                };

                if (activated)
                {
                    return true;
                }
            }

            // Final fallback shared by all plugins: focus the element.
            return UiaSafe.TryRun(() => element.SetFocus());
        }

        private static bool TrySelect(AutomationElement element)
        {
            var pattern = GetPattern(element, SelectionItemPattern.Pattern);
            if (pattern == null)
            {
                return false;
            }

            return UiaSafe.TryRun(() => ((SelectionItemPattern)pattern).Select());
        }

        private static bool TryInvoke(AutomationElement element)
        {
            var pattern = GetPattern(element, InvokePattern.Pattern);
            if (pattern == null)
            {
                return false;
            }

            return UiaSafe.TryRun(() => ((InvokePattern)pattern).Invoke());
        }

        private static bool TryExpand(AutomationElement element)
        {
            var pattern = GetPattern(element, ExpandCollapsePattern.Pattern);
            if (pattern == null)
            {
                return false;
            }

            return UiaSafe.TryRun(() => ((ExpandCollapsePattern)pattern).Expand());
        }

        private static object? GetPattern(AutomationElement element, AutomationPattern patternId)
        {
            object? result = null;
            var available = UiaSafe.TryGet(
                () =>
                {
                    if (element.TryGetCurrentPattern(patternId, out var pattern))
                    {
                        result = pattern;
                    }

                    return result != null;
                },
                out var ok);

            return ok ? result : null;
        }
    }
}
