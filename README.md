# SwitchBlade Technical Documentation

**Current Version: 1.8.3**

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [CHANGELOG.md](CHANGELOG.md) | Version history and release notes |
| [PLUGIN_DEVELOPMENT.md](PLUGIN_DEVELOPMENT.md) | Guide for creating custom plugins |
| [BUILD.md](BUILD.md) | Build instructions and project setup |

---

## Overview
SwitchBlade is a high-performance Keyboard-Driven Window Switcher for Windows. It is built using **C# / WPF** and follows the **MVVM (Model-View-ViewModel)** architectural pattern. It is designed to be extensible via a robust Plugin System, allowing it to index not just top-level windows but also internal document tabs as searchable items.

The package comes with several specialized plugins out of the box:
- **Window Finder**: Discovers all standard top-level desktop application windows.
- **Chrome Tab Finder**: Indexes individual tabs from Google Chrome, Microsoft Edge, and other Chromium-based browsers.
- **Windows Terminal**: Discovers and allows switching between multiple tabs within Windows Terminal instances.
- **Notepad++**: Indexes and switches between individual open files/tabs in Notepad++.
- **Microsoft Teams**: Discovers and switches between individual chat conversations in Microsoft Teams (v2).


## Basic Usage

- **Toggle SwitchBlade**: Press `Ctrl + Shift + Q` (Default) to show or hide the search window.
- **Search**: Start typing immediately to filter open windows and tabs.
- **Navigate**: Use `Up` / `Down` arrows to select a window.
- **Activate**: Press `Enter` to switch to the selected window.
- **Close**: Press `Escape` to hide SwitchBlade without switching.

## Keyboard Shortcuts

SwitchBlade supports the following keyboard shortcuts for navigation:

| Key | Action |
| :--- | :--- |
| `↑` / `↓` | Move selection up/down by one item |
| `Page Up` / `Page Down` | Move selection up/down by one visible page |
| `Ctrl+Home` | Jump to the first item |
| `Ctrl+End` | Jump to the last item |
| `Enter` | Activate the selected window |
| `Escape` | Hide the SwitchBlade window |
| `Alt+1` to `Alt+0` | Quick-switch to windows 1-10 (configurable modifier) |

## Architecture

### Core Components
- **MainViewModel**: The central brain of the application. It orchestrates window provider execution, aggregates results, and manages the search/filter state.
- **Service Layer**: 
  - `SettingsService`: Manages persistence of user preferences (Registry-based).
  - `HotKeyService`: Handles global low-level keyboard hooks for the toggle hotkey.
- **Window Providers**: Independent modules responsible for scanning and returning `WindowItem` objects.

```mermaid
graph TD
    User((User)) -->|Hotkey| HotKeyService
    User -->|Types| SearchText[Search Input]
    SearchText -->|Triggers| MainViewModel
    HotKeyService -->|Toggle| MainViewModel
    
    subgraph Core Application
        MainViewModel -->|Manages| SearchState
        MainViewModel -->|Hits| RegexCache[LRU Regex Cache]
        MainViewModel -->|Reads/Writes| SettingsService
        MainViewModel -->|Executes| PluginLoader
    end

    subgraph Data Sources
        PluginLoader -->| loads | IWindowProvider
        IWindowProvider -.-> WindowFinder
        IWindowProvider -.-> ChromeTabFinder
        IWindowProvider -.-> TerminalPlugin
        IWindowProvider -.-> NotepadPlusPlusPlugin
        IWindowProvider -.-> TeamsPlugin
    end

    MainViewModel -->|Aggregates| WindowList[Filtered Window List]
    WindowFinder -->|Yields| WindowItem
    ChromeTabFinder -->|Yields| WindowItem
    TerminalPlugin -->|Yields| WindowItem
    NotepadPlusPlusPlugin -->|Yields| WindowItem
    TeamsPlugin -->|Yields| WindowItem
    
    style RegexCache fill:#f9f,stroke:#333,stroke-width:2px,color:black
```

## Performance

SwitchBlade 1.5.1+ utilizes bleeding-edge .NET 9 features to ensure minimal resource footprint and maximum responsiveness.

### Key Optimizations
- **Zero-Allocation Window Scanning**: Uses `Span<char>`, `stackalloc` and `Unsafe` pointers to retrieve window titles and binary paths without generating garbage (GC pressure).
- **Source-Generated Interop**: Replaces slow `[DllImport]` with high-performance `[LibraryImport]` for all Windows API calls, ensuring trimming and AOT compatibility.
- **Modern Async Polling**: Uses `PeriodicTimer` for lock-free, efficient background updates.
- **Smart Caching**: Process names, paths, and icons come from a concurrent cache to minimize kernel transitions and I/O.
- **Configurable Regex caching**: Implements an LRU (Least Recently Used) cache for compiled regex objects to ensure buttery-smooth search responsiveness during rapid typing.
- **Immune to ReDoS**: Dynamically switches to the `.NET 9 NonBacktracking` engine for all user-provided patterns, providing guaranteed linear-time matching and protection against malicious regex hangs.
- **ReadyToRun (R2R) Deployment**: Pre-compiled native code in the binary reduces startup time and eliminates JIT warm-up latency.

```mermaid
graph TD
    subgraph UI Thread
        UI[Main Window]
        RC[LRU Regex Cache]
        NB[NonBacktracking Engine]
    end

    subgraph Background Service
        BP[BackgroundPollingService]
        PT["PeriodicTimer (Async)"]
    end

    subgraph Core Logic
        WF[WindowFinder]
        NI[NativeInterop]
    end

    subgraph Windows OS
        API1[EnumWindows]
        API2[GetWindowTextW]
    end

    %% Discovery Path
    UI -- Dispatcher --> BP
    BP -- Await Tick --> PT
    BP -- Refresh --> WF
    WF -- StackAlloc Buffer --> NI
    NI -- LibraryImport --> API1
    NI -- Unsafe Pointer --> API2

    %% Search Path
    UI -- User Input --> RC
    RC -- Cache Hit/Miss --> NB
    NB -- Guaranteed O(n) --> UI

    style NI fill:#f9f,stroke:#333,stroke-width:2px,color:black
    style WF fill:#bbf,stroke:#333,stroke-width:2px,color:black
    style RC fill:#f9f,stroke:#333,stroke-width:2px,color:black
    style NB fill:#bbf,stroke:#333,stroke-width:1px,color:black
```

## Fuzzy Search

SwitchBlade 1.6.0 introduces intelligent fuzzy search that makes finding windows effortless.

### Features

| Feature | Description | Example |
|:--------|:------------|:--------|
| **Delimiter Equivalence** | Spaces, underscores, and dashes are treated identically | `hello there` matches `hello_there` |
| **Subsequence Matching** | Characters must appear in order but not consecutively | `gc` matches `Google Chrome` |
| **Case Insensitive** | Matching ignores letter case | `CHROME` matches `chrome` |
| **Relevance Sorting** | Best matches appear first based on scoring | Exact matches rank highest |

### Scoring System

Fuzzy search ranks results using a weighted scoring algorithm:

| Bonus | Points | Awarded When |
|:------|:------:|:-------------|
| Base Match | +1 | Each matched character |
| Contiguity | +2 | Consecutive character matches |
| Word Boundary | +3 | Match at start of title |
| Starts-With | +5 | Title begins with query |

```mermaid
flowchart TD
    Start[User Types Query] --> FastPath{Exact Substring?}
    FastPath -- Yes --> ExactScore[Calculate Exact Score + Bonuses]
    FastPath -- No --> Normalize
    
    subgraph Zero-Allocation Pipeline
        Normalize[Normalize Title & Query]
        Normalize --> |stackalloc buffer| RemoveDelim[Remove Spaces/Underscores/Dashes]
        RemoveDelim --> |Span char| ToLower[Convert to Lowercase]
    end
    
    ToLower --> LengthCheck{Query <= Title?}
    LengthCheck -- No --> NoMatch[Return 0]
    LengthCheck -- Yes --> Subsequence
    
    subgraph Subsequence Matching
        Subsequence[Find Characters in Order]
        Subsequence --> |For each match| AddBase[+1 Base Score]
        AddBase --> Contiguous{Previous was adjacent?}
        Contiguous -- Yes --> AddCont[+2 Contiguity Bonus]
        Contiguous -- No --> CheckStart
        AddCont --> CheckStart{At position 0?}
        CheckStart -- Yes --> AddBoundary[+3 Word Boundary]
        CheckStart -- No --> NextChar[Continue]
        AddBoundary --> NextChar
    end
    
    NextChar --> AllFound{All chars found?}
    AllFound -- No --> NoMatch
    AllFound -- Yes --> StartsCheck{Started at pos 0?}
    StartsCheck -- Yes --> AddStarts[+5 Starts-With Bonus]
    StartsCheck -- No --> FinalScore
    AddStarts --> FinalScore[Return Total Score]
    ExactScore --> SortResults
    FinalScore --> SortResults[Sort by Score DESC]
    
    style Normalize fill:#f9f,stroke:#333,stroke-width:2px,color:black
    style RemoveDelim fill:#f9f,stroke:#333,stroke-width:2px,color:black
    style ToLower fill:#f9f,stroke:#333,stroke-width:2px,color:black
```

### Configuration

- **Enable/Disable**: Toggle in Settings → Search & Performance → "Enable Fuzzy Search"
- **Default**: Enabled
- **Fallback**: When disabled, uses legacy regex/substring matching

## Development

For information on how to build the project and create plugins, please refer to the following guides:

- [Build Instructions](BUILD.md): Detailed steps for setting up your environment, building SwitchBlade, and running unit tests.
- [Plugin Development Guide](PLUGIN_DEVELOPMENT.md): A comprehensive guide on building custom plugins for window discovery.

### Unit Tests
The project includes comprehensive xUnit tests in `SwitchBlade.Tests/`. Run tests with:
```powershell
dotnet test SwitchBlade.Tests/SwitchBlade.Tests.csproj
```

### Plugin System
SwitchBlade uses a contract-based plugin architecture.
- **Interface**: `SwitchBlade.Contracts.IWindowProvider`
- **Mechanism**: On startup, `PluginLoader` scans the `Plugins` directory for DLLs implementing `IWindowProvider`.
- **Isolation**: Each plugin runs within the main application process but is logically isolated by the `WindowItem` source property.

## Command-Line Arguments

SwitchBlade supports the following command-line parameters (prefixes `/`, `--`, or `-` are all supported):

| Parameter | Description |
| :--- | :--- |
| `/debug` | Enables verbose logging. Logs are saved to `%TEMP%\switchblade_debug.log`. |
| `/minimized` | Starts the application in the system tray without showing the main window. |
| `/enablestartup` | Used by the installer to enable "Launch on Startup" in the Windows Registry on first run. |

## Window Discovery Logic

SwitchBlade uses a two-tier architecture for window discovery: fast in-process scanning for standard windows, and out-of-process UIA scanning for specialized plugins with **streaming results**.

### Streaming NDJSON Protocol (v1.8.2+)

UIA plugins run in parallel and emit results immediately as each completes. This eliminates the blocking behavior where fast plugins waited for slow ones.

```mermaid
sequenceDiagram
    participant Main as SwitchBlade (Main)
    participant Worker as UiaWorker.exe
    participant Chrome as ChromeTabFinder
    participant Terminal as WindowsTerminal

    Main->>Worker: Start process, send JSON request
    
    par Parallel Plugin Execution
        Worker->>Chrome: GetWindows()
        Worker->>Terminal: GetWindows()
    end
    
    Chrome-->>Worker: Results (15ms)
    Worker-->>Main: {"pluginName":"Chrome","windows":[...]}
    Note over Main: UI updates immediately with Chrome tabs
    
    Terminal-->>Worker: Results (2000ms)
    Worker-->>Main: {"pluginName":"Terminal","windows":[...]}
    Note over Main: Terminal tabs appear when ready
    
    Worker-->>Main: {"isFinal":true}
    Worker->>Worker: Exit (releases all COM objects)
```

### Architecture Overview

```mermaid
flowchart LR
    Start[Start Scan] --> Parallel{Parallel Execution}
    Parallel -->|Task 1| WF["WindowFinder (In-Process)"]
    Parallel -->|Task 2| UIA[UiaWorkerClient]
    
    subgraph "Main Process"
        WF --> Enum[EnumWindows]
        Enum --> Filter{Is Visible?}
        Filter -- Yes --> Exclude{Handling Plugin Exists?}
        Exclude -- No --> Result1[Add WindowItem]
    end

    subgraph "Child Process (SwitchBlade.UiaWorker.exe)"
        UIA -->|JSON over Stdin| Plugins[Parallel Plugins]
        Plugins --> CTF[ChromeTabFinder]
        Plugins --> WTP[WindowsTerminalPlugin]
        Plugins --> NPP[NotepadPlusPlusPlugin]
        
        CTF -->|Stream| NDJSON[NDJSON Output]
        WTP -->|Stream| NDJSON
        NPP -->|Stream| NDJSON
    end
    
    Result1 --> UI[Update UI]
    NDJSON -->|IAsyncEnumerable| UI
```

### Key Benefits

| Aspect | Before (v1.8.1) | After (v1.8.2) |
|--------|-----------------|----------------|
| **Fast Plugin Visibility** | Blocked until all complete | Immediate |
| **Plugin Execution** | Sequential | Parallel |
| **Protocol** | Single JSON response | Streaming NDJSON |
| **User Experience** | Delayed "all at once" | Progressive "pop-in" |

### 1. Core Window Finder (`WindowFinder.cs`)
This is the built-in provider for standard desktop applications.
- **Method**: Uses the Win32 `EnumWindows` API to iterate over all top-level windows on the desktop.
- **Filtering**:
  - Checks `IsWindowVisible`.
  - Filters out known system noise (e.g., "Program Manager").
  - **Zero-Allocation Process Lookup**: Uses specialized native APIs (`QueryFullProcessImageName`) instead of the heavy .NET `Process` class to identify window owners without allocating managed memory.
  - **Smart De-Duplication**: It automatically inspects the `IBrowserSettingsProvider` list. If a window belongs to a process that is handled by a specialized plugin (e.g., "chrome", "comet"), `WindowFinder` **excludes** it. This prevents double-entries where both the generic window title and the specific tabs would appear.

### 2. Chrome Tab Finder (`ChromeTabFinder.cs`)
A specialized plugin for Chromium-based browsers (Chrome, Edge, Brave, Comet, etc.).
- **Execution Mode**: Runs **Out-of-Process** via `SwitchBlade.UiaWorker.exe` to prevent native memory leaks.
- **Discovery Strategy**:
  1.  **Process Identification**: Identifies target processes by name (configurable).
  2.  **Window Enumeration**: Uses `EnumWindows` (Win32) to find **ALL** top-level windows belonging to those PIDs.
  3.  **UI Automation**: Attaches to each window using `System.Windows.Automation`.
  4.  **Tree Traversal**: Performs a Breadth-First Search (BFS) of the automation tree to find elements with `ControlType.TabItem`.

#### Thread Safety & Isolation
Since this plugin runs in a separate process, it is immune to the "FindAll" memory leak inherent in Windows 11's UIA framework. When the worker process exits after scanning, all accumulated COM references are instantly released by the OS.

### 3. Windows Terminal Plugin (`WindowsTerminalPlugin.cs`)
A specialized plugin for Microsoft's Windows Terminal.
- **Execution Mode**: Runs **Out-of-Process** via `SwitchBlade.UiaWorker.exe`.
- **Discovery Strategy**:
  1.  **Process Identification**: Identifies target processes by name (default: "WindowsTerminal", configurable via settings).
  2.  **UI Automation**: Attaches to the main window handle (`MainWindowHandle`) of each identified process.
  3.  **Tree Traversal (`ScanForTabs`)**: Performs a Breadth-First Search (BFS) of the automation tree up to a depth of 12 to find `ControlType.TabItem` elements.
- **Fallback Mechanism**: If no tabs are discovered (often due to elevation/UIPI restrictions when SwitchBlade is not elevated), the plugin returns the main terminal window as a single searchable item.
- **Activation**:
  1.  Brings the main window to the foreground.
  2.  Uses UI Automation patterns (`SelectionItemPattern` or `InvokePattern`) to programmatically select the specific tab requested by the user.

### 4. Notepad++ Plugin (`NotepadPlusPlusPlugin.cs`)
Indexes and switches between individual open files/tabs in Notepad++.
- **Execution Mode**: Runs **Out-of-Process** via `SwitchBlade.UiaWorker.exe`.
- **Mechanism**: Similar to the Terminal plugin, it uses UI Automation to traverse the document tabs in Notepad++.
- **Strategy**: Identifies `notepad++` processes and scans for tab items to allow direct file-level switching.

### 5. Microsoft Teams Plugin (`TeamsPlugin.cs`)
A specialized plugin for Microsoft Teams (v2/New Teams).
- **Execution Mode**: Runs **Out-of-Process** via `SwitchBlade.UiaWorker.exe` to isolate the main application from WebView2 memory characteristics.
- **Discovery Strategy**:
  1. **Process Identification**: Identifies `ms-teams` processes.
  2. **Chat Parsing**: Scans the UI tree for `TreeItem` elements representing chats. Parses contact names and statuses (e.g., "Available", "Busy") using regex patterns derived from extensive testing.
  3. **Chat Types**: Distinguishes between Individual, Group, and Meeting chats.
- **Activation**:
  - Uses a robust "Pattern Cascade" to activate chats:
    1. `InvokePattern` (Click)
    2. `SelectionItemPattern` (Select)
    3. `ExpandCollapsePattern` (Expand)
    4. `SetFocus` (Fallback)
  - This ensures reliable switching regardless of the specific UI state of the chat item.

## Async & Threading Model

### Parallel Execution
SwitchBlade does NOT block the UI thread while searching.
- When `RefreshWindows()` is called, the application spawns a separate `Task` for each loaded `IWindowProvider`.
- These tasks run in parallel on the ThreadPool. The fast `WindowFinder` typically finishes in <10ms, while `ChromeTabFinder` may take 100-300ms depending on open tabs.

### UI Marshalling
- As each background task completes, it marshals its results back to the UI thread using `Application.Current.Dispatcher.Invoke`.
- This creates a "Pop-in" effect where core windows appear instantly, followed shortly by browser tabs.

## Smart Refresh & List Merge Strategy

SwitchBlade uses a sophisticated incremental update strategy to keep the window list stable and prevent visual disruption during updates. The goal is to never clear the list and re-add all items, which would cause flickering and loss of user context.

### Persistence Strategy
1. **No Clear-On-Toggle**: When the Global Hotkey is pressed, the list is **NOT** cleared. The user immediately sees the results from the *previous* session while background scans run.
2. **Provider-Isolated Updates**: Each window provider (e.g., `WindowFinder`, `ChromeTabFinder`) updates its own slice of the list independently. Changes from one provider don't affect items from other providers.

### Incremental Merge Algorithm (O(N) Optimized)

SwitchBlade uses a high-performance, two-phase synchronization algorithm to update the UI collection without full list refreshes. This ensures selection persistence and buttery-smooth animations.

```mermaid
flowchart TD
    Start["Source List Received"] --> Phase1["Phase 1: Cleanup"]
    Phase1 --> BuildSet["Build HashSet of Source Items"]
    BuildSet --> ReverseLoop["Loop Collection Backwards"]
    ReverseLoop --> Exists{"In SourceSet?"}
    Exists -- No --> Remove["RemoveAt i"]
    Exists -- Yes --> NextDel["Next Item"]
    
    Remove --> NextDel
    NextDel -->|Done| Phase2["Phase 2: O(N) Two-Pointer Sync"]
    
    Phase2 --> InitPtr["Set ptr = 0"]
    InitPtr --> SourceLoop["Loop through Source"]
    SourceLoop --> Match{"collection[ptr] == source[i]?"}
    
    Match -- Yes --> IncPtr["ptr++"]
    IncPtr --> SourceLoop
    
    Match -- No --> Find["Search Forward for Item"]
    Find --> Found{"Found?"}
    
    Found -- Yes --> Move["collection.Move foundAt -> ptr"]
    Move --> IncPtr
    
    Found -- No --> Insert["collection.Insert ptr, item"]
    Insert --> IncPtr
    
    SourceLoop -->|Complete| End["Sync Finished"]
    
    style Phase2 fill:#f9f,stroke:#333,stroke-width:2px,color:black
    style Match fill:#bbf,stroke:#333,stroke-width:2px,color:black
```

#### Phase 1: Reconciliation (Cleanup)
Identifies and removes items that are no longer part of the current search results. Using a `HashSet<WindowItem>` ensures existence checks are $O(1)$.

#### Phase 2: Two-Pointer Sync (Order & Stability)
Synchronizes the collection order with the source list using a single pass ($O(N)$). It minimizes UI thread workload by only issuing `Move` or `Insert` commands when structural changes are detected. By searching forward from the current pointer, it avoids the $O(N^2)$ penalty of multiple `IndexOf` calls.

### Selection Preservation

During list updates, the selection behavior is controlled by the **List Refresh Behavior** setting:

| Setting | Behavior |
| :--- | :--- |
| **Preserve scroll position** (default) | Selection is updated silently. The scroll position stays exactly where it was. The view does NOT auto-scroll to the selected item. |
| **Follow selected window (Identity)** | Selection follows the same **window identity** (Hwnd + Title). If your selected window moves, the list auto-scrolls to keep it visible. |
| **Keep selection index (Position)** | Selection stays at the current **index position**. If you're viewing item #3, you'll still be viewing item #3 after refresh (even if the window at that position changed). The list auto-scrolls to the new selection. |

### Diff Key Design

Chrome tabs share the same `Hwnd` (the browser window handle), so we use a composite key:
```
Identity = (Hwnd, Title)
```
This allows us to:
- Distinguish between tabs in the same browser window
- Detect when a tab's title has changed (e.g., page navigation)
- Properly track selection across refreshes

### Thread Safety

The merge operation runs on background threads via `Task.Run()`, but all mutations to `_allWindows` and `FilteredWindows` are marshalled to the UI thread via `Dispatcher.Invoke()`. This ensures:
- No race conditions on the ObservableCollection
- WPF bindings receive proper change notifications
- The UI remains responsive during long scans

## Run as Administrator

Some plugins require elevated privileges to fully inspect certain windows (e.g., tabs in an elevated Terminal or other admin-level applications).

### Configuration
- **Toggle**: Found in Settings → "Run as Administrator"
- **Default**: Off (disabled)
- **Effect**: When enabled, SwitchBlade displays a UAC prompt on startup

### Behavior
When the setting is toggled:
1. The setting is saved immediately
2. A dialog prompts the user to restart
3. On next startup, SwitchBlade requests elevation via UAC

> **Note**: If "Launch on Windows Startup" is also enabled, and the user wants automatic elevation, they may need to configure a Scheduled Task with "Run with highest privileges" instead of the standard Run registry entry.

## Background Polling

SwitchBlade supports optional background polling to keep the window list up-to-date even when the application is not in focus.

### Configuration
- **Enable Background Polling**: Toggle in Settings (default: enabled).
- **Polling Interval**: Configurable in Settings (default: 30 seconds, range: 5-120 seconds).

### Concurrency Protection
The `BackgroundPollingService` uses a `SemaphoreSlim(1, 1)` to ensure only one refresh operation runs at a time. If a refresh is already in progress when the timer ticks, that tick is skipped. This prevents thread contention and race conditions on the window list.

## Number Shortcuts

SwitchBlade supports number shortcuts for instant window switching. When enabled, holding the modifier key and pressing a number key (1-9 or 0) will immediately activate the corresponding window in the list.

### Key Mapping
| Keys | Window Position |
| :---: | :---: |
| `Alt+1` | 1st window |
| `Alt+2` | 2nd window |
| ... | ... |
| `Alt+9` | 9th window |
| `Alt+0` | 10th window |

### Configuration
- **Enable Number Shortcuts**: Toggle in Settings (default: enabled)
- **Shortcut Modifier**: Choose Alt, Ctrl, Shift, or None (default: Alt)
- Supports both main keyboard number row and NumPad keys
- When enabled, number badges appear next to the first 10 windows in the list

### Smooth Reordering
The window list maintains a stable sort (by Process Name → Title → Handle) to minimize visual disruption when new windows appear. Combined with the incremental merge strategy, the numbered positions update smoothly without full list refreshes.

### Badge Animation System

The Alt+Number badges feature a staggered animation that provides visual polish when the window list appears. Each badge fades in and slides from left to right in sequence.

```mermaid
sequenceDiagram
    participant UI as UI Thread
    participant BAS as BadgeAnimationService
    participant B1 as Badge Alt+1
    participant B2 as Badge Alt+2
    participant B0 as Badge Alt+0

    UI->>BAS: TriggerStaggeredAnimationAsync()
    BAS->>BAS: ResetAnimationState()
    
    Note over BAS: Stagger delay = 75ms per badge
    
    BAS->>B1: Start animation (0ms delay)
    Note over B1: Opacity: 0→1<br/>TranslateX: -20px→0
    
    BAS->>B2: Start animation (75ms delay)
    Note over B2: Opacity: 0→1<br/>TranslateX: -20px→0
    
    BAS->>B0: Start animation (675ms delay)
    Note over B0: Last badge (index 9)
    
    Note over B1,B0: Each animation: 150ms duration, cubic ease-out
```

#### Animation Timing
| Parameter | Value | Purpose |
|:---|:---|:---|
| **Stagger Delay** | 75ms | Time between each badge starting its animation |
| **Duration** | 150ms | Total animation time per badge |
| **Offset** | -20px | Starting X position (slides right to 0) |
| **Easing** | Cubic ease-out | Smooth deceleration |

#### HWND Tracking
The `BadgeAnimationService` tracks which window handles (HWNDs) have been animated to prevent re-animation:
- When a window's title changes but HWND remains the same → badge stays visible (no re-animation)
- When search text changes → animation state resets, badges re-animate with filtered results
- When window hides and shows again → full reset, all badges animate fresh

#### Configuration
- **Enable Badge Animations**: Toggle in Settings (default: enabled)
- When disabled, badges appear instantly at full opacity
