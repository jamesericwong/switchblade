using Xunit;
using SwitchBlade.Services;

namespace SwitchBlade.Tests.Services
{
    public class UserSettingsTests
    {
        [Fact]
        public void UserSettings_DefaultHotKeyModifiers_IsCtrlShift()
        {
            var settings = new UserSettings();

            // Ctrl (2) + Shift (4) = 6
            Assert.Equal(6u, settings.HotKeyModifiers);
        }

        [Fact]
        public void UserSettings_DefaultHotKeyKey_IsQ()
        {
            var settings = new UserSettings();

            // VK_Q = 0x51
            Assert.Equal(0x51u, settings.HotKeyKey);
        }

        [Fact]
        public void UserSettings_DefaultEnablePreviews_IsTrue()
        {
            var settings = new UserSettings();

            Assert.True(settings.EnablePreviews);
        }

        [Fact]
        public void UserSettings_DefaultLaunchOnStartup_IsFalse()
        {
            var settings = new UserSettings();

            Assert.False(settings.LaunchOnStartup);
        }

        [Fact]
        public void UserSettings_DefaultCurrentTheme_IsLight()
        {
            var settings = new UserSettings();

            Assert.Equal("Light", settings.CurrentTheme);
        }

        [Fact]
        public void UserSettings_DefaultBrowserProcesses_ContainsChrome()
        {
            var settings = new UserSettings();

            Assert.Contains("chrome", settings.BrowserProcesses);
        }

        [Fact]
        public void UserSettings_DefaultExcludedProcesses_ContainsSwitchBlade()
        {
            var settings = new UserSettings();

            // The default only contains "SwitchBlade"
            Assert.Contains("SwitchBlade", settings.ExcludedProcesses);
        }

        [Fact]
        public void UserSettings_DefaultEnableBackgroundPolling_IsTrue()
        {
            var settings = new UserSettings();

            Assert.True(settings.EnableBackgroundPolling);
        }

        [Fact]
        public void UserSettings_DefaultBackgroundPollingIntervalSeconds_Is30()
        {
            var settings = new UserSettings();

            Assert.Equal(30, settings.BackgroundPollingIntervalSeconds);
        }

        [Fact]
        public void UserSettings_SetProperty_ReturnsNewValue()
        {
            var settings = new UserSettings();

            settings.HotKeyModifiers = 0x0008; // WIN key
            settings.EnablePreviews = false;
            settings.CurrentTheme = "Light";

            Assert.Equal(0x0008u, settings.HotKeyModifiers);
            Assert.False(settings.EnablePreviews);
            Assert.Equal("Light", settings.CurrentTheme);
        }
    }
}
