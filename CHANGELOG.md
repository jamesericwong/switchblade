# Changelog

All notable changes to this project will be documented in this file.

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
