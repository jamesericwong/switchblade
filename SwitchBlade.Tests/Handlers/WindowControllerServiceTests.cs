using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwitchBlade.Handlers;
using SwitchBlade.Services;
using SwitchBlade.ViewModels;
using SwitchBlade.Contracts;

namespace SwitchBlade.Tests.Handlers
{
    public class WindowControllerServiceTests
    {
        private static readonly IntPtr TestHwnd = new(0x1234);
        private static readonly IntPtr OwnerHwnd = new(0x9876);

        // ---------- Test doubles ----------

        private sealed class FakeWindowSurface : IWindowSurface
        {
            public IntPtr Handle => TestHwnd;
            public bool IsVisible { get; set; }
            public double Opacity { get; set; } = 1.0;
            public List<string> Sequence { get; } = [];

            private Action? _pendingAnimationCallback;
            public (double From, double To, int DurationMs)? LastAnimation { get; private set; }

            public void Show() => Sequence.Add("Show");
            public void Hide() => Sequence.Add("Hide");
            public void Activate() => Sequence.Add("Activate");
            public void NormalizeState() => Sequence.Add("NormalizeState");
            public void FocusSearchInput() => Sequence.Add("FocusSearchInput");

            public void AnimateOpacity(double from, double to, int durationMs, Action? onCompleted)
            {
                LastAnimation = (from, to, durationMs);
                _pendingAnimationCallback = onCompleted;
            }

            /// <summary>Simulates the WPF animation completing so tests can verify the completion callback wiring.</summary>
            public void CompletePendingAnimation() => _pendingAnimationCallback?.Invoke();
        }

        private sealed class FakeStyleInterop : IWindowStyleInterop
        {
            public List<(int Attribute, int Value)> DwmCalls { get; } = [];
            public Dictionary<IntPtr, int> ExStyles { get; } = new();
            public Dictionary<IntPtr, int> Styles { get; } = new();
            public List<(IntPtr Hwnd, int Index, IntPtr Value)> SetCalls { get; } = [];
            public IntPtr Owner { get; set; } = IntPtr.Zero;

            public void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value) => DwmCalls.Add((attribute, value));

            public IntPtr GetWindowLongPtr(IntPtr hwnd, int index) =>
                (index == NativeInterop.GWL_EXSTYLE ? ExStyles : Styles).GetValueOrDefault(hwnd);

            public void SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) => SetCalls.Add((hwnd, index, value));

            public IntPtr GetWindow(IntPtr hwnd, uint cmd) => Owner;
        }

        private sealed class FakeBadgeAnimator : IBadgeAnimator
        {
            public List<WindowItem> AnimatedItems { get; } = [];
            public void Animate(WindowItem item, int delayMs, int durationMs, double startingOffsetX) => AnimatedItems.Add(item);
        }

        /// <summary>Records requested delays (so debounce vs skip-debounce is observable) without actually waiting.</summary>
        private sealed class RecordingDelayProvider : IDelayProvider
        {
            public List<int> Delays { get; } = [];
            public Task Delay(int millisecondsDelay, CancellationToken cancellationToken = default)
            {
                Delays.Add(millisecondsDelay);
                return Task.CompletedTask;
            }
        }

        // ---------- Test context ----------

        private sealed class TestContext
        {
            public FakeWindowSurface Surface { get; } = new();
            public FakeStyleInterop Style { get; } = new();
            public UserSettings Settings { get; } = new();
            public Mock<ISettingsService> SettingsMock { get; }
            public MainViewModel ViewModel { get; }
            public Mock<IWindowOrchestrationService> Orchestration { get; }
            public Mock<IWindowInterop> WindowInterop { get; }
            public FakeBadgeAnimator Animator { get; } = new();
            public RecordingDelayProvider Delays { get; } = new();
            public BadgeAnimationService Badges { get; }
            public bool ModalOpen { get; set; }
            public WindowControllerService Controller { get; }

            public TestContext()
            {
                SettingsMock = new Mock<ISettingsService>();
                SettingsMock.Setup(s => s.Settings).Returns(Settings);

                Orchestration = new Mock<IWindowOrchestrationService>();
                Orchestration.Setup(o => o.AllWindows).Returns(new List<WindowItem>());
                Orchestration.Setup(o => o.RefreshAsync(It.IsAny<ISet<string>>())).Returns(Task.CompletedTask);

                var search = TestMocks.CreateMockSearchService();
                var navigation = new Mock<INavigationService>().Object;
                ViewModel = new MainViewModel(Orchestration.Object, search, navigation, SettingsMock.Object, new SynchronousDispatcherService());

                WindowInterop = new Mock<IWindowInterop>();

                // Real badge service with recording seams; zero out timing so tests stay instant.
                Badges = new BadgeAnimationService(Animator, Delays);
                Badges.StaggerDelayMs = 0;
                Badges.AnimationDurationMs = 0;

                Controller = new WindowControllerService(
                    Surface,
                    SettingsMock.Object,
                    new SynchronousDispatcherService(),
                    Mock.Of<ILogger>(),
                    ViewModel,
                    () => ModalOpen,
                    Style,
                    WindowInterop.Object);
            }

            public TestContext WithBadges()
            {
                Controller.SetBadgeAnimationService(Badges);
                return this;
            }

            public static WindowItem Item(string title, int shortcutIndex = -1) => new()
            {
                Hwnd = new IntPtr(0x100),
                Title = title,
                ShortcutIndex = shortcutIndex
            };
        }

        // ---------- Constructor guards ----------

        private static MainViewModel CreateViewModel()
        {
            var orchestration = new Mock<IWindowOrchestrationService>();
            orchestration.Setup(o => o.AllWindows).Returns(new List<WindowItem>());
            return new MainViewModel(orchestration.Object, TestMocks.CreateMockSearchService(), new Mock<INavigationService>().Object);
        }

        private static (ISettingsService Settings, IDispatcherService Dispatcher, ILogger Logger, MainViewModel ViewModel, Func<bool> ModalOpen, IWindowStyleInterop StyleInterop, IWindowInterop WindowInterop) ValidCtorArgs() =>
            (Mock.Of<ISettingsService>(), new SynchronousDispatcherService(), Mock.Of<ILogger>(), CreateViewModel(), () => false, new FakeStyleInterop(), Mock.Of<IWindowInterop>());

        [Fact]
        public void Ctor_NullSurface_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(null!, args.Settings, args.Dispatcher, args.Logger, args.ViewModel, args.ModalOpen, args.StyleInterop, args.WindowInterop));
            Assert.Equal("surface", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullSettingsService_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), null!, args.Dispatcher, args.Logger, args.ViewModel, args.ModalOpen, args.StyleInterop, args.WindowInterop));
            Assert.Equal("settingsService", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullDispatcherService_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, null!, args.Logger, args.ViewModel, args.ModalOpen, args.StyleInterop, args.WindowInterop));
            Assert.Equal("dispatcherService", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullLogger_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, args.Dispatcher, null!, args.ViewModel, args.ModalOpen, args.StyleInterop, args.WindowInterop));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullViewModel_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, args.Dispatcher, args.Logger, null!, args.ModalOpen, args.StyleInterop, args.WindowInterop));
            Assert.Equal("viewModel", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullModalOpenCallback_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, args.Dispatcher, args.Logger, args.ViewModel, null!, args.StyleInterop, args.WindowInterop));
            Assert.Equal("isModalDialogOpen", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullStyleInterop_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, args.Dispatcher, args.Logger, args.ViewModel, args.ModalOpen, null!, args.WindowInterop));
            Assert.Equal("styleInterop", ex.ParamName);
        }

        [Fact]
        public void Ctor_NullWindowInterop_ThrowsArgumentNullException()
        {
            var args = ValidCtorArgs();
            var ex = Assert.Throws<ArgumentNullException>(() => new WindowControllerService(new FakeWindowSurface(), args.Settings, args.Dispatcher, args.Logger, args.ViewModel, args.ModalOpen, args.StyleInterop, null!));
            Assert.Equal("windowInterop", ex.ParamName);
        }

        // ---------- ApplyBackdrop ----------

        [Fact]
        public void ApplyBackdrop_DarkTheme_SetsDarkModeMicaAndRoundedCorners()
        {
            var ctx = new TestContext();
            ctx.Settings.CurrentTheme = "Midnight Dark";

            ctx.Controller.ApplyBackdrop(TestHwnd);

            Assert.Contains((NativeInterop.DWMWA_USE_IMMERSIVE_DARK_MODE, 1), ctx.Style.DwmCalls);
            Assert.Contains((NativeInterop.DWMWA_SYSTEMBACKDROP_TYPE, NativeInterop.DWM_BACKDROP_MICA), ctx.Style.DwmCalls);
            Assert.Contains((NativeInterop.DWMWA_WINDOW_CORNER_PREFERENCE, NativeInterop.DWMWCP_ROUND), ctx.Style.DwmCalls);
        }

        [Fact]
        public void ApplyBackdrop_LightTheme_DisablesDarkMode()
        {
            var ctx = new TestContext(); // default theme "Super Light"

            ctx.Controller.ApplyBackdrop(TestHwnd);

            Assert.Contains((NativeInterop.DWMWA_USE_IMMERSIVE_DARK_MODE, 0), ctx.Style.DwmCalls);
        }

        // ---------- ConfigureWindowStyles ----------

        [Fact]
        public void ConfigureWindowStyles_NeedsChanges_OwnerNeedsToolwindow_AppliesAll()
        {
            var ctx = new TestContext();
            ctx.Style.ExStyles[TestHwnd] = 0; // no WS_EX_APPWINDOW yet
            ctx.Style.Styles[TestHwnd] = NativeInterop.WS_CAPTION | NativeInterop.WS_SYSMENU;
            ctx.Style.Owner = OwnerHwnd;
            ctx.Style.ExStyles[OwnerHwnd] = 0; // owner lacks WS_EX_TOOLWINDOW

            ctx.Controller.ConfigureWindowStyles(TestHwnd);

            Assert.Contains((TestHwnd, NativeInterop.GWL_EXSTYLE, (IntPtr)NativeInterop.WS_EX_APPWINDOW), ctx.Style.SetCalls);
            Assert.Contains((TestHwnd, NativeInterop.GWL_STYLE, IntPtr.Zero), ctx.Style.SetCalls); // both chrome styles cleared
            var ownerSet = ctx.Style.SetCalls.Single(c => c.Hwnd == OwnerHwnd);
            Assert.Equal(NativeInterop.WS_EX_TOOLWINDOW, (int)ownerSet.Value & NativeInterop.WS_EX_TOOLWINDOW);
        }

        [Fact]
        public void ConfigureWindowStyles_AllCorrect_NoOwner_MakesNoChanges()
        {
            var ctx = new TestContext();
            ctx.Style.ExStyles[TestHwnd] = NativeInterop.WS_EX_APPWINDOW;
            ctx.Style.Styles[TestHwnd] = 0;

            ctx.Controller.ConfigureWindowStyles(TestHwnd);

            Assert.Empty(ctx.Style.SetCalls);
        }

        [Fact]
        public void ConfigureWindowStyles_OwnerAlreadyToolwindow_SkipsOwnerChange()
        {
            var ctx = new TestContext();
            ctx.Style.ExStyles[TestHwnd] = NativeInterop.WS_EX_APPWINDOW;
            ctx.Style.Styles[TestHwnd] = 0;
            ctx.Style.Owner = OwnerHwnd;
            ctx.Style.ExStyles[OwnerHwnd] = NativeInterop.WS_EX_TOOLWINDOW;

            ctx.Controller.ConfigureWindowStyles(TestHwnd);

            Assert.DoesNotContain(ctx.Style.SetCalls, c => c.Hwnd == OwnerHwnd);
        }

        // ---------- FadeIn / FadeOut ----------

        [Fact]
        public void FadeIn_PositiveDuration_AnimatesFromZeroToTarget()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 200;
            ctx.Settings.WindowOpacity = 0.9;

            ctx.Controller.FadeIn();

            Assert.Equal((0.0, 0.9, 200), ctx.Surface.LastAnimation);
        }

        [Fact]
        public void FadeIn_ZeroDuration_SetsOpacityDirectly()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;
            ctx.Settings.WindowOpacity = 0.75;

            ctx.Controller.FadeIn();

            Assert.Equal(0.75, ctx.Surface.Opacity);
            Assert.Null(ctx.Surface.LastAnimation);
        }

        [Fact]
        public void FadeOut_VisibleWithDuration_AnimatesThenRunsCallback()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 200;
            ctx.Surface.Opacity = 0.8;

            bool completed = false;
            ctx.Controller.FadeOut(() => completed = true);

            Assert.False(completed); // deferred until the animation completes
            Assert.Equal((0.8, 0.0, 200), ctx.Surface.LastAnimation);

            ctx.Surface.CompletePendingAnimation();
            Assert.True(completed);
        }

        [Fact]
        public void FadeOut_ZeroDuration_CompletesImmediately()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;
            ctx.Surface.Opacity = 0.8;

            bool completed = false;
            ctx.Controller.FadeOut(() => completed = true);

            Assert.True(completed);
            Assert.Null(ctx.Surface.LastAnimation);
        }

        [Fact]
        public void FadeOut_AlreadyTransparent_CompletesImmediately()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 200;
            ctx.Surface.Opacity = 0;

            bool completed = false;
            ctx.Controller.FadeOut(() => completed = true);

            Assert.True(completed);
            Assert.Null(ctx.Surface.LastAnimation);
        }

        // ---------- ToggleVisibility ----------

        [Fact]
        public void ToggleVisibility_ModalDialogOpen_SuppressesToggle()
        {
            var ctx = new TestContext();
            ctx.ModalOpen = true;
            ctx.Settings.FadeDurationMs = 0;

            ctx.Controller.ToggleVisibility();

            Assert.Empty(ctx.Surface.Sequence);
            ctx.WindowInterop.Verify(i => i.ForceForegroundWindow(It.IsAny<IntPtr>()), Times.Never);
        }

        [Fact]
        public void ToggleVisibility_WindowVisible_HidesAfterFadeOut()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;
            ctx.Surface.IsVisible = true;

            ctx.Controller.ToggleVisibility();

            Assert.Contains("Hide", ctx.Surface.Sequence);
        }

        [Fact]
        public void ToggleVisibility_WindowHidden_ForceOpens()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            ctx.Controller.ToggleVisibility();

            Assert.Contains("Show", ctx.Surface.Sequence);
            ctx.WindowInterop.Verify(i => i.ForceForegroundWindow(TestHwnd), Times.Once);
        }

        // ---------- ForceOpen ----------

        [Fact]
        public void ForceOpen_ExecutesFullPresentationSequence()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Settings.FadeDurationMs = 200;

            var withShortcut = TestContext.Item("Chrome", shortcutIndex: 0);
            withShortcut.HasBeenAnimated = true;
            var plain = TestContext.Item("Notepad"); // no shortcut
            ctx.ViewModel.FilteredWindows.Add(withShortcut);
            ctx.ViewModel.FilteredWindows.Add(plain);

            ctx.Controller.ForceOpen();

            // Presentation sequence on the surface
            Assert.Equal(0.0, ctx.Surface.Opacity); // starts transparent for fade-in
            var seq = ctx.Surface.Sequence;
            Assert.Contains("Show", seq);
            Assert.Contains("NormalizeState", seq);
            Assert.Contains("Activate", seq);
            Assert.Contains("FocusSearchInput", seq);
            Assert.True(seq.IndexOf("Show") < seq.IndexOf("Activate"));

            ctx.WindowInterop.Verify(i => i.ForceForegroundWindow(TestHwnd), Times.Once);

            // Badge state: service-level reset + per-item reset for shortcut items only
            Assert.False(withShortcut.HasBeenAnimated);
            Assert.Equal(-20.0, withShortcut.BadgeTranslateX); // ResetBadgeAnimation applied
            Assert.Equal(0.0, plain.BadgeTranslateX);          // non-shortcut item untouched

            Assert.Equal("", ctx.ViewModel.SearchText);

            // Fade-in kicked off toward the configured opacity
            Assert.Equal((0.0, 1.0, 200), ctx.Surface.LastAnimation);

            // Async tail refreshed the window list
            ctx.Orchestration.Verify(o => o.RefreshAsync(It.IsAny<ISet<string>>()), Times.Once);
        }

        [Fact]
        public void ForceOpen_ClearsSearchText()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Settings.FadeDurationMs = 0;
            ctx.ViewModel.SearchText = "abc";

            ctx.Controller.ForceOpen();

            Assert.Equal("", ctx.ViewModel.SearchText);
        }

        [Fact]
        public void ForceOpen_BadgeAnimationsDisabled_ForceBadgesVisible()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Settings.FadeDurationMs = 0;
            ctx.Settings.EnableBadgeAnimations = false;

            var item = TestContext.Item("Chrome", shortcutIndex: 0);
            item.BadgeOpacity = 0;
            ctx.ViewModel.FilteredWindows.Add(item);

            ctx.Controller.ForceOpen();

            Assert.Equal(1.0, item.BadgeOpacity); // forced visible by the async tail
            Assert.Equal(0, item.BadgeTranslateX);
        }

        // ---------- InitialLoadAsync ----------

        [Fact]
        public async Task InitialLoadAsync_AnimationsEnabled_ResetsStateAndSelectsFirst()
        {
            var ctx = new TestContext().WithBadges();
            var first = TestContext.Item("Chrome", shortcutIndex: 0);
            first.HasBeenAnimated = true;
            var second = TestContext.Item("Notepad");
            ctx.ViewModel.FilteredWindows.Add(first);
            ctx.ViewModel.FilteredWindows.Add(second);

            await ctx.Controller.InitialLoadAsync();

            Assert.False(first.HasBeenAnimated); // reset applied at start of load
            ctx.Orchestration.Verify(o => o.RefreshAsync(It.IsAny<ISet<string>>()), Times.Once);
            Assert.Same(first, ctx.ViewModel.SelectedWindow); // selection settled to top
        }

        [Fact]
        public async Task InitialLoadAsync_AnimationsDisabled_ForceBadgesVisible()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Settings.EnableBadgeAnimations = false;

            var item = TestContext.Item("Chrome", shortcutIndex: 0);
            item.HasBeenAnimated = true; // must NOT be reset when animations are disabled
            item.BadgeOpacity = 0;
            ctx.ViewModel.FilteredWindows.Add(item);

            await ctx.Controller.InitialLoadAsync();

            Assert.True(item.HasBeenAnimated);
            Assert.Equal(1.0, item.BadgeOpacity);
        }

        [Fact]
        public async Task InitialLoadAsync_NoBadgeService_StillLoads()
        {
            var ctx = new TestContext(); // badge service never attached

            await ctx.Controller.InitialLoadAsync();

            Assert.Null(ctx.ViewModel.SelectedWindow);
            ctx.Orchestration.Verify(o => o.RefreshAsync(It.IsAny<ISet<string>>()), Times.Once);
        }

        [Fact]
        public async Task InitialLoadAsync_EmptyList_DoesNotSelectFirst()
        {
            var ctx = new TestContext().WithBadges();

            await ctx.Controller.InitialLoadAsync();

            Assert.Null(ctx.ViewModel.SelectedWindow);
        }

        // ---------- OnResultsUpdated / OnSearchTextChanged state machine ----------

        [Fact]
        public void OnResultsUpdated_PendingTextChange_ResetsAndDebounces()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Surface.IsVisible = true;

            var a = TestContext.Item("A", shortcutIndex: 0);
            var b = TestContext.Item("B", shortcutIndex: 1);
            var c = TestContext.Item("C"); // no shortcut
            foreach (var item in new[] { a, b, c })
            {
                item.HasBeenAnimated = true;
            }

            ctx.ViewModel.FilteredWindows.Add(a);
            ctx.ViewModel.FilteredWindows.Add(b);
            ctx.ViewModel.FilteredWindows.Add(c);

            ctx.Controller.OnSearchTextChanged(null, EventArgs.Empty);
            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            // Pending reset applied: items that were already 'animated' got re-queued and animated.
            // (Without the reset they would have been skipped as already-animated.)
            Assert.Equal(2, ctx.Animator.AnimatedItems.Count);
            Assert.Contains(a, ctx.Animator.AnimatedItems);
            Assert.Contains(b, ctx.Animator.AnimatedItems);
            Assert.DoesNotContain(c, ctx.Animator.AnimatedItems); // no shortcut -> never animated
            // Debounce path taken (default 75ms) — not skipped
            Assert.Contains(75, ctx.Delays.Delays);
        }

        [Fact]
        public void OnResultsUpdated_ForceOpenPending_SkipsDebounce()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Surface.IsVisible = true;
            ctx.Settings.FadeDurationMs = 0;

            var a = TestContext.Item("A", shortcutIndex: 0);
            ctx.ViewModel.FilteredWindows.Add(a);

            ctx.Controller.OnSearchTextChanged(null, EventArgs.Empty); // pending text change...
            ctx.Controller.ForceOpen();                                 // ...then force-open overrides it
            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            Assert.DoesNotContain(75, ctx.Delays.Delays); // debounce skipped despite the pending reset
        }

        [Fact]
        public void OnResultsUpdated_NoPending_TriggersWithoutDebounce()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Surface.IsVisible = true;

            var a = TestContext.Item("A", shortcutIndex: 0);
            ctx.ViewModel.FilteredWindows.Add(a);

            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            Assert.DoesNotContain(75, ctx.Delays.Delays);
            Assert.Single(ctx.Animator.AnimatedItems);
        }

        [Fact]
        public void OnResultsUpdated_WindowHidden_ResetsButDoesNotAnimate()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Surface.IsVisible = false;

            var a = TestContext.Item("A", shortcutIndex: 0);
            a.HasBeenAnimated = true;
            ctx.ViewModel.FilteredWindows.Add(a);

            ctx.Controller.OnSearchTextChanged(null, EventArgs.Empty);
            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            Assert.False(a.HasBeenAnimated); // pending reset applies regardless of visibility
            Assert.Empty(ctx.Animator.AnimatedItems); // no stagger while hidden
        }

        [Fact]
        public void OnResultsUpdated_NoBadgeService_DoesNothing()
        {
            var ctx = new TestContext(); // badge service never attached
            ctx.Surface.IsVisible = true;

            var a = TestContext.Item("A", shortcutIndex: 0);
            a.HasBeenAnimated = true;
            ctx.ViewModel.FilteredWindows.Add(a);

            ctx.Controller.OnSearchTextChanged(null, EventArgs.Empty);
            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            Assert.True(a.HasBeenAnimated); // reset branch skipped without the service
            Assert.Empty(ctx.Animator.AnimatedItems);
        }

        [Fact]
        public void OnResultsUpdated_AnimationsDisabled_ForceBadgesVisible()
        {
            var ctx = new TestContext().WithBadges();
            ctx.Surface.IsVisible = true;
            ctx.Settings.EnableBadgeAnimations = false;

            var a = TestContext.Item("A", shortcutIndex: 0);
            a.BadgeOpacity = 0;
            ctx.ViewModel.FilteredWindows.Add(a);

            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);

            Assert.Equal(1.0, a.BadgeOpacity);
            Assert.Equal(0.0, a.BadgeTranslateX);
        }

        // ---------- ActivateWindow ----------

        [Fact]
        public void ActivateWindow_NullItem_NoOp()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            ctx.Controller.ActivateWindow(null);

            Assert.Empty(ctx.Surface.Sequence);
            ctx.WindowInterop.Verify(i => i.SetForegroundWindow(It.IsAny<IntPtr>()), Times.Never);
        }

        [Fact]
        public void ActivateWindow_WithSource_ActivatesViaProviderAndHides()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            var providerMock = new Mock<IWindowProvider>();
            var item = TestContext.Item("Chrome");
            item.Source = providerMock.Object;

            ctx.Controller.ActivateWindow(item);

            providerMock.Verify(p => p.ActivateWindow(item), Times.Once);
            Assert.Contains("Hide", ctx.Surface.Sequence);
        }

        [Fact]
        public void ActivateWindow_WithoutSource_RestoresIconicAndForegrounds()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            var item = TestContext.Item("Chrome");
            ctx.WindowInterop.Setup(i => i.IsIconic(item.Hwnd)).Returns(true);

            ctx.Controller.ActivateWindow(item);

            ctx.WindowInterop.Verify(i => i.ShowWindow(item.Hwnd, NativeInterop.SW_RESTORE), Times.Once);
            ctx.WindowInterop.Verify(i => i.SetForegroundWindow(item.Hwnd), Times.Once);
            Assert.Contains("Hide", ctx.Surface.Sequence);
        }

        [Fact]
        public void ActivateWindow_WithoutSource_NotIconic_SkipsRestore()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            var item = TestContext.Item("Chrome");
            ctx.WindowInterop.Setup(i => i.IsIconic(item.Hwnd)).Returns(false);

            ctx.Controller.ActivateWindow(item);

            ctx.WindowInterop.Verify(i => i.ShowWindow(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.Never);
            ctx.WindowInterop.Verify(i => i.SetForegroundWindow(item.Hwnd), Times.Once);
        }

        [Fact]
        public void ActivateWindow_FallbackThrows_LogsErrorAndStillHides()
        {
            var ctx = new TestContext();
            ctx.Settings.FadeDurationMs = 0;

            var item = TestContext.Item("Chrome");
            ctx.WindowInterop.Setup(i => i.IsIconic(item.Hwnd)).Throws<InvalidOperationException>();

            ctx.Controller.ActivateWindow(item);

            Assert.Contains("Hide", ctx.Surface.Sequence); // hide still happens after the failure
        }

        [Fact]
        public void OnResultsUpdated_PendingResetWithoutBadgeService_SkipsReset()
        {
            var ctx = new TestContext(); // BadgeAnimationService never attached -> null badge service path
            ctx.Controller.OnSearchTextChanged(null, EventArgs.Empty); // arms the pending animation reset

            ctx.Controller.OnResultsUpdated(null, EventArgs.Empty);    // guard must short-circuit on the null badge service

            Assert.Empty(ctx.Animator.AnimatedItems);                  // nothing routed through the absent service
        }
    }
}
