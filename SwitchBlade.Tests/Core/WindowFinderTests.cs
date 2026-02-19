using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Core;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Core
{
    public class WindowFinderTests
    {
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IWindowInterop> _mockInterop;
        private readonly Mock<IPluginContext> _mockContext;
        private readonly Mock<ILogger> _mockLogger;

        public WindowFinderTests()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.Settings).Returns(new UserSettings());
            
            _mockInterop = new Mock<IWindowInterop>();
            _mockLogger = new Mock<ILogger>();
            _mockContext = new Mock<IPluginContext>();
            _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        }

        private WindowFinder CreateFinder()
        {
            var finder = new WindowFinder(_mockSettingsService.Object, _mockInterop.Object);
            finder.Initialize(_mockContext.Object);
            return finder;
        }

        [Fact]
        public void Connect_ShouldHaveCorrectMetadata()
        {
            var finder = CreateFinder();
            Assert.Equal("WindowFinder", finder.PluginName);
            Assert.False(finder.HasSettings);
            Assert.False(finder.IsUiaProvider);
        }

        [Fact]
        public void ReloadSettings_ShouldNotThrow()
        {
            var finder = CreateFinder();
            finder.ReloadSettings();
        }

        [Fact]
        public void SetExclusions_ShouldUpdateExclusions()
        {
            var finder = CreateFinder();
            finder.SetExclusions(new[] { "ExcludedApp" });
        }

        [Fact]
        public void ActivateWindow_ShouldCallInterop()
        {
            var finder = CreateFinder();
            var window = new WindowItem { Hwnd = (IntPtr)12345 };

            finder.ActivateWindow(window);

            _mockInterop.Verify(x => x.ForceForegroundWindow((IntPtr)12345), Times.Once);
        }

        [Fact]
        public void GetWindows_ShouldReturnWindows_WhenInteropEnumerates()
        {
             // Arrange
            var hWnd = (IntPtr)0x100;
            var windowTitle = "Test Window";
            var processName = "notepad";

            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
                .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) =>
                {
                    callback(hWnd, param);
                });

            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);
            
            // Mock GetWindowText
            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                    var chars = windowTitle.ToCharArray();
                    Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                    return chars.Length;
                });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny))
                .Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => 
                {
                     pid = 100;
                }));
            
            _mockInterop.Setup(x => x.GetProcessInfo(100)).Returns((processName, "C:\\Windows\\notepad.exe"));

            var finder = CreateFinder();

            // Act
            var results = finder.GetWindows();

            // Assert
            Assert.Single(results);
            var item = results.First();
            Assert.Equal("Test Window", item.Title);
            Assert.Equal("notepad", item.ProcessName);
            Assert.Equal(hWnd, item.Hwnd);
        }

        // Delegate for out parameter callback
        delegate void WindowThreadProcessIdCallback(IntPtr hWnd, out uint lpdwProcessId);

        [Fact]
        public void GetWindows_ShouldFilterInvisibleWindows()
        {
            var hWnd = (IntPtr)0x200;
            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(false);

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Empty(results);
        }

        [Fact]
        public void GetWindows_ShouldFilterEmptyTitles()
        {
            var hWnd = (IntPtr)0x300;
            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);
            
            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>())).Returns(0);

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Empty(results);
        }

        [Fact]
        public void GetWindows_ShouldFilterProgramManager()
        {
            var hWnd = (IntPtr)0x400;
            var title = "Program Manager";
            
            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
             _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);

            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Empty(results);
        }

        [Fact]
        public void GetWindows_ShouldHandleProgramManagerLikeTitles_ThatAreNotProgramManager()
        {
            // Title length 15, starts with "Program ", but NOT "Program Manager"
            // "Program ManageX"
            var hWnd = (IntPtr)0x550;
            var title = "Program ManageX"; // Length 15, mismatch at end
            
            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
             _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);

            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => pid = 100));
            _mockInterop.Setup(x => x.GetProcessInfo(100)).Returns(("notepad", null));

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Single(results);
            Assert.Equal(title, results.First().Title);
        }

        [Fact]
        public void GetWindows_ShouldFilterExcludedProcesses()
        {
             var hWnd = (IntPtr)0x500;
             var title = "Excluded Window";
             var process = "bad_process";

             _mockSettingsService.Setup(s => s.Settings).Returns(new UserSettings 
             { 
                 ExcludedProcesses = new List<string> { process } 
             });

            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);
            
            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => pid = 500));
            _mockInterop.Setup(x => x.GetProcessInfo(500)).Returns((process, null));

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Empty(results);
        }

        [Fact]
        public void GetWindows_ShouldFilterDynamicExclusions()
        {
             var hWnd = (IntPtr)0x600;
             var title = "Dynamic Excluded Window";
             var process = "dynamic_bad";

            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);
            
            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                      var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => pid = 600));
            _mockInterop.Setup(x => x.GetProcessInfo(600)).Returns((process, null));

            var finder = CreateFinder();
            finder.SetExclusions(new[] { process });
            var results = finder.GetWindows();

            Assert.Empty(results);
        }

        [Fact]
        public void GetWindows_ShouldHandleProcessInfoExceptions()
        {
            var hWnd = (IntPtr)0x700;
            var title = "Error Window";

            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);

            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Throws(new Exception("Access Denied"));

            var finder = CreateFinder();
            var results = finder.GetWindows();

            // Should default to "Window" process and still return it if not excluded
            Assert.Single(results);
            Assert.Equal("Window", results.First().ProcessName);
        }

        [Fact]
        public void GetWindows_ShouldHandleTitleLength15_NotProgramManager()
        {
            var hWnd = (IntPtr)0x800;
            var title = "Not Progr Manag"; // Length 15
            
            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
             _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);

            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => pid = 100));
            _mockInterop.Setup(x => x.GetProcessInfo(100)).Returns(("notepad", null));

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Single(results);
            Assert.Equal("Not Progr Manag", results.First().Title);
        }

        [Fact]
        public void GetWindows_ShouldHandlePidZero()
        {
            var hWnd = (IntPtr)0x900;
            var title = "Pid Zero Window";

            _mockInterop.Setup(x => x.EnumWindows(It.IsAny<NativeInterop.EnumWindowsProc>(), It.IsAny<IntPtr>()))
               .Callback<NativeInterop.EnumWindowsProc, IntPtr>((callback, param) => callback(hWnd, param));
            _mockInterop.Setup(x => x.IsWindowVisible(hWnd)).Returns(true);

            _mockInterop.Setup(x => x.GetWindowTextUnsafe(hWnd, It.IsAny<IntPtr>(), It.IsAny<int>()))
                .Returns((IntPtr h, IntPtr buf, int max) =>
                {
                     var chars = title.ToCharArray();
                     Marshal.Copy(chars, 0, buf, Math.Min(chars.Length, max));
                     return chars.Length;
                 });

            _mockInterop.Setup(x => x.GetWindowThreadProcessId(hWnd, out It.Ref<uint>.IsAny)).Callback(new WindowThreadProcessIdCallback((IntPtr h, out uint pid) => pid = 0));

            var finder = CreateFinder();
            var results = finder.GetWindows();

            Assert.Single(results);
            Assert.Equal("Window", results.First().ProcessName);
        }

        [Fact]
        public void GetWindows_SettingsNull_ReturnsEmpty()
        {
            var finder = new WindowFinder(null, _mockInterop.Object);
            var results = finder.GetWindows();
            Assert.Empty(results);
        }

        [Fact]
        public void ProtectedMethods_ExceptionPaths_ReturnFallback()
        {
            // Testing protected methods via public entry points or inheritance if accessible, 
            // but WindowFinder exposes them as internal/protected.
            // Since we use CachingWindowProviderBase, we can test the overrides.
            
            var finder = new WindowFinderTestWrapper(_mockSettingsService.Object, _mockInterop.Object);
            
            // Test GetPid with exception
            _mockInterop.Setup(x => x.GetWindowThreadProcessId(It.IsAny<IntPtr>(), out It.Ref<uint>.IsAny)).Throws(new Exception());
            Assert.Equal(-1, finder.GetPidPublic(IntPtr.Zero));

            // Test GetProcessInfo with pid -1
            var (name1, path1) = finder.GetProcessInfoPublic(unchecked((uint)-1));
            Assert.Equal("Window", name1);
            Assert.Null(path1);

            // Test GetProcessInfo with exception
            _mockInterop.Setup(x => x.GetProcessInfo(123)).Throws(new Exception());
            var (name2, path2) = finder.GetProcessInfoPublic(123);
            Assert.Equal("Window", name2);
            Assert.Null(path2);
        }

        private class WindowFinderTestWrapper : WindowFinder
        {
            public WindowFinderTestWrapper(ISettingsService settings, IWindowInterop interop) : base(settings, interop) { }
            public int GetPidPublic(IntPtr hwnd) => base.GetPid(hwnd);
            public (string, string?) GetProcessInfoPublic(uint pid) => base.GetProcessInfo(pid);
        }

        [Fact]
        public void GetPid_ReturnsMinusOne_OnException()
        {
             var finder = new WindowFinderTestWrapper(_mockSettingsService.Object, _mockInterop.Object);
             _mockInterop.Setup(x => x.GetWindowThreadProcessId(It.IsAny<IntPtr>(), out It.Ref<uint>.IsAny)).Throws(new Exception());
             Assert.Equal(-1, finder.GetPidPublic(IntPtr.Zero));
        }

        [Fact]
        public void GetProcessInfo_ReturnsWindow_OnMinusOne()
        {
             var finder = new WindowFinderTestWrapper(_mockSettingsService.Object, _mockInterop.Object);
             var (name, path) = finder.GetProcessInfoPublic(unchecked((uint)-1));
             Assert.Equal("Window", name);
             Assert.Null(path);
        }

        [Fact]
        public void GetProcessInfo_ReturnsWindow_OnException()
        {
             var finder = new WindowFinderTestWrapper(_mockSettingsService.Object, _mockInterop.Object);
             _mockInterop.Setup(x => x.GetProcessInfo(123)).Throws(new Exception());
             var (name, path) = finder.GetProcessInfoPublic(123);
             Assert.Equal("Window", name);
             Assert.Null(path);
        }
    }
}
