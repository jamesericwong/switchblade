using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Plugins.Teams;
using Xunit;

namespace SwitchBlade.Tests.Plugins
{
    public class TeamsPluginTests
    {
        private readonly TeamsPlugin _plugin;
        private readonly Mock<IPluginSettingsService> _mockSettings;
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;

        public TeamsPluginTests()
        {
            _mockSettings = new Mock<IPluginSettingsService>();
            _mockContext = new Mock<IPluginContext>();
            _mockLogger = new Mock<ILogger>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);

            _plugin = new TeamsPlugin(_mockSettings.Object);
        }

        [Fact]
        public void Metadata_IsCorrect()
        {
            Assert.Equal("TeamsPlugin", _plugin.PluginName);
            Assert.True(_plugin.HasSettings);
            Assert.True(_plugin.IsUiaProvider);
        }

        [Fact]
        public void Initialize_LoadsSettings_IfExist()
        {
            // Arrange
            _mockSettings.Setup(s => s.KeyExists("TeamsProcesses")).Returns(true);
            var expectedProcesses = new List<string> { "custom_teams" };
            _mockSettings.Setup(s => s.GetStringList("TeamsProcesses", It.IsAny<List<string>>()))
                .Returns(expectedProcesses);

            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert
            var handled = _plugin.GetHandledProcesses().ToList();
            Assert.Contains("custom_teams", handled);
            Assert.Single(handled);
        }

        [Fact]
        public void Initialize_SetsDefaults_IfSettingsMissing()
        {
            // Arrange
            _mockSettings.Setup(s => s.KeyExists("TeamsProcesses")).Returns(false);

            // Act
            _plugin.Initialize(_mockContext.Object);

            // Assert
            _mockSettings.Verify(s => s.SetStringList("TeamsProcesses", It.IsAny<List<string>>()), Times.Once);
            var handled = _plugin.GetHandledProcesses().ToList();
            Assert.Contains("ms-teams", handled);
        }

        [Theory]
        [InlineData("Chat John Doe Available", "John Doe", "Individual", false)]
        [InlineData("Chat Jane Smith Busy", "Jane Smith", "Individual", false)]
        [InlineData("Chat Boss Man Do not disturb", "Boss Man", "Individual", false)]
        [InlineData("Chat Colleague Appear offline", "Colleague", "Individual", false)]
        [InlineData("Chat Pinner Has pinned", "Pinner", "Individual", false)]
        public void ParseChatName_IndividualChats_ParsesCorrectly(string raw, string expectedName, string expectedType, bool expectedUnread)
        {
            var result = _plugin.ParseChatName(raw);
            Assert.NotNull(result);
            Assert.Equal(expectedName, result.Value.Name);
            Assert.Equal(expectedType, result.Value.Type);
            Assert.Equal(expectedUnread, result.Value.IsUnread);
        }

        [Theory]
        [InlineData("Group chat Project Alpha Last message yesterday", "Project Alpha", "Group", false)]
        [InlineData("Group chat Lunch Crew Last message 10:00 AM", "Lunch Crew", "Group", false)]
        public void ParseChatName_GroupChats_ParsesCorrectly(string raw, string expectedName, string expectedType, bool expectedUnread)
        {
            var result = _plugin.ParseChatName(raw);
            Assert.NotNull(result);
            Assert.Equal(expectedName, result.Value.Name);
            Assert.Equal(expectedType, result.Value.Type);
            Assert.Equal(expectedUnread, result.Value.IsUnread);
        }

        [Theory]
        [InlineData("Meeting chat Weekly Sync Last message Tuesday", "Weekly Sync", "Meeting", false)]
        [InlineData("Meeting chat Standup Last message 9:15 AM", "Standup", "Meeting", false)]
        public void ParseChatName_MeetingChats_ParsesCorrectly(string raw, string expectedName, string expectedType, bool expectedUnread)
        {
            var result = _plugin.ParseChatName(raw);
            Assert.NotNull(result);
            Assert.Equal(expectedName, result.Value.Name);
            Assert.Equal(expectedType, result.Value.Type);
            Assert.Equal(expectedUnread, result.Value.IsUnread);
        }

        [Theory]
        [InlineData("Unread message Chat Alice Available", "Alice", "Individual", true)]
        [InlineData("Unread message Group chat Dev Team Last message now", "Dev Team", "Group", true)]
        [InlineData("Unread message Meeting chat All Hands Last message now", "All Hands", "Meeting", true)]
        public void ParseChatName_UnreadMessages_DetectedCorrectly(string raw, string expectedName, string expectedType, bool expectedUnread)
        {
            var result = _plugin.ParseChatName(raw);
            Assert.NotNull(result);
            Assert.Equal(expectedName, result.Value.Name);
            Assert.Equal(expectedType, result.Value.Type);
            Assert.Equal(expectedUnread, result.Value.IsUnread);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Random Text")]
        [InlineData("Chat without status")]
        [InlineData("Group chat incomplete")]
        public void ParseChatName_InvalidInput_ReturnsNull(string raw)
        {
            var result = _plugin.ParseChatName(raw);
            Assert.Null(result);
        }
    }
}
