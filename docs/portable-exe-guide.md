# Creating a Portable Windows Executable in Visual Studio 2022

The Improved Mod Menu is a .NET application. To give other players a "download and run" experience on Windows, publish a self-contained, single-file build. Visual Studio 2022 can generate this package for you in a few clicks.

## 1. Prepare the project
1. Open `ImprovedModMenu.sln` in Visual Studio 2022.
2. Set the build configuration to **Release** and the target framework to **net8.0** (our primary target).
3. Build the solution once to make sure it compiles without errors (Build → Build Solution).

## 2. Create a publish profile
1. Right-click the `ImprovedModMenu` project in **Solution Explorer** and choose **Publish…**
2. In the publish target dialog:
   - Select **Folder** and click **Next**.
   - Choose a destination folder such as `Publish\win-x64` inside your repository.
   - Press **Finish** to create the profile.

Visual Studio adds a `FolderProfile.pubxml` inside `ImprovedModMenu/Properties/PublishProfiles`. You can re-use it whenever you need a portable build.

## 3. Configure the profile for a portable .exe
1. With the profile selected in the **Publish** tool window, click **Show all settings**.
2. Set the following options:
   - **Configuration**: `Release`
   - **Target framework**: `net8.0`
   - **Target runtime**: `win-x64` (or `win-x86` if you specifically need 32-bit)
   - **Deployment mode**: `Self-contained`
   - **Produce single file**: `True`
   - (Optional) **ReadyToRun compilation**: `True` for faster startup on end-user machines
3. Save the profile.

These settings bundle the .NET runtime and all managed dependencies into a single executable so the end user does not need to install anything.

## 4. Publish the build
1. Click **Publish** in the Publish tool window.
2. Wait for Visual Studio to finish. The output folder now contains files similar to:
   - `ImprovedModMenu.exe` (the portable executable)
   - Content files that cannot be embedded (JSON, images, etc.)

If the build fails, open the **Output** window for details. Resolve any errors and publish again.

## 5. Ship the deliverable
1. Zip the entire publish output folder (including `ImprovedModMenu.exe` and any remaining assets).
2. Share the zip with your users. They can extract and run `ImprovedModMenu.exe` immediately on Windows 10/11 x64.

## 6. Command-line equivalent (optional)
You can create the same package from a terminal:

```powershell
# From the repository root
dotnet publish ImprovedModMenu/ImprovedModMenu.csproj ^
  -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:PublishReadyToRun=true
```

Adjust the runtime identifier (`-r`) if you need a different architecture. The output will be written to `ImprovedModMenu/bin/Release/net8.0/win-x64/publish/`.

## Troubleshooting tips
- **DLLs or JSON assets are missing**: ensure `Copy to Output Directory` is set to `Copy always` or `Copy if newer` for those files.
- **Antivirus false positives**: Single-file executables sometimes trigger antivirus warnings. Code-signing the executable or distributing the zipped folder can reduce these alerts.
- **File size is large**: self-contained deployments include the .NET runtime (~100 MB). You can distribute a framework-dependent build instead, but the user must have the matching .NET runtime installed.

Following the steps above yields a portable `.exe` that anyone on a compatible Windows machine can run without additional installers or prerequisites.
