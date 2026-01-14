using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SwitchBlade.Services
{
    public class ThemeInfo
    {
        public string Name { get; set; } = "";
        public ResourceDictionary Resources { get; set; } = new ResourceDictionary();
    }

    public class ThemeService
    {
        private readonly ISettingsService _settingsService;
        private readonly IApplicationResourceHandler _resourceHandler;
        private ResourceDictionary? _currentThemeDictionary;
        public List<ThemeInfo> AvailableThemes { get; private set; } = new List<ThemeInfo>();

        public ThemeService(ISettingsService settingsService, IApplicationResourceHandler? resourceHandler = null)
        {
            _settingsService = settingsService;
            _resourceHandler = resourceHandler ?? new WpfApplicationResourceHandler();
            InitializeThemes();
        }

        private void InitializeThemes()
        {
            AvailableThemes = new List<ThemeInfo>
            {
                // Sleek Dark (Default)
                CreateTheme("Dark", "#1E1E1E", "#2D2D30", "#DDDDDD", "#3E3E42"),
                // Cyberpunk (High Contrast Neon)
                CreateTheme("Cyberpunk", "#09080D", "#13111A", "#00FF9F", "#FF003C"),
                // Deep Ocean (Blue/Black)
                CreateTheme("Deep Ocean", "#0F1724", "#172336", "#E6F1FF", "#1E2D45"),
                // Moonlight (Cool Grey)
                CreateTheme("Moonlight", "#22252A", "#2C3038", "#AABBC3", "#3C424D"),
                // Dracula (Classic)
                CreateTheme("Dracula", "#282a36", "#44475a", "#f8f8f2", "#6272a4"),
                // Light (Clean)
                CreateTheme("Light", "#F5F5F5", "#FFFFFF", "#333333", "#E0E0E0"),
                // Super Light (Pure White)
                CreateTheme("Super Light", "#FFFFFF", "#FFFFFF", "#111111", "#F0F0F0"),
            };
        }

        private ThemeInfo CreateTheme(string name, string background, string controlBackground, string foreground, string border)
        {
            var dict = new ResourceDictionary();

            // Background with 85% opacity for Glass effect
            var bgCol = (Color)ColorConverter.ConvertFromString(background);
            bgCol.A = 217; // ~85%
            dict["WindowBackground"] = new SolidColorBrush(bgCol);

            // Control background with 60% opacity
            var ctrlCol = (Color)ColorConverter.ConvertFromString(controlBackground);
            ctrlCol.A = 153; // ~60%
            dict["ControlBackground"] = new SolidColorBrush(ctrlCol);

            dict["ForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));

            // Border with 30% opacity
            var borderCol = (Color)ColorConverter.ConvertFromString(border);
            borderCol.A = 77; // ~30%
            dict["BorderBrush"] = new SolidColorBrush(borderCol);

            // Highlight: Use border color with more opacity for better hover contrast
            var highlightCol = (Color)ColorConverter.ConvertFromString(border);
            highlightCol.A = 150; // ~60% for better contrast
            dict["HighlightBrush"] = new SolidColorBrush(highlightCol);

            // Accent Brush (Blue-ish defaults, can be customized per theme)
            dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 120, 215));

            return new ThemeInfo { Name = name, Resources = dict };
        }

        public void ApplyTheme(string themeName)
        {
            var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName) ?? AvailableThemes.First();

            // Remove the previously applied theme dictionary
            if (_currentThemeDictionary != null)
            {
                _resourceHandler.RemoveMergedDictionary(_currentThemeDictionary);
            }

            // apply new one
            _currentThemeDictionary = theme.Resources;
            _resourceHandler.AddMergedDictionary(_currentThemeDictionary);

            if (_settingsService.Settings.CurrentTheme != themeName)
            {
                _settingsService.Settings.CurrentTheme = themeName;
                _settingsService.SaveSettings();
            }
        }

        public void LoadCurrentTheme()
        {
            ApplyTheme(_settingsService.Settings.CurrentTheme);
        }
    }
}
