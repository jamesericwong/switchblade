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
        private readonly SettingsService _settingsService;
        private ResourceDictionary? _currentThemeDictionary; 
        public List<ThemeInfo> AvailableThemes { get; private set; } = new List<ThemeInfo>();

        public ThemeService(SettingsService settingsService)
        {
            _settingsService = settingsService;
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
            };
        }

        private ThemeInfo CreateTheme(string name, string background, string controlBackground, string foreground, string border)
        {
            var dict = new ResourceDictionary();
            dict["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
            dict["ControlBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(controlBackground));
            dict["ForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
            dict["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
            
            // Highlight: Use border color but with slight opacity for hover effects
            var highlight = (Color)ColorConverter.ConvertFromString(border);
            // If border is too dark, lighten it?? 
            // Better: use Foreground with very low opacity
            var fg = (Color)ColorConverter.ConvertFromString(foreground);
            dict["HighlightBrush"] = new SolidColorBrush(fg) { Opacity = 0.1 }; 

            return new ThemeInfo { Name = name, Resources = dict };
        }

        public void ApplyTheme(string themeName)
        {
            var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName) ?? AvailableThemes.First();
            
            // Remove the previously applied theme dictionary
            if (_currentThemeDictionary != null)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            }

            // apply new one
            _currentThemeDictionary = theme.Resources;
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(_currentThemeDictionary);

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
