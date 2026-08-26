using System.Collections.Generic;

namespace SwitchBlade.Services
{
    /// <summary>
    /// Abstraction of the application's theming engine. Consumers depend on this interface rather than
    /// the concrete ThemeService (DIP), and only see the members they actually call (ISP).
    /// </summary>
    public interface IThemeService
    {
        IReadOnlyList<ThemeInfo> AvailableThemes { get; }

        void ApplyTheme(string themeName);

        void LoadCurrentTheme();
    }
}
