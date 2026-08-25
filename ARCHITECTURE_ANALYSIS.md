# SwitchBlade v1.9.16 — Architecture & Code Quality Analysis

**Date:** 2026-08-25
**Scope:** 8 projects, ~10k lines production C#/.NET 9 WPF + ~735 test methods. A keyboard-driven window switcher with an in-process/out-of-process plugin system.

---

## 1. Architecture

### What's well designed

- **Clean project layering, zero circular references.** Plugins and the UIA worker depend *only* on `SwitchBlade.Contracts`; the main app references plugins build-only (`ReferenceOutputAssembly=false`, SwitchBlade.csproj:39-50), preserving runtime isolation.
- **The out-of-process UIA pipeline is the best part of the system** (Services/UiaWorkerClient.cs, SwitchBlade.UiaWorker/Program.cs): streaming NDJSON over stdio, parent-PID watchdog in the worker, client-side timeout + process-tree kill, stdout-pollution prevention, `IProviderRunner` strategy seam (OCP), and a null-object client for tests.
- **No `.Result`/`.Wait()` anywhere** — no WPF Dispatcher deadlock patterns; re-entrancy guards via `SemaphoreSlim.Wait(0)`; events emitted outside locks (WindowOrchestrationService.cs:182).
- **Strong testability seams**: `IProcessFactory`, `IFileSystem`, `IDelayProvider`, `IPeriodicTimer`, `IMemoryInfoProvider` abstract all system interactions.
- No TODO/FIXME debt, no hardcoded secrets, fail-fast constructor guards throughout.

### Structural problems (verified)

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| 1 | **Service hard-depends on a concrete view + magic string**: `Application.Current.MainWindow as MainWindow` → `FindName("ResultsConfig")` | Services/StoryboardBadgeAnimator.cs:119-124 | High (layering) |
| 2 | **Layer inversion**: service layer depends on presentation abstraction (`IWindowListViewModel`) | Services/INumberShortcutService.cs:18, NumberShortcutService.cs:32 | Medium |
| 3 | **"Contracts" is a shared kernel, not contracts** — ships ~10 implementation classes (NativeInterop ×5 partials, `CachingWindowProviderBase` 332 lines of behavior, registry I/O) plus WPF/UIA deps, forcing the worker to enable WPF | SwitchBlade.Contracts/ | Medium |
| 4 | **God base class**: every plugin inherits caching + LKG + all capability interfaces with no-op overrides (ISP/OCP violation) | SwitchBlade.Contracts/CachingWindowProviderBase.cs:21-96 | Medium |
| 5 | **Static mutable state**: `App.StartMinimized`/`EnableStartupOnFirstRun`/`IsModalDialogOpen` public static settable; 8+ classes call static `Logger.Log` instead of injected `ILogger` | App.xaml.cs:30-41, Core/Logger.cs:9-12 | Medium |
| 6 | **~500 lines of logic in view code-behind**: DWM backdrop, Win32 style manipulation, fade/force-open orchestration, direct VM mutation, native activation fallback | MainWindow.xaml.cs (whole file) | Medium |
| 7 | `goto EmitAndReturn` — violates the project's own "No goto" rule | Services/WindowOrchestrationService.cs:157,182 | Low (style) |
| 8 | **Bidirectional Core↔Services references** within one assembly; WPF converters/behaviors live in `Core/`; `Models/*.cs` declare `namespace SwitchBlade.Services` | multiple | Low |
| 9 | Plugin discovery duplicated across the process boundary with *inconsistent* filtering (main loads any `*.dll`, worker only `SwitchBlade.Plugins.*`) | Core/PluginLoader.cs:45 vs UiaWorker/Program.cs:326-341 | Medium (security) |
| 10 | Concrete-type DI for `ThemeService`, `WindowFinder`, `MemoryDiagnosticsService`; runners `new`'d inline in the composition root | Services/ServiceConfiguration.cs:50,124-125 | Low |

---

## 2. Code Quality & Safety

### Real issues (verified)

1. **Process-crash path via timer callback** — HIGH.
   `ScheduleSave()` uses a `System.Threading.Timer` whose callback runs on the thread pool (ViewModels/SettingsViewModel.cs:399-403). Inside, `SaveSettings()` calls `UpdateStartupRegistryEntry()` *outside* its try/catch (Services/SettingsService.cs:205) — any exception there escapes into a timer callback and **kills the process**. The cross-thread `PropertyChanged` raises are mostly safe (WPF marshals them), but this is still the most dangerous concurrency spot.

2. **Unguarded provider activation** — MEDIUM.
   `MainWindow.ActivateWindow` calls `windowItem.Source.ActivateWindow(windowItem)` with no try/catch (MainWindow.xaml.cs:473-496); a SEHException from P/Invoke on a dead HWND propagates into the WPF input handler → crash dialog, and the fade-out is skipped.

3. **HotKeyService lifecycle gaps** — MEDIUM.
   `Dispose()` never unsubscribes `_window.Loaded`/`Closing` (zombie global hotkey if disposed pre-load); the HwndHook invokes `_onHotKeyPressed` with no guard (Services/HotKeyService.cs:47,124-132).

4. **UiaWorkerClient dispose race** — MEDIUM / low-probability.
   `ObjectDisposedException.ThrowIf(_disposed)` runs outside the lock; interleaving with `Dispose()` can throw ODE from `CreateLinkedTokenSource` (Services/UiaWorkerClient.cs:81 vs 96,351-383).

5. **MemoryDiagnosticsService.Dispose** disposes its CTS without cancelling first → abandoned await at shutdown; no double-dispose guard (Services/MemoryDiagnosticsService.cs:49-70,127-132).

6. **Silent settings loss**: bare `catch { return defaultValue; }` in Services/RegistrySettingsStorage.cs:77-80,132-135 — corrupt registry values silently revert with no log, and the "heal" logic only re-saves *missing* keys (Services/SettingsService.cs:121-125).

7. **Mutable hash identity**: `WindowItem.Equals/GetHashCode` on mutable `(Hwnd, Title)` while instances live in HashSets — a persistent trap if any path mutates `Title` without remove/re-add (SwitchBlade.Contracts/WindowItem.cs:201-210).

8. **Plugin trust model**: any DLL dropped into `Plugins\` is `Assembly.LoadFrom`'d and instantiated — no signature/allowlist check; the main process doesn't even apply the worker's name-prefix filter (Core/PluginLoader.cs:45-51). Inherent to plugins, but worth documenting.

9. **Elevation relaunch argument corruption**: `string.Join(" ", args)` with no quoting (Program.cs:70); Services/RestartLogic.cs:29-55 builds PowerShell by string concatenation.

### ⚠️ Two findings investigated and **disproved** (do not "fix" these)

- **"OOB stack read in WindowFinder.cs:106 / plugin `buffer[..length]`"** — *false positive*. MSDN for `GetWindowTextW`: return value is the length of the **copied** string, capped at `nMaxCount-1`. Every call site passes `nMaxCount == buffer size` (Core/WindowFinder.cs:63, SwitchBlade.Plugins.Chrome/ChromeTabFinder.cs:142-144), so reads are always in-bounds. Optional hardening only.
- **"ShortcutDisplay off-by-one"** — *false positive*. `_shortcutIndex < 9` covers indices 0–8 → "1"–"9", index 9 → "0". Correct as written (SwitchBlade.Contracts/WindowItem.cs:131-141).

### Good practices worth keeping

- ReDoS protection via `RegexOptions.NonBacktracking` + LRU regex cache.
- Zero-allocation fuzzy matching with correct Span bounds discipline.
- Frozen `BitmapSource`s for cross-thread use; HICON cleanup in `finally`; DWM thumbnail unregistration on dispose.
- Single-instance mutex with 2s restart grace window.
- Per-provider exception isolation so one bad plugin can't sink a scan.

---

## 3. Testing & Build Health

- **735 test methods** (724 `[Fact]` + 11 `[Theory]`, 76 `InlineData`) across 59 files, xUnit 2.9 + Moq; no skipped tests; consistent `Method_Condition_ExpectedResult` naming; interface-based faking throughout.
- **Coverage**: latest scoped run (2026-06-29) = **100% line / 98.1% branch** — but plugins, Contracts, UiaWorker and all Views are *excluded* from measurement; a broader-scope run shows ~70.6%. Treat the headline number as "measured code," not thoroughness proof.
- **CI is solid**: `ci.yml` (build → test with coverage + blame-hang → ReportGenerator → GitHub Pages), `release.yml` (WiX MSI to GitHub Releases), `codeql.yml`, dependabot.
- **But: zero linting** — no Roslyn analyzers, no StyleCop/Roslynator, no `.editorconfig`, no `Directory.Build.props`, no `TreatWarningsAsErrors`. The only static analysis is CodeQL (security).
- **Test isolation weaknesses**:
  - `RegistryServiceWrapperTests` mutates the *live* registry.
  - `ProcessTests` spawns real `ping`/`cmd.exe`.
  - `LoggerTests` rewrites a static path with no `[Collection]` serialization (xUnit parallelism flake risk).
  - `WindowOrchestrationServiceTests` fakes log messages to satisfy assertions (implementation-detail testing).
  - Several weak `score > 0` / `DoesNotThrow`-only assertions.
  - ~8 files use sleep-based timing → CI flake risk.

---

## 4. Repo Hygiene

- ~50 untracked artifacts at root (build/test logs, gcdumps, 9 coverage dirs, PDFs) — all correctly gitignored, purely local clutter.
- Two strays: `CODE_REVIEW.md` and `CODEBASE_ASSESSMENT.md` are **uncommitted** (neither tracked nor ignored).
- Empty leftover `tests/` directory at root.

---

## 5. Prioritized Recommendations

1. **Crash path**: wrap `UpdateStartupRegistryEntry()` in try/catch or marshal the debounced save onto the dispatcher (SettingsViewModel.cs:399, SettingsService.cs:205) + regression test.
2. Guard provider activation in `MainWindow.ActivateWindow` and the HotKeyService hook invocation; fix HotKeyService event unsubscription on Dispose.
3. Extract `StoryboardBadgeAnimator.FindContainer`'s visual-tree lookup behind an abstraction injected from MainWindow (kills the service→view dependency).
4. Remove the `goto`; replace static `App.*` flags with an injected startup-options object; route all logging through `ILogger`.
5. Add a lint pipeline: Roslyn analyzers + `.editorconfig` + `TreatWarningsAsErrors` in CI — cheapest quality win available.
6. Fix test isolation (registry tests → mocked, `LoggerTests` into a `[Collection]`, replace fake-log assertions with behavior assertions).
7. Longer-term: split `CachingWindowProviderBase` into composable capabilities; introduce `IThemeService`; move MainWindow code-behind logic to a window-controller service; align plugin-loading policy between host and worker.
