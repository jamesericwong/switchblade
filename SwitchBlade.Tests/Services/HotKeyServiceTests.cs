using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Moq;
using SwitchBlade.Contracts;
using SwitchBlade.Services;
using Xunit;

namespace SwitchBlade.Tests.Services
{
    public class HotKeyServiceTests
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001; // Must match HotKeyService.HOTKEY_ID

        private readonly Mock<ISettingsService> _mockSettings = new();
        private readonly Mock<ILogger> _mockLogger = new();

        public HotKeyServiceTests()
        {
            _mockSettings.Setup(s => s.Settings).Returns(new UserSettings());
        }

        /// <summary>
        /// WPF requires an STA thread; xUnit test threads are MTA by default.
        /// </summary>
        private static void RunOnStaThread(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { action(); } catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        [Fact]
        public void HwndHook_HandlerThrows_DoesNotThrow()
        {
            RunOnStaThread(() =>
            {
                var service = new HotKeyService(
                    new System.Windows.Window(),
                    _mockSettings.Object,
                    _mockLogger.Object,
                    () => throw new InvalidOperationException("simulated handler failure"));

                try
                {
                    bool handled = false;

                    Assert.Null(Record.Exception(() =>
                        service.HwndHook(IntPtr.Zero, WM_HOTKEY, (IntPtr)HOTKEY_ID, IntPtr.Zero, ref handled)));

                    Assert.True(handled);
                    _mockLogger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
                }
                finally
                {
                    service.Dispose();
                }
            });
        }

        [Fact]
        public void HwndHook_NonHotKeyMessage_NotHandled()
        {
            RunOnStaThread(() =>
            {
                var service = new HotKeyService(
                    new System.Windows.Window(),
                    _mockSettings.Object,
                    _mockLogger.Object,
                    () => throw new InvalidOperationException("must not be called"));

                try
                {
                    bool handled = true; // Pre-set to detect accidental writes

                    Assert.Null(Record.Exception(() =>
                        service.HwndHook(IntPtr.Zero, 0x0100, (IntPtr)HOTKEY_ID, IntPtr.Zero, ref handled)));

                    Assert.True(handled); // Untouched - hook must not act on other messages
                }
                finally
                {
                    service.Dispose();
                }
            });
        }

        [Fact]
        public void Dispose_BeforeWindowLoaded_PreventsLateInitialization()
        {
            RunOnStaThread(() =>
            {
                var window = new System.Windows.Window();
                var service = new HotKeyService(window, _mockSettings.Object, _mockLogger.Object, () => { });
                service.Dispose();

                // A late Loaded event after dispose must be a no-op. Without the unsubscribe fix this
                // reaches InitializeHotKey, where HwndSource.FromHwnd(IntPtr.Zero) is null and AddHook throws NRE.
                Assert.Null(Record.Exception(() =>
                    window.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent))));
            });
        }
    }
}
