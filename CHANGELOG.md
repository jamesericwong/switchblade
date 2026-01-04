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
