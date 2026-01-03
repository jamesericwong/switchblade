# Changelog

All notable changes to this project will be documented in this file.

## [1.4.2] - 2026-01-03

### Added
- **Dependency Injection Container**: Introduced `Microsoft.Extensions.DependencyInjection` for proper service resolution.
- **IPluginContext Interface**: New context object passed to plugins during initialization, replacing the loosely-typed `(object settingsService, ILogger logger)` signature.
- **NativeInterop Shared Library**: Consolidated P/Invoke declarations in `SwitchBlade.Contracts.NativeInterop` for use by both Core and plugins.
- **ServiceConfiguration**: New composition root class (`Services/ServiceConfiguration.cs`) that registers all services.

### Changed
- **BREAKING**: `IWindowProvider.Initialize()` now takes `IPluginContext context` instead of `(object settingsService, ILogger logger)`. Plugin developers must update their implementations.
- **App Architecture**: Removed Service Locator pattern (`((App)Application.Current).SettingsService`). Services are now injected via constructor.
- **MainWindow**: Now receives `IServiceProvider` via constructor instead of accessing services through `App`.
- **MainWindow SRP Refactoring**: Extracted handlers into dedicated classes:
  - `Handlers/KeyboardInputHandler.cs` - All keyboard navigation and shortcuts (~160 lines extracted)
  - `Handlers/WindowResizeHandler.cs` - Resize grip handling (~40 lines extracted)
- **HotKeyService/BackgroundPollingService**: Now depend on `ISettingsService` interface instead of concrete class.

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
