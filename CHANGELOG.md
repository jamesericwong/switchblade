## [1.5.2] - 2026-01-11
### Performance
- **Configurable Regex Caching**: Implemented an LRU (Least Recently Used) cache for compiled regex objects to improve search responsiveness during typing.
- **Improved Regex Safety**: Migrated to `RegexOptions.NonBacktracking` for user-provided patterns to guarantee linear-time execution and prevent ReDoS vulnerabilities.
- **Customizable Cache Size**: Added `RegexCacheSize` setting (default: 50) to control the memory footprint of the regex cache.

## [1.5.1] - 2026-01-11
### Performance
- **Zero-Allocation Window Discovery**: Implemented `stackalloc` and `unsafe` P/Invoke to eliminate string allocations during window scanning.
- **Modern Interop**: Migrated all Windows API calls to .NET 9 `[LibraryImport]` for reduced marshalling overhead.
- **Efficient Polling**: Replaced `System.Timers.Timer` with `PeriodicTimer` in `BackgroundPollingService` to remove lock contention and improve async correctness.
- **Reduced Memory Usage**: Optimized `WindowFinder` and `ThumbnailService` to minimize heap allocations.

## [1.5.0] - 2026-01-11

### Changed
- **Significant Memory Optimization**: Reduced application memory footprint.
  - **Native Process Lookups**: Replaced expensive `System.Diagnostics.Process` usage with lightweight native APIs (`OpenProcess` + `QueryFullProcessImageName`).
  - **Smart Caching**: Implemented a short-lived process name cache to eliminate redundant kernel calls during window scans.
  - **Garbage Collection**: Switched to `Workstation` GC mode and disabled VM retention to encourage more aggressive memory release.
- **Icon Loading Optimization**: Fixed a GDI handle leak in the application icon loader.

## [1.4.21] - 2026-01-11

### Added
- **"Super Light" Theme**: Introduced a new, even lighter theme with a pure white background (`#FFFFFF`) and refined contrast for maximum clarity.

### Changed
- **New Default Theme**: Set "Super Light" as the default theme for all new installations.
- **Global Style Refactoring**: Refactored Button and TextBox styles to use dynamic resource-based coloring.
  - Controls now automatically adapt their background and border colors based on the active theme.
  - Improved visibility and contrast for all interactive elements on light backgrounds.

### Fixed
- **Theme Selection Tests**: Updated `UserSettingsTests` to reflect the new default theme.

## [1.4.20] - 2026-01-11

### Added
- **Windows 11 Mica Backdrop**: Native Windows 11 backdrop effect with semi-transparent theme colors.
  - Uses DWM `DWMWA_SYSTEMBACKDROP_TYPE` for Mica integration.
  - Native rounded corners via `DWMWA_WINDOW_CORNER_PREFERENCE`.
  - Provides a modern, OS-integrated glassmorphism aesthetic.
- **Modern Typography**: Introduced "Inter" font family with fallbacks to Segoe UI and Roboto.
  - Base style applied globally to all controls for consistent appearance.
- **Modernized Settings Window**: Replaced `GroupBox` sections with card-based borders.
  - Cleaner visual hierarchy with semi-transparent backgrounds.
  - Added section header styles for improved readability.

### Changed
- **Theme Colors**: All theme brushes now use semi-transparent alpha values:
  - Window background: 85% opacity
  - Control background: 60% opacity
  - Borders: 30% opacity
  - Hover highlights: ~60% opacity for better contrast
- **Hover Effect Simplified**: Mouse hover on list items now only changes the background color without any scale transformation, providing consistent spacing across all interaction modes.
- **Native Window Chrome**: Integrated dragging into `WindowChrome` (28px caption height) for reliable window movement.
- **Refined Control Styles**: Updated Button, TextBox, CheckBox, and RadioButton styles with modern padding, focus animations, and cleaner borders.

### Fixed
- **Dynamic Window Titles Not Updating**: Fixed a bug where windows with frequently changing titles (e.g., bandwidth monitors like BiglyBT) would not display updated titles in SwitchBlade.
  - **Root cause 1**: The diff check compared both HWND and Title, so identical HWNDs with changed titles skipped the update path entirely.
  - **Root cause 2**: `WindowItem.Title` was an auto-property that didn't fire `PropertyChanged`, so in-place updates weren't reflected in the UI.
  - **Fix**: 
    - Split structural changes (windows added/removed) from title-only updates in `MainViewModel.RefreshWindows()`.
    - Made `WindowItem.Title` a notifying property that fires `PropertyChanged` on modification.
  - Titles now update in real-time without triggering badge re-animation.
- **Settings Window Crash**: Fixed XAML parse error when opening Settings caused by invalid style inheritance (`TextBlock` cannot inherit from `Control`-based style).
- **Hover Highlight Extending Too Far**: Removed the `ScaleTransform` from the hover effect so highlight dimensions match keyboard selection exactly.

## [1.4.19] - 2026-01-11

### Fixed
- **Inconsistent Scroll Position on Startup**: Fixed a bug where the search results list would sometimes start in the middle instead of at the top.
  - Implemented an explicit scroll-to-top after the initial provider load is complete.
  - Added scroll-to-top whenever the application is opened from the tray or hotkey to ensure a clean starting point.
- **SyncCollection Move Exception**: Added `.Distinct()` to the search result filtering logic to ensure duplicate object references don't cause an out-of-bounds error during UI synchronization.

### Changed
- **Selection Performance**: Optimized `SelectedWindow` property to only trigger UI update notifications when the selection actually changes, reducing redundant layout cycles.
- **Improved Code Documentation**: Added technical comments to `MainViewModel` explaining the interaction between selection state and list scrolling.

## [1.4.18] - 2026-01-11

### Changed
- **Always Re-animate**: Updated badge animation behavior to always play the "waterfall" effect whenever the search text is modified (typing, deleting, or clearing).
  - This replaces the previous "Uniform Snap" behavior on search clear, ensuring a consistent, dynamic feel across all interactions.

### Fixed
- **Search Clear Animation**: Fixed a bug where clearing the search via backspace would sometimes fail to re-animate the revealed windows (invisible badges).
  - **Root cause**: The animation reset was applied to the *old* filtered list instead of the *new* full list.
  - **Fix**: Implemented a **Deferred Reset** pattern to apply the animation state reset specifically to the newly updated list just before rendering.
- **Badge Animation Not Replaying**: Fixed a bug where windows with unchanged HWNDs would not re-animate when toggling the hotkey.
  - **Fix**: Added `item.ResetBadgeAnimation()` call in `BadgeAnimationService` to force reset visual state before animating.

## [1.4.17] - 2026-01-10

### Added
- **Staggered Badge Animations**: Alt+Number badges now animate with a smooth staggered fade-in and slide-in effect.
  - Badges animate in order: Alt+1 first → Alt+0 last (75ms stagger delay).
  - Animation: Fade from 0→1 opacity + Slide from -20px→0 offset (150ms duration, cubic ease-out).
  - HWND tracking prevents re-animation when window titles change but HWNDs remain the same.
  - Animations trigger on window show (startup/hotkey) and when search results update.
- **Enable Badge Animations Setting**: New toggle in Settings (enabled by default) to enable/disable the badge animations.
- **Preview Title Bar**: The preview panel now shows a "Previewing: [window title]" bar at the bottom with a semi-transparent dark background.

### Changed
- **List Item Spacing**: Increased right margin on list items for better visual separation from the scrollbar.

### Technical
- New `BadgeAnimationService.cs` coordinates animation state and timing.
- `WindowItem` extended with `BadgeOpacity`, `BadgeTranslateX`, and `ResetBadgeAnimation()` for animation support.
- `MainViewModel` extended with `ResultsUpdated` and `SearchTextChanged` events for animation triggers.

## [1.4.16] - 2026-01-06

### Fixed
- **Hotkey Not Working When Starting Minimized**: Fixed a bug where the global hotkey would not work if the application was started with the `/minimized` switch.
  - **Root cause**: The `HotKeyService` checked `Window.IsLoaded` which is only true after the window is shown, not just when the HWND exists.
  - **Solution**: 
    - `MainWindow` now calls `WindowInteropHelper.EnsureHandle()` in the constructor to create the window handle without showing it.
    - `HotKeyService` now checks for an existing HWND first (via `WindowInteropHelper.Handle != IntPtr.Zero`) before falling back to the `Loaded` event.
  - The hotkey now works immediately on startup, regardless of whether the window is visible.

## [1.4.15] - 2026-01-06

### Added
- **Notepad++ Plugin**: New plugin (`SwitchBlade.Plugins.NotepadPlusPlus`) that lists individual tabs within Notepad++ instances.
  - Uses UI Automation to discover and activate specific tabs within each Notepad++ window.
  - Follows the same pattern as Chrome and Windows Terminal plugins for consistency.
  - Includes settings dialog to configure which process names to scan (default: `notepad++`).
  - Settings UI matches the dark theme design of other plugin settings windows.

## [1.4.14] - 2026-01-04

### Fixed
- **Truly Rounded Corners**: Window corners are now genuinely rounded instead of square with grey corner fill.
  - Restructured window layout with nested borders: outer transparent border provides shadow space, inner border renders rounded content.
  - Removed the grey artifacts that appeared in the corner areas.
- **Thumbnail Preview Resize**: Window preview thumbnails now properly adjust when the application is resized.
  - Added `SizeChanged` event subscription in `ThumbnailService` to automatically update thumbnail positioning.
  - DWM thumbnail destination rectangle is now recalculated when the preview container size changes.
- **Window Resize Broken**: Fixed window resizing after structure changes for rounded corners.
  - Increased `ResizeBorderThickness` from 5 to 25 to account for the 20px margin added for shadow space.
- **Hotkey Spontaneously Changing**: Fixed a critical bug where the global hotkey could change to Ctrl+A unexpectedly.
  - **Root cause**: Settings window was opened non-modally with `.Show()`, allowing it to linger in background while still capturing keyboard input.
  - **Fix**: Changed to modal `.ShowDialog()` so the Settings window must be explicitly closed before returning to the main app.

## [1.4.13] - 2026-01-04

### Added
- **Moveable Window**: The SwitchBlade window can now be dragged to reposition it on screen.
  - Added a subtle drag bar at the top of the window with centered grip dots indicator (⋮⋮⋮).
  - Cursor changes to `SizeAll` on hover to indicate the draggable area.
  - Window corners remain rounded—the drag bar is contained within the existing border design.

### Fixed
- **Smart Centering on Startup**: Window now correctly centers on screen based on the persisted size.
  - Previously, `WindowStartupLocation="CenterScreen"` centered based on default size, then the saved size was applied, causing offset positioning.
  - Now calculates true center after applying saved dimensions using `SystemParameters.WorkArea`.

## [1.4.12] - 2026-01-04

### Changed
- **System Tray Toggle**: The tray menu "Show" item is now "Show / Hide" and toggles window visibility.
  - If the window is visible and active, clicking toggles it hidden.
  - If the window is hidden or inactive, clicking shows and activates it.
  - Double-clicking the tray icon also toggles visibility.
- **Startup DI Refactoring**: Moved DI container initialization from `App` to `Program.Main()`.
  - `Program.cs` now initializes `ServiceConfiguration.ConfigureServices()` early and resolves `ILogger` for all startup logging.
  - All static `Logger.Log()`/`Logger.LogError()` calls replaced with injected `ILogger` instance.
  - `App` constructor now accepts `IServiceProvider` from `Program.Main()`.

### Fixed
- **Keyboard Navigation & Auto-Selection**: Fixed a bug where pressing Up/Down arrow keys would not select any row when no item was selected, and typing a search term would not visually highlight the first row.
  - Pressing **Down** with no selection now selects the **first** item.
  - Pressing **Up** with no selection now selects the **last** item.
  - **Typing a search term now immediately selects and visually highlights the first matching row**, providing instant keyboard navigation feedback.
  - This matches standard app launcher behavior (Windows PowerToys Run, Alfred, Raycast).



## [1.4.11] - 2026-01-03

### Added
- **Dual-Purpose Installer**: The MSI installer now supports both per-user and per-machine installation modes.
  - Added a new Scope selection dialog to the installer.
  - **Per-User Mode**: Installs to `%LocalAppData%\Programs\SwitchBlade`. Does not require Administrator privileges.
  - **Per-Machine Mode**: Installs to `C:\Program Files\SwitchBlade`. Requires Administrator privileges (triggers UAC).
  - Both modes correctly handle shortcuts and startup settings.

### Fixed
- **Admin Toggle Restart**: Completely reworked the restart logic when toggling "Run as Administrator" setting.
  - **Mutex race condition**: The new process now waits for the old process to fully exit using PowerShell's `Wait-Process` before attempting to acquire the single-instance mutex.
  - **Setting persistence**: Added `Registry.Flush()` to ensure registry writes are committed before restart begins.
  - **Checkbox/state mismatch**: The setting is now only persisted after the user confirms the restart. Clicking "No" on the restart dialog now correctly reverts the checkbox.
  - **De-elevation support**: When turning admin OFF, uses `explorer.exe` to launch the new instance. This ensures the new process runs at the user's normal privilege level instead of inheriting elevation from the current process.

## [1.4.10] - 2026-01-03

### Fixed
- **Admin Restart Reliability**: Improved mutex acquisition to retry with a 2-second timeout during application restart.
  - Resolves "SwitchBlade is already running" error when toggling "Run as Administrator" from settings
  - New process now waits for the previous instance to complete shutdown before acquiring the mutex
  - Gracefully handles asynchronous shutdown timing issues

## [1.4.9] - 2026-01-03

### Added
- **Windows Terminal Plugin**: New plugin (`SwitchBlade.Plugins.WindowsTerminal`) that lists individual tabs within Windows Terminal instances.
  - Uses UI Automation to discover and activate specific tabs within each Terminal window.
  - **Defensive Fallback**: If tabs cannot be enumerated (e.g., due to UIPI when SwitchBlade runs as User but Terminal runs as Administrator), the plugin returns the main window as a fallback to avoid "losing" windows.
  - Added `IsTerminalTab` property to `WindowItem` to distinguish Terminal tab results from standard windows.
- **Run as Administrator Setting**: New toggle in Settings to run SwitchBlade with elevated privileges.
  - Some plugins require Administrator privileges for full window inspection (e.g., viewing tabs in elevated applications).
  - When enabled, SwitchBlade prompts for UAC consent on startup.
  - Toggling the setting prompts to restart the application.

### Changed
- **Plugin Architecture Improvement**: Removed plugin-specific flags from core `WindowItem` contract.
  - Deleted `IsChromeTab` and `IsTerminalTab` properties to make the architecture truly plugin-agnostic.
  - Plugins now rely on the `Source` property and internal logic to determine activation strategies.
  - **BREAKING**: External plugins depending on these flags must be updated to use `item.Source == this` checks instead.
  - This change ensures that future plugin developers won't need to modify core contracts.
- **Windows Terminal Plugin - Configurable Processes**: Added settings dialog to configure which terminal processes to scan for tabs.
  - Default processes: `WindowsTerminal`
  - Settings UI matches the Chrome plugin's design for consistency
  - Process names are stored in registry and persist across restarts

### Fixed
- **Run as Administrator Restart**: Fixed a critical bug where toggling "Run as Administrator" caused a "SwitchBlade is already running" error.
  - **Root cause**: The single-instance mutex was held during restart, preventing the elevated process from starting.
  - **Solution**: Release and dispose the mutex before launching the elevated process in `Program.cs`.
  - **Additional fix**: Updated `SettingsViewModel.RestartApplication()` to properly delegate elevation logic to `Program.Main()`.
  - Toggling admin privileges (on or off) now works reliably without manual registry cleanup.

### Notes
- **Privilege Considerations**: 
  - Standard applications: Visible to SwitchBlade running as User.
  - Elevated (Administrator) applications: Requires SwitchBlade to also run as Administrator to see internal details. Otherwise, only the main window is listed.


## [1.4.8] - 2026-01-03

### Fixed
- **Search Reset**: Fixed an issue where the search results and search bar were not clearing correctly when the window was re-shown.
  - Updated `MainWindow` to modify the ViewModel's `SearchText` property directly instead of the View's `TextBox`, ensuring state consistency.
- **Stability**: Fixed an `ArgumentException` crash ("An item with the same key has already been added") in `MainViewModel`.
  - Updated `SyncCollection` to correctly handle and deduplicate window items with identical Hwnd and Title returned by multiple plugins.

## [1.4.7] - 2026-01-03

### Changed
- **DI & Service Locator Refactoring**:
  - Eliminated Service Locator usages in `MainWindow` and `Helpers`.
  - Refactored `MainWindow` to use full Constructor Injection for `ISettingsService`, `IDispatcherService`, and `ILogger`.
  - Converted `Logger` to a proper Singleton `ILogger` implementation, removing `LoggerBridge` and static dependency usage in core services.
  - Updated `PluginLoader` to simplified dependency passing logic.
  - Standardized `IWindowProvider.Initialize` to use `IPluginContext` consistently, with `CachingWindowProviderBase` exposing a protected `Logger` property for derived plugins.

### Fixed
- **Logging**: Fixed duplicate `ILogger` method definitions and potential file lock contention.
- **Unit Tests**: Updated test suite to use `Moq` for `IPluginContext` and `ILogger`, achieving greater isolation and reliability.

## [1.4.6] - 2026-01-03

### Changed
- **Native Interop Refactoring**: 
  - Consolidated all Win32 P/Invoke declarations into `SwitchBlade.Contracts.NativeInterop`.
  - Deleted the redundant `SwitchBlade.Core.Interop` class.
  - Updated all core components (`WindowFinder`, `ThumbnailService`, `HotKeyService`) and plugins to use the shared Contracts library, establishing a "Single Source of Truth" for OS interactions.

## [1.4.5] - 2026-01-03

### Fixed
- **Hotkey Reliability**: Fixed a bug where ALT+1-0 shortcuts would sometimes become duplicated or skipped after extensive use.
  - Replaced unreliable UI-based index calculation with a robust backend `ShortcutIndex` property on `WindowItem`.
  - The application now explicitly assigns shortcut indices (0-9) to the top 10 filtered results, ensuring the UI number badges always match the actual activation logic.
- **Unit Test Stability**: Suppressed a nullable warning in `ConverterTests` to cleanly pass strict validaton checks.

## [1.4.4] - 2026-01-03

### Fixed
- **Window Duplication**: Fixed a bug where browser windows (e.g., Comet) were listed twice in results—once by the specialized plugin (e.g., ChromeTabFinder) and once by the core WindowFinder.
  - Implemented proper `SetExclusions` override in `WindowFinder` to listen for excluded processes.
  - Added virtual `SetExclusions` support to `CachingWindowProviderBase` to ensure exclusions are correctly propagated from the main application.

## [1.4.3] - 2026-01-03

### Fixed
- **Build & Test Infrastructure**:
  - Achieved clean build (0 errors, 0 warnings) by resolving multiple compiler warnings.
  - Resolved `App.InitializeComponent` build error during testing by explicitly defining `App.xaml` as `ApplicationDefinition`.
  - Achieved **100% Test Pass Rate** (128/128 tests) by fixing flaky and broken tests.
- **Unit Tests**:
  - Refactored `ConverterTests` to remove dependency on WPF `ListBoxItem`, resolving `STAThread` errors.
  - Fixed async/await implementation in `BackgroundPollingServiceTests`.

## [1.4.2] - 2026-01-03

### Added
- **Dependency Injection Container**: Introduced `Microsoft.Extensions.DependencyInjection` for proper service resolution.
- **IPluginContext Interface**: New context object passed to plugins during initialization, replacing the loosely-typed `(object settingsService, ILogger logger)` signature.
- **NativeInterop Shared Library**: Consolidated P/Invoke declarations in `SwitchBlade.Contracts.NativeInterop` for use by both Core and plugins.
- **ServiceConfiguration**: New composition root class (`Services/ServiceConfiguration.cs`) that registers all services.
- **Testability Interfaces**: Introduced abstractions to enable unit testing:
  - `IDispatcherService`: Abstracts `Dispatcher.Invoke/InvokeAsync`.
  - `IApplicationResourceHandler`: Abstracts access to `Application.Current.Resources`.
  - `IWindowListViewModel`: Decouples input handlers from the main ViewModel.
  - `IPluginSettingsService`: Abstracts registry access for plugins.

### Changed
- **BREAKING**: `IWindowProvider.Initialize()` now takes `IPluginContext context` instead of `(object settingsService, ILogger logger)`. Plugin developers must update their implementations.
- **App Architecture**: Removed Service Locator pattern (`((App)Application.Current).SettingsService`). Services are now injected via constructor.
- **MainWindow**: Now receives `IServiceProvider` via constructor instead of accessing services through `App`.
- **MainWindow SRP Refactoring**: Extracted handlers into dedicated classes:
  - `Handlers/KeyboardInputHandler.cs` - All keyboard navigation and shortcuts (~160 lines extracted)
  - `Handlers/WindowResizeHandler.cs` - Resize grip handling (~40 lines extracted)
- **HotKeyService/BackgroundPollingService**: Now depend on `ISettingsService` interface instead of concrete class.
- **ChromeTabFinder**: Now depends on `IPluginSettingsService` for better testability.

### Fixed
- **Silent Exception Swallowing**: Added logging to previously empty catch blocks in `SettingsService`.
- **Code Organization**: 
  - Extracted `UserSettings` class to `Models/UserSettings.cs`.
  - Extracted `RefreshBehavior` enum to `Models/RefreshBehavior.cs`.
  - Created `ModifierKeyFlags` constants to replace magic numbers.

### Removed
- **Duplicated Native Methods**: Removed ~70 lines of duplicated P/Invoke code from `ChromeTabFinder` plugin.

## [1.4.1] - 2026-01-03

### Added
- **Keyboard Navigation**:
  - `Ctrl+Home` / `Ctrl+End`: Jump to first/last item in the list.
  - `Page Up` / `Page Down`: Move selection by one visible page.
- **Preserve Selection on Refresh**: New setting (disabled by default) to control whether selection follows window identity or stays at index position during list updates.
- **Plugin Framework**: Added `CachingWindowProviderBase` abstract base class to `SwitchBlade.Contracts` for thread-safe window scanning with automatic caching.
  - When a scan is in progress, subsequent calls to `GetWindows()` return cached results instead of starting duplicate scans.
  - Includes `IsScanRunning` and `CachedWindows` properties for plugin developers.

### Fixed
- **Settings Crash**: Fixed missing `ModernRadioButtonStyle` resource causing a crash when opening settings.
- **Scroll Preservation**: Fixed list scroll position resetting during refresh by synchronizing the window list in-place instead of replacing it.

### Changed
- **Plugins**: `ChromeTabFinder` and `WindowFinder` now inherit from `CachingWindowProviderBase` for improved concurrency handling.
- **Docs**: Updated `PLUGIN_DEVELOPMENT.md` with concurrency and caching best practices section.
- **Docs**: Added comprehensive "Smart Refresh & List Merge Strategy" section to README.

## [1.4.0] - 2026-01-02

### Added
- **Plugin Management**: Added ability to enable/disable plugins from Settings without restarting.
- **System Tray**:
    - Double-click tray icon to show search bar.
    - Added "Show" option to tray context menu.
- **Shortcuts**:
    - Number shortcuts (Alt+1...9) to quickly switch to top results.
    - Configurable modifier key for number shortcuts (Alt, Ctrl, Shift, None).
- **Settings UI**:
    - Improved plugin list with enable/disable toggles.
    - Added dedicated settings button (⚙) for configurable plugins.

### Changed
- **Architecture**: Refactored plugin system to support per-plugin configuration storage.
- **Settings**: Plugin settings are now stored in `HKCU\Software\SwitchBlade\Plugins\{PluginName}`.
- **Chrome Plugin**: Now manages its own target processes list via its own settings dialog.

### Fixed
- **Default Hotkey**: Fixed issue where default hotkey could fallback to `Ctrl+Shift+Tab` instead of `Ctrl+Shift+Q`.
- **Performance**: Optimized window list updates to reduce flicker.
- **Reliability**: Fixed potential crashes when switching windows.

## [1.3.0] - 2026-01-01
- Initial public release components.
