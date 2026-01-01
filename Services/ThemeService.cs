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
                CreateTheme("Light", "#FFFFFF", "#F0F0F0", "#333333", "#CCCCCC"),
                CreateTheme("Dark", "#202020", "#303030", "#FFFFFF", "#404040"),
                CreateTheme("Solarized", "#002b36", "#073642", "#839496", "#586e75"),
                CreateTheme("Dracula", "#282a36", "#44475a", "#f8f8f2", "#6272a4"),
                CreateTheme("Nord", "#2e3440", "#3b4252", "#d8dee9", "#4c566a"),
                CreateTheme("Cyberpunk", "#000b1e", "#05162e", "#00ff9f", "#ff003c")
            };
        }

        private ThemeInfo CreateTheme(string name, string background, string controlBackground, string foreground, string border)
        {
            var dict = new ResourceDictionary();
            dict["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
            dict["ControlBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(controlBackground));
            dict["ForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
            dict["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
            
            // Generate a highlight brush (e.g., control background with some opacity or lighter)
            // For simplicity, reusing border or a computed color could work, but let's just use Border for now or a fixed transparency
            var highlightColor = (Color)ColorConverter.ConvertFromString(controlBackground);
            highlightColor.A = 100; // Semi-transparent
            dict["HighlightBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border)) { Opacity = 0.4 }; 
            
            return new ThemeInfo { Name = name, Resources = dict };
        }

        public void ApplyTheme(string themeName)
        {
            var theme = AvailableThemes.FirstOrDefault(t => t.Name == themeName) ?? AvailableThemes.First();
            
            // Remove old theme resources if any (we assume we clear specific keys or just clear merged dictionaries that are themes)
            // For simplicity, let's assume we manage one "Theme" dict in App.xaml
            
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear(); 
            // Re-add other necessary dicts if we had any, but we likely don't at this stage except default styles.
            // If we have default styles in App.xaml, we should be careful. 
            // Better strategy: Have a dedicated MergedDictionary for Theme.
            
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(theme.Resources);

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
