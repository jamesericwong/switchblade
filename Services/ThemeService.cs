using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

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
            _resourceHandler = resourceHandler ?? new WinUIApplicationResourceHandler();
            InitializeThemes();
        }

        private void InitializeThemes()
        {
            AvailableThemes = new List<ThemeInfo>
            {
                // WinUI uses built-in theming, these are accent overrides
                CreateTheme("Dark", "#1E1E1E", "#2D2D30", "#DDDDDD", "#3E3E42"),
                CreateTheme("Cyberpunk", "#09080D", "#13111A", "#00FF9F", "#FF003C"),
                CreateTheme("Deep Ocean", "#0F1724", "#172336", "#E6F1FF", "#1E2D45"),
                CreateTheme("Moonlight", "#22252A", "#2C3038", "#AABBC3", "#3C424D"),
                CreateTheme("Dracula", "#282a36", "#44475a", "#f8f8f2", "#6272a4"),
                CreateTheme("Light", "#F5F5F5", "#FFFFFF", "#333333", "#E0E0E0"),
            };
        }

        private ThemeInfo CreateTheme(string name, string background, string controlBackground, string foreground, string border)
        {
            var dict = new ResourceDictionary();

            // Parse colors for WinUI
            var bgColor = ParseColor(background);
            var ctrlColor = ParseColor(controlBackground);
            var fgColor = ParseColor(foreground);
            var borderColor = ParseColor(border);

            dict["WindowBackground"] = new SolidColorBrush(bgColor);
            dict["ControlBackground"] = new SolidColorBrush(ctrlColor);
            dict["ForegroundBrush"] = new SolidColorBrush(fgColor);
            dict["BorderBrush"] = new SolidColorBrush(borderColor);

            // Highlight with low opacity
            var highlightBrush = new SolidColorBrush(fgColor) { Opacity = 0.1 };
            dict["HighlightBrush"] = highlightBrush;

            return new ThemeInfo { Name = name, Resources = dict };
        }

        private static Windows.UI.Color ParseColor(string hex)
        {
            hex = hex.TrimStart('#');
            byte a = 255;
            byte r, g, b;

            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex.Substring(0, 2), 16);
                r = Convert.ToByte(hex.Substring(2, 2), 16);
                g = Convert.ToByte(hex.Substring(4, 2), 16);
                b = Convert.ToByte(hex.Substring(6, 2), 16);
            }
            else if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }
            else
            {
                throw new ArgumentException($"Invalid color format: {hex}");
            }

            return Windows.UI.Color.FromArgb(a, r, g, b);
        }

        public void ApplyTheme(string themeName)
        {
            var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName) ?? AvailableThemes.First();

            // Remove the previously applied theme dictionary
            if (_currentThemeDictionary != null)
            {
                _resourceHandler.RemoveMergedDictionary(_currentThemeDictionary);
            }

            // Apply new one
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
