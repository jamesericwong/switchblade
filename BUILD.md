# Building SwitchBlade

This document provides detailed instructions on how to build the SwitchBlade project from source, including the application, its plugins, and the installer.

## Prerequisites

Before building SwitchBlade, ensure you have the following installed on your system:

- **Visual Studio 2022**: Version 17.8 or later is recommended.
- **.NET 9 SDK**: The project targets `net9.0-windows`.
- **WiX Toolset v5**: Required for building the installer. You can install it via dotnet tool:
  ```powershell
  dotnet tool install --global wix
  ```
  And ensure the WiX SDK is available:
  ```powershell
  wix extension add WixToolset.UI.wixext
  wix extension add WixToolset.Util.wixext
  ```

## Project Structure

- `SwitchBlade.sln`: The main solution file.
- `SwitchBlade.csproj`: The main WPF application project.
- `SwitchBlade.Contracts/`: Shared plugin contracts + non-UI helpers (WPF-free).
- `SwitchBlade.Contracts.Uia/`: Shared UIA helper assembly (`UiaElementResolver`) — the only plugin-facing assembly that requires WPF.
- `SwitchBlade.Plugins.Chrome/`: The Chrome tab finder plugin.
- `SwitchBlade.Plugins.Teams/`: The Microsoft Teams chat list plugin.
- `SwitchBlade.Plugins.WindowsTerminal/`: The Windows Terminal tab plugin.
- `SwitchBlade.Plugins.NotepadPlusPlus/`: The Notepad++ tab plugin.
- `SwitchBlade.UiaWorker/`: The out-of-process worker for UIA scans.
- `SwitchBlade.Tests/`: The xUnit test suite (1071 tests, 100% line coverage).
- `Installer/SwitchBlade.Installer.wixproj`: The WiX installer project.

## Building with Visual Studio

1. Open `SwitchBlade.sln` in Visual Studio 2022.
2. Select the **Release** configuration and **x64** platform.
3. Right-click on the `SwitchBlade` project and select **Build**.
   - This will build the core application and trigger the build of referenced projects, including `SwitchBlade.UiaWorker`.
   - Note: Both the core app and the worker search for plugins in the `Plugins` sub-directory relative to their executable.

## Building with .NET CLI

You can build the entire solution from the command line:

```powershell
dotnet build SwitchBlade.sln -c Release
```

To build and publish the main application as a self-contained unit:

```powershell
dotnet publish SwitchBlade.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

## Manual High-Performance (R2R) Deployment

For computers where an MSI cannot be run, you can create a high-performance, self-contained binary using ReadyToRun (R2R) compilation.

### 1. Build the R2R Package
Run the following command from the project root:

```powershell
dotnet publish SwitchBlade.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**Key Parameters:**
- `-r win-x64`: Targets 64-bit Windows.
- `--self-contained true`: Bundles the .NET runtime (app runs without .NET installed).
- `-p:PublishReadyToRun=true`: Enables AOT-style native compilation for instant startup.
- `-p:PublishSingleFile=true`: Merges the app and runtime into a single executable.

### 2. Locate Published Files
The artifacts will be generated in:
`bin\Release\net9.0-windows\win-x64\publish\`

### 3. Manual Installation
1.  **Copy Files**: Copy the entire contents of the `publish` folder to the target machine (e.g., `C:\Tools\SwitchBlade`).
2.  **Verify Plugins**: Ensure the `Plugins` subfolder is present and contains the plugin DLLs. The build system automatically compiles and bundles these into the `Plugins` folder during the `publish` command, so no manual copying of individual plugin DLLs is required.
3.  **Run**: Execute `SwitchBlade.exe`.

## Building the Installer

The installer project (`SwitchBlade.Installer.wixproj`) is configured to automatically publish the main application before building the MSI.

### Using Visual Studio
1. Right-click on the `SwitchBlade.Installer` project.
2. Select **Build**.
3. The resulting `.msi` will be in `Installer\bin\Release\SwitchBlade.msi`.

### Using the CLI
From the root directory:


```powershell
cd Installer
dotnet build -c Release
```

### Building R2R Installer (MSI)

To build an MSI installer that deploys the Single-File R2R executable (High Performance):

```powershell
cd Installer
dotnet build -c Release -p:PublishR2R=true
```


## Plugin Development

If you are developing a new plugin:
1. Reference `SwitchBlade.Contracts.csproj` — and also `SwitchBlade.Contracts.Uia.csproj` if your plugin uses UI Automation (`IsUiaProvider = true`).
2. Ensure your build output (usually a `.dll`) is copied to a folder named `Plugins` in the same directory as `SwitchBlade.exe`.
3. Both the main application and the UIA worker discover plugins via the shared `SwitchBlade.Contracts.PluginDiscovery` routine — recursively under the `Plugins` folder, loading only DLLs whose name starts with `SwitchBlade.Plugins.` (case-insensitive); anything else is silently ignored.

### Example Plugin Build Step
The existing Chrome plugin uses this post-build target in its `.csproj`:

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <MakeDir Directories="$(MSBuildProjectDirectory)\..\bin\$(Configuration)\net9.0-windows\Plugins" />
  <Copy SourceFiles="$(TargetDir)$(TargetName).dll" DestinationFolder="$(MSBuildProjectDirectory)\..\bin\$(Configuration)\net9.0-windows\Plugins\" />
</Target>
```

## Unit Testing

The project includes a comprehensive xUnit test suite in `SwitchBlade.Tests/`.

### Running Tests

**Using Visual Studio:**
1. Open `SwitchBlade.sln` in Visual Studio 2022.
2. Open **Test > Test Explorer**.
3. Click **Run All** to execute all tests.

**Using .NET CLI:**
```powershell
# Run all tests
dotnet test SwitchBlade.Tests/SwitchBlade.Tests.csproj

# Run tests with detailed output
dotnet test SwitchBlade.Tests/SwitchBlade.Tests.csproj --verbosity normal

# Run tests with code coverage (same coverage flags as CI — ci.yml additionally passes --no-build and --blame-hang and runs against the Debug build; the runsettings file scopes the report to the main app and emits Cobertura XML)
dotnet test SwitchBlade.sln -c Release --collect:"XPlat Code Coverage" --settings CodeCoverage.runsettings --results-directory ./coverage
```

### Coverage pitfalls (learned the hard way — don't repeat these)

- **Do not use `/p:CollectCoverage=true` / `-p:CoverletOutputFormat=cobertura`.** With `coverlet.collector` 10.x on .NET 9, tests run green but **no data collector is registered at all** — no error, no warning, no report file appears. The correct switch is `--collect:"XPlat Code Coverage"` (the collector's friendly name).
- **Do not use `dotnet test --coverage`.** That flag only exists in .NET 10 SDKs; on .NET 9 it fails with `MSB1001: Unknown switch`.
- **Always pass `--settings CodeCoverage.runsettings`.** Without it, collection still works but the scope balloons to include all plugins, XAML views/code-behind, and compiler-generated code — rates drop to ~line 83% / branch 76%, which is a *scope error*, not a regression. The runsettings exclusions define the "main app logic" scope we track (target: line = 100%).
- **The output folder has a random GUID name.** Per run, expect `<results-directory>\<guid>\coverage.cobertura.xml` (default location without `--results-directory`: `SwitchBlade.Tests/TestResults/`). Take the newest file when parsing.
- **Header `branch-rate` sits at 0.9974 even though every reported line/branch point is fully covered — this is forensically investigated (2026-08-27), not hand-waved.** Evidence: per-method branch sums in the OpenCover report are 1108/1108 visited; removing `[ExcludeFromCodeCoverage]` from `StoryboardBadgeAnimator` to expose its data showed the *only* uncovered code anywhere is WPF visual-tree glue (`FindChild` + the apply path that needs a realized container) — and doing so dropped line-rate to **97.47%**, violating our "never drop below main" rule, so the exclusion stays (it was the right design: headless xUnit can't realize ListBoxItem templates). An explicit type-level `<Exclude>` entry had no effect on coverlet v10's summary aggregation and was reverted — don't add one. **Gate standard = line-rate 1.0 + zero partial branch lines** (both true at tip); header ≈ 0.997 is the accepted floor. Only remaining levers for a literal 1.0: swapping the coverlet dependency version or building a live-visual-tree UI test harness — both rejected as disproportionate to an artifact number.

### Test Structure

| Directory | Description |
|-----------|-------------|
| `Core/` | Tests for `PluginLoader`, `WindowFinder`, `FuzzyMatcher`, `LruRegexCache`, `NumberShortcutService`, `Logger`, ... |
| `Services/` | Tests for `SettingsService`, `HotKeyService`, `BackgroundPollingService`, `BadgeAnimationService`, `UiaWorkerClient`, `WindowOrchestrationService`, ... |
| `ViewModels/` | Tests for `RelayCommand`, `MainViewModel`, `SettingsViewModel` |
| `Contracts/` | Tests for `WindowItem`, `CachingScanCoordinator`, `LastKnownGoodStrategy`, `CachingWindowProviderBase` |
| `Handlers/` | Tests for `WindowControllerService`, `KeyboardInputHandler` |
| `Plugins/` | Tests for the bundled plugins (Chrome, Teams, Windows Terminal, Notepad++) |

### Writing New Tests

1. Add a new test class in the appropriate subdirectory.
2. Use `[Fact]` for simple tests and `[Theory]` for parameterized tests.
3. Follow the pattern: `ClassName_MethodOrProperty_ExpectedBehavior`.
4. Use **Moq** for mocking dependencies.

Example:
```csharp
[Fact]
public void MyClass_MyMethod_ReturnsExpectedValue()
{
    var sut = new MyClass();
    var result = sut.MyMethod();
    Assert.Equal("expected", result);
}
```

## Troubleshooting

- **WiX Build Errors**: Ensure WiX v5 is installed and the required extensions (`WixToolset.UI.wixext`, `WixToolset.Util.wixext`) are registered.
- **Missing Plugins**: If plugins don't show up, check if they are in the `Plugins` folder relative to the executable and that they implement `IWindowProvider`.
- **Reference Errors**: Ensure all NuGet packages are restored (`dotnet restore`).
- **Test Failures**: Run `dotnet restore SwitchBlade.Tests/SwitchBlade.Tests.csproj` to restore test dependencies.
