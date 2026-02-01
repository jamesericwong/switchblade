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
- `SwitchBlade.Contracts/`: Interface definitions for plugins.
- `SwitchBlade.Plugins.Chrome/`: The Chrome tab finder plugin.
- `SwitchBlade.UiaWorker/`: The out-of-process worker for UIA scans.
- `Installer/SwitchBlade.Installer.wixproj`: The WiX installer project.

## Building with Visual Studio

1. Open `SwitchBlade.sln` in Visual Studio 2022.
2. Select the **Release** configuration and **x64** platform.
3. Right-click on the `SwitchBlade` project and select **Build**.
   - This will build the core application and trigger the build of referenced projects.
   - Note: The Chrome plugin is set up to copy its output to the `bin\Release\net9.0-windows\Plugins` directory via a post-build event.

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
3. The resulting `.msi` will be in `Installer\bin\Release\en-US\SwitchBlade.msi`.

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
1. Reference `SwitchBlade.Contracts.csproj`.
2. Ensure your build output (usually a `.dll`) is copied to a folder named `Plugins` in the same directory as `SwitchBlade.exe`.
3. The main application uses `Directory.GetFiles` to look for `*.dll` files in the `Plugins` subfolder at runtime.

### Example Plugin Build Step
The existing Chrome plugin uses this post-build event in its `.csproj`:

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /F &quot;$(TargetDir)$(TargetName).dll&quot; &quot;$(MSBuildProjectDirectory)\..\bin\$(Configuration)\net9.0-windows\Plugins\&quot;" />
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

# Run tests with code coverage (requires coverlet)
dotnet test SwitchBlade.Tests/SwitchBlade.Tests.csproj --collect:"XPlat Code Coverage"
```

### Test Structure

| Directory | Description |
|-----------|-------------|
| `Core/` | Tests for `PluginInfo`, `PluginLoader`, `WindowFinder`, `Logger`, `LoggerBridge` |
| `Services/` | Tests for `UserSettings`, `ThemeInfo`, `ThemeService` |
| `ViewModels/` | Tests for `RelayCommand`, `MainViewModel`, `SettingsViewModel` |
| `Contracts/` | Tests for `WindowItem` |

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
