# SwitchBlade Technical Documentation

## Overview
SwitchBlade is a high-performance Keyboard-Driven Window Switcher for Windows. It is built using **C# / WPF** and follows the **MVVM (Model-View-ViewModel)** architectural pattern. It is designed to be extensible via a robust Plugin System, allowing it to index not just top-level windows but also internal document tabs (like browser tabs) as searchable items.

## Architecture

### Core Components
- **MainViewModel**: The central brain of the application. It orchestrates window provider execution, aggregates results, and manages the search/filter state.
- **Service Layer**: 
  - `SettingsService`: Manages persistence of user preferences (Registry-based).
  - `HotKeyService`: Handles global low-level keyboard hooks for the toggle hotkey.
- **Window Providers**: Independent modules responsible for scanning and returning `WindowItem` objects.

### Plugin System
SwitchBlade uses a contract-based plugin architecture. For a comprehensive guide on building custom plugins, including API references and examples, see [Plugin Development Guide](PLUGIN_DEVELOPMENT.md).
- **Interface**: `SwitchBlade.Contracts.IWindowProvider`
- **Mechanism**: On startup, `PluginLoader` scans the `Plugins` directory for DLLs implementing `IWindowProvider`.
- **Isolation**: Each plugin runs within the main application process but is logically isolated by the `WindowItem` source property.

## Window Discovery Logic

### 1. Core Window Finder (`WindowFinder.cs`)
This is the built-in provider for standard desktop applications.
- **Method**: Uses the Win32 `EnumWindows` API to iterate over all top-level windows on the desktop.
- **Filtering**:
  - Checks `IsWindowVisible`.
  - Filters out known system noise (e.g., "Program Manager").
  - **Smart De-Duplication**: It automatically inspects the `IBrowserSettingsProvider` list. If a window belongs to a process that is handled by a specialized plugin (e.g., "chrome", "comet"), `WindowFinder` **excludes** it. This prevents double-entries where both the generic window title and the specific tabs would appear.

### 2. Chrome Tab Finder (`ChromeTabFinder.cs`)
A specialized plugin for Chromium-based browsers (Chrome, Edge, Brave, Comet, etc.).
- **Discovery Strategy**:
  1.  **Process Identification**: Identifies target processes by name (configurable).
  2.  **Window Enumeration**: Uses `EnumWindows` (Win32) to find **ALL** top-level windows belonging to those PIDs. This is critical for supporting multi-window setups, as `Process.MainWindowHandle` often misses secondary windows.
  3.  **UI Automation**: Attaches to each window using `System.Windows.Automation`.
  4.  **Tree Traversal (`FindTabsBFS`)**: Performs a Breadth-First Search (BFS) of the automation tree to find elements with `ControlType.TabItem`.

#### Performance Optimization
- **Document Pruning**: The scanner explicitly skips traversing into `ControlType.Document` nodes. This effectively ignores the millions of DOM elements inside the web page content, focusing the scan solely on the browser's UI "chrome". This reduces scan time from seconds to milliseconds.
- **Depth Limiting**: Traversal is capped at a depth of 20 to prevent infinite recursion in complex UI trees.

#### Thread Safety
- **Logging**: Debug logging to `%TEMP%` is protected by a static `lock` object to prevent write contention during parallel scans.

## Async & Threading Model

### Parallel Execution
SwitchBlade does NOT block the UI thread while searching.
- When `RefreshWindows()` is called, the application spawns a separate `Task` for each loaded `IWindowProvider`.
- These tasks run in parallel on the ThreadPool. The fast `WindowFinder` typically finishes in <10ms, while `ChromeTabFinder` may take 100-300ms depending on open tabs.

### UI Marshalling
- As each background task completes, it marshals its results back to the UI thread using `Application.Current.Dispatcher.Invoke`.
- This creates a "Pop-in" effect where core windows appear instantly, followed shortly by browser tabs.

## Smart Refresh & Caching

To ensure the application feels instant, SwitchBlade employs a **Persistence Strategy**.
1.  **No Clear-On-Toggle**: When the Global Hotkey is pressed, the list is **NOT** cleared. The user immediately sees the results from the *previous* session.
2.  **Incremental Merge**:
    - The background scan starts immediately.
    - When a provider finishes (e.g., `ChromeTabFinder`), the application removes *only* the old items sourced from that specific provider and atomically adds the new items.
    - This ensures the user never encounters a "Loading..." screen or a blank flash.
