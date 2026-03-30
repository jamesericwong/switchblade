using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using SwitchBlade.Core;

namespace SwitchBlade.Tests
{
    public static class TestMocks
    {
        public static IWindowOrchestrationService CreateMockOrchestrationService(IEnumerable<WindowItem>? windows = null)
        {
            var mock = new Mock<IWindowOrchestrationService>();
            mock.Setup(o => o.AllWindows).Returns(windows?.ToList() ?? new List<WindowItem>());
            mock.Setup(o => o.RefreshAsync(It.IsAny<ISet<string>>())).Returns(Task.CompletedTask);
            return mock.Object;
        }

        public static IWindowSearchService CreateMockSearchService(IEnumerable<WindowItem>? results = null)
        {
            var mock = new Mock<IWindowSearchService>();
            if (results != null)
            {
                mock.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(results.ToList());
            }
            else
            {
                mock.Setup(s => s.Search(It.IsAny<IEnumerable<WindowItem>>(), It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns((IEnumerable<WindowItem> items, string q, bool f) => items != null ? items.ToList() : new List<WindowItem>());
            }
            return mock.Object;
        }

        public static ISettingsService CreateMockSettingsService(UserSettings? settings = null)
        {
            var mock = new Mock<ISettingsService>();
            mock.Setup(s => s.Settings).Returns(settings ?? new UserSettings());
            return mock.Object;
        }
    }

    public class SynchronousDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => action();
        public Task InvokeAsync(Func<Task> action) => action();
    }
}
