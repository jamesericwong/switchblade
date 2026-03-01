using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SwitchBlade.Core
{
    /// <summary>
    /// Represents a text segment with an indication of whether it should be bold.
    /// This is a pure data type with no WPF dependency, enabling unit testing.
    /// </summary>
    public readonly struct HighlightSegment
    {
        public string Text { get; }
        public bool IsBold { get; }

        public HighlightSegment(string text, bool isBold)
        {
            Text = text;
            IsBold = isBold;
        }
    }

    /// <summary>
    /// Attached behavior that highlights matching search characters in a TextBlock
    /// by rendering them with bold font weight.
    /// </summary>
    /// <remarks>
    /// Uses four attached DPs (Title, SearchText, IsEnabled, UseFuzzy).
    /// When any of them changes, the TextBlock.Inlines collection is rebuilt
    /// with bold <see cref="Run"/> segments for matched characters.
    /// </remarks>
    public static class SearchHighlightBehavior
    {
        // --- Title ---
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.RegisterAttached(
                "Title", typeof(string), typeof(SearchHighlightBehavior),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static string GetTitle(DependencyObject obj) => (string)obj.GetValue(TitleProperty);
        public static void SetTitle(DependencyObject obj, string value) => obj.SetValue(TitleProperty, value);

        // --- SearchText ---
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.RegisterAttached(
                "SearchText", typeof(string), typeof(SearchHighlightBehavior),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static string GetSearchText(DependencyObject obj) => (string)obj.GetValue(SearchTextProperty);
        public static void SetSearchText(DependencyObject obj, string value) => obj.SetValue(SearchTextProperty, value);

        // --- IsEnabled ---
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(SearchHighlightBehavior),
                new PropertyMetadata(true, OnPropertyChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        // --- UseFuzzy ---
        public static readonly DependencyProperty UseFuzzyProperty =
            DependencyProperty.RegisterAttached(
                "UseFuzzy", typeof(bool), typeof(SearchHighlightBehavior),
                new PropertyMetadata(true, OnPropertyChanged));

        public static bool GetUseFuzzy(DependencyObject obj) => (bool)obj.GetValue(UseFuzzyProperty);
        public static void SetUseFuzzy(DependencyObject obj, bool value) => obj.SetValue(UseFuzzyProperty, value);

        // --- HighlightColor ---
        public static readonly DependencyProperty HighlightColorProperty =
            DependencyProperty.RegisterAttached(
                "HighlightColor", typeof(string), typeof(SearchHighlightBehavior),
                new PropertyMetadata("#FF0078D4", OnPropertyChanged));

        public static string GetHighlightColor(DependencyObject obj) => (string)obj.GetValue(HighlightColorProperty);
        public static void SetHighlightColor(DependencyObject obj, string value) => obj.SetValue(HighlightColorProperty, value);

        // --- Core logic ---
        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock textBlock) return;

            string title = GetTitle(textBlock);
            string searchText = GetSearchText(textBlock);
            bool isEnabled = GetIsEnabled(textBlock);
            bool useFuzzy = GetUseFuzzy(textBlock);
            string highlightColor = GetHighlightColor(textBlock);

            var segments = BuildSegments(title, searchText, isEnabled, useFuzzy);
            
            System.Windows.Media.Brush? highlightBrush = null;
            try
            {
                if (!string.IsNullOrEmpty(highlightColor))
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(highlightColor);
                    highlightBrush = new System.Windows.Media.SolidColorBrush(color);
                }
            }
            catch { /* Fallback to default/none */ }

            textBlock.Inlines.Clear();
            foreach (var seg in segments)
            {
                var run = new Run(seg.Text);
                if (seg.IsBold)
                {
                    run.FontWeight = FontWeights.Bold;
                    if (highlightBrush != null)
                        run.Foreground = highlightBrush;
                }
                textBlock.Inlines.Add(run);
            }
        }

        /// <summary>
        /// Builds a list of <see cref="HighlightSegment"/> for the given title,
        /// marking matched characters as bold when highlighting is enabled.
        /// This method is pure (no WPF dependency) and safe to call from any thread.
        /// </summary>
        internal static List<HighlightSegment> BuildSegments(string title, string searchText, bool isEnabled, bool useFuzzy)
        {
            var segments = new List<HighlightSegment>();

            if (string.IsNullOrEmpty(title))
            {
                segments.Add(new HighlightSegment(string.Empty, false));
                return segments;
            }

            if (!isEnabled || string.IsNullOrEmpty(searchText))
            {
                segments.Add(new HighlightSegment(title, false));
                return segments;
            }

            var matchedIndices = FuzzyMatcher.GetMatchedIndices(title, searchText, useFuzzy);

            if (matchedIndices.Length == 0)
            {
                segments.Add(new HighlightSegment(title, false));
                return segments;
            }

            // Build a set for O(1) lookup
            var matchSet = new HashSet<int>(matchedIndices);

            // Group consecutive characters with the same bold/normal state into segments
            int segmentStart = 0;
            bool currentBold = matchSet.Contains(0);

            for (int i = 1; i <= title.Length; i++)
            {
                bool isBold = i < title.Length && matchSet.Contains(i);
                if (i == title.Length || isBold != currentBold)
                {
                    var text = title.Substring(segmentStart, i - segmentStart);
                    segments.Add(new HighlightSegment(text, currentBold));

                    segmentStart = i;
                    currentBold = isBold;
                }
            }

            return segments;
        }
    }
}
