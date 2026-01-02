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

## Troubleshooting

- **WiX Build Errors**: Ensure WiX v5 is installed and the required extensions (`WixToolset.UI.wixext`, `WixToolset.Util.wixext`) are registered.
- **Missing Plugins**: If plugins don't show up, check if they are in the `Plugins` folder relative to the executable and that they implement `IWindowProvider`.
- **Reference Errors**: Ensure all NuGet packages are restored (`dotnet restore`).
