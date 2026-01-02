# SwitchBlade Plugin Development Guide

This guide explains how to extend **SwitchBlade** by creating custom plugins. SwitchBlade's plugin architecture allows you to add search results from any source—whether it's browser tabs, specific application windows, or even cloud resources—by implementing a simple interface.

## Prerequisites

- **Visual Studio 2022** or **VS Code**
- **.NET 9.0 SDK** (Core application target)
- Access to **SwitchBlade.Contracts.dll** (found in the SwitchBlade build directory)

---

## The Core Concept

A SwitchBlade plugin is simply a .NET Main Library (`.dll`) that contains one or more classes implementing the `IWindowProvider` interface.

### The Interface

The contract is defined in `SwitchBlade.Contracts.dll`:

```csharp
namespace SwitchBlade.Contracts
{
    public interface IWindowProvider
    {
        // 1. Initialization: Receive shared application state/settings and logger
        void Initialize(object settingsService, ILogger logger);

        // 2. Refresh: Return a list of items to display in the user's search
        IEnumerable<WindowItem> GetWindows();

        // 3. Activation: Handle what happens when the user presses Enter on your item
        void ActivateWindow(WindowItem item);
    }
}
```

### The Data Object

Your provider returns `WindowItem` objects:

```csharp
public class WindowItem
{
    public IntPtr Hwnd { get; set; }          // Window Handle (if applicable, else IntPtr.Zero)
    public string Title { get; set; }         // Text shown in search
    public string ProcessName { get; set; }   // Subtitle / category app name
    public IWindowProvider? Source { get; set; } // ALWAYS set this to 'this'
    
    // ... other properties
}
```

---

## Step-by-Step Implementation Guide

### 1. Create a Project
Create a new **Class Library** project targeting **.NET 9.0-windows**.
```bash
dotnet new classlib -n MyCustomPlugin -f net9.0-windows
```

### 2. Add References
Reference the `SwitchBlade.Contracts.dll`. You can copy this DLL from the SwitchBlade main output directory.

### 3. Implement the Provider
Here is a complete, minimal example:

```csharp
using SwitchBlade.Contracts;
using System;
using System.Collections.Generic;

namespace MyCustomPlugin
{
    public class SimpleProvider : IWindowProvider
    {
        public void Initialize(object settingsService, ILogger logger)
        {
            // Optional: Store settings/logger if you need them later
        }

        public IEnumerable<WindowItem> GetWindows()
        {
            // Return dummy items, file results, or API data
            yield return new WindowItem
            {
                Title = "My Plugin Result",
                ProcessName = "PluginDemo",
                Source = this // Critical for activation callback
            };
        }

        public void ActivateWindow(WindowItem item)
        {
            // Logic to execute when selected
            System.Diagnostics.Process.Start("notepad.exe");
        }
    }
}
```

---

## Case Study: ChromeTabFinder

The `ChromeTabFinder` is a real-world example used within SwitchBlade to index individual Chrome tabs as searchable items. It demonstrates complex usage including `UIAutomation` and custom activation logic.

#### 1. Initialization
It receives the `SettingsService` (as an object) and an `ILogger` instance.

```csharp
public void Initialize(object settingsService, ILogger logger)
{
   _logger = logger;
   if (settingsService is IBrowserSettingsProvider service)
   {
       _settingsService = service;
   }
}
```

### 2. Getting Windows (Aggregation)
It finds the Browser process, then uses `UIAutomation` (TreeWalker) to find tab controls.

```csharp
public IEnumerable<WindowItem> GetWindows()
{
    // 1. Get processes from settings interface
    var processesToScan = _settingsService?.BrowserProcesses ?? new List<string>(); // Safe access

    foreach (var processName in processesToScan)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var proc in processes)
        {
            // 2. Scan the automation tree for "TabItem" controls
            var foundTabs = FindTabsBFS(AutomationElement.FromHandle(proc.MainWindowHandle));
            
            foreach (var tabName in foundTabs)
            {
                // 3. Create a WindowItem for each tab
                results.Add(new WindowItem
                {
                    Hwnd = proc.MainWindowHandle, // Same HWND for all tabs in that window
                    Title = tabName,
                    ProcessName = proc.ProcessName,
                    IsChromeTab = true,
                    Source = this // Links this item back to THIS provider instance
                });
            }
        }
    }
    return results;
}
```

### 3. Activation (The Payoff)
When a user selects a specific *tab* (which isn't a separate window in Windows), existing `SetForegroundWindow` isn't enough. The provider must handle the specifics.

```csharp
public void ActivateWindow(WindowItem item)
{
    // 1. Bring the main Chrome window to front first
    Interop.SetForegroundWindow(item.Hwnd);
    
    // 2. Use UIAutomation to find the specific tab control again
    var root = AutomationElement.FromHandle(item.Hwnd);
    var tabElement = FindTabByNameBFS(root, item.Title);

    if (tabElement != null)
    {
        // 3. Invoke the pattern to switch tabs inside content
        if (tabElement.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
        {
            ((SelectionItemPattern)pattern).Select();
        }
    }
}
```

## Deployment

1. Build your project (`Release` mode recommended).
2. Navigate to the SwitchBlade installation directory.
3. Create a folder named `Plugins` (if it doesn't exist).
    - `SwitchBlade.exe`
    - `Plugins\`
        - `MyCustomPlugin.dll`
        - `AnotherPlugin.dll`
4. Restart SwitchBlade. The `PluginLoader` will automatically discover your DLL, find any class implementing `IWindowProvider`, and load it.

## Best Practices

- **Performance**: `GetWindows()` is called every time the search refreshes (or periodically). Keep it fast. If you are querying slow APIs, cache your results and return the cached list immediately.
- **Error Handling**: Wrap your `GetWindows` logic in try/catch blocks. If your plugin throws an exception, it might be logged but won't crash the main app.
- **Dependencies**: If your plugin relies on other DLLs, ensure they are also copied to the `Plugins` folder or available in the global path.
