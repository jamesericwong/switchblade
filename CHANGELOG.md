# Changelog

All notable changes to this project will be documented in this file.

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
