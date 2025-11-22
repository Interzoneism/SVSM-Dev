# GitHub Copilot Instructions for ImprovedModMenu

## Project Overview
This is a **WPF desktop application** for managing Vintage Story mods. The application is called "Simple VS Manager" and provides a portable, user-friendly interface for:
- Browsing, installing, and managing Vintage Story mods
- Activating/deactivating mods
- Organizing mod load order
- Viewing mod metadata and dependencies

### Technology Stack
- **Framework**: .NET 8.0+ (Windows-targeted WPF application, currently builds with .NET 10.0.100)
- **UI Framework**: WPF with ModernWpfUI for modern styling
- **Architecture**: MVVM pattern using CommunityToolkit.Mvvm
- **Language**: C# with nullable reference types enabled
- **Main Dependencies**: CommunityToolkit.Mvvm, ModernWpfUI, HtmlAgilityPack, QuestPDF, YamlDotNet

## Repository Structure
- `VintageStoryModManager/` - Main application code (112 C# files, 29 XAML files, ~58MB)
  - `Views/` - WPF views and UI components (26 XAML files, including MainWindow.xaml with 13K+ lines)
  - `ViewModels/` - MVVM view models (13 C# files)
  - `Models/` - Data models (17 C# files)
  - `Services/` - Business logic and service layer (37 C# files)
  - `Converters/` - WPF value converters (8 C# files)
  - `Resources/` - UI resources, themes, and assets (DarkVsTheme.xaml, images)
  - `Properties/` - Application properties and settings
  - `Design/` - Design-time data
- `VSFOLDER/` - **Reference only**: Game dependency DLL files (~54MB, do not modify)
- `VS_1.21_assets/` - **Reference only**: Vintage Story game assets (~125MB, JSON for blocktypes, recipes, shapes, etc.)
- `VS_1.21_Decompiled/` - **Reference only**: Decompiled Vintage Story game source code (~25MB, for API reference)
- `.github/` - GitHub configuration and Copilot instructions

**IMPORTANT WARNINGS**:
- The folders `VSFOLDER`, `VS_1.21_assets`, and `VS_1.21_Decompiled` are reference code only (total ~204MB). Do not search, modify, or include them in changes unless explicitly requested.
- **There is NO ZZCakeBuild directory** - The `build.sh` and `build.ps1` scripts reference a non-existent `./ZZCakeBuild/CakeBuild.csproj` and will fail. Use `dotnet build` directly instead.
- The repository contains 3808+ C# files total (most in reference directories), but only 112 in the main project.

## Build & Test

### Prerequisites
- **.NET SDK**: 8.0 or later (tested with 10.0.100)
- **Platform**: Windows (Linux/Mac build will fail due to WPF dependency)
- **First time setup**: Run `dotnet restore` to restore NuGet packages (~4 seconds)

### Build Commands - VALIDATED AND WORKING
```bash
# Primary build command (ALWAYS USE THIS - verified working)
dotnet build -nologo -clp:Summary -warnaserror

# Build without treating warnings as errors (for exploration)
dotnet build -nologo -clp:Summary

# Clean build artifacts
dotnet clean --nologo

# Restore packages (usually automatic, but can be run explicitly)
dotnet restore --nologo
```

**Build Performance**:
- Clean build: ~17-25 seconds
- Incremental build: ~15 seconds
- Output: `VintageStoryModManager/bin/Release/net8.0-windows/VintageStoryModManager.dll` (1.7MB PE32 executable)
- The DLL is the main executable file - on Windows this can be run directly

**CRITICAL - DO NOT USE**:
- **DO NOT** run `./build.sh` or `build.ps1` - These scripts reference a non-existent `ZZCakeBuild/CakeBuild.csproj` and will fail with "The provided file path does not exist"
- Always use `dotnet build` directly

### Test Commands
```bash
# Run tests (currently NO tests exist in the solution)
dotnet test --nologo --verbosity=minimal
```
**Note**: Running `dotnet test` completes successfully but runs zero tests. Do not be alarmed by this - the project has no test project.

### Linting & Formatting
```bash
# Format verification (CORRECT SYNTAX - note that --nologo doesn't work with this command)
dotnet format --verify-no-changes

# Auto-fix formatting issues (will modify files)
dotnet format
```
**Warning**: The codebase currently has whitespace formatting issues. Running `dotnet format --verify-no-changes` will show many WHITESPACE errors, primarily in `ViewModels/MainViewModel.cs` and `ViewModels/ModConfigNodeViewModels.cs`. This is expected - format checking is optional unless you're modifying those specific files.

### Important Build Notes
- **Target Framework**: .NET 8.0 for Windows (`net8.0-windows`)
- **Output Type**: WinExe (Windows application, not console)
- **Ignore NET7 warnings**: Focus is on NET8; all NET7 compatibility warnings should be ignored
- **Solution File**: `ImprovedModMenu.sln` (contains single project: VintageStoryModManager)
- **Project File**: `VintageStoryModManager/VintageStoryModManager.csproj`
- **Main Assembly**: VintageStoryModManager.dll (but displays as "Simple VS Manager" in Task Manager)
- **Version Management**: Version is centrally managed in `Directory.Build.props` (currently 1.6.3)
- **Icon**: `VintageStoryModManager/SVSM.ico` (169KB)

## Code Style & Conventions

### C# Guidelines
- Use **nullable reference types** (enabled project-wide)
- Use **implicit usings** (enabled in project)
- Follow **MVVM pattern** for UI logic separation
- Use **CommunityToolkit.Mvvm** for command and observable property implementations
- Prefer **file-scoped namespaces** when appropriate
- Use **descriptive variable names** that clearly indicate purpose
- Keep methods focused and reasonably sized

### XAML Guidelines
- Follow WPF best practices for data binding
- Use **ModernWpfUI** controls for consistent modern styling
- Leverage resource dictionaries for theming (see `Resources/Themes/`)
- Use value converters for complex binding scenarios

### Naming Conventions
- ViewModels: `*ViewModel.cs`
- Views: `*.xaml` with code-behind `*.xaml.cs`
- Services: Descriptive names in `Services/` folder
- Models: Business entities in `Models/` folder

## Vintage Story API Integration

### When Working with Vintage Story APIs
- **Always check** the corresponding file in `VS_1.21_Decompiled/` when:
  - Using Vintage Story API classes, methods, or properties
  - Understanding game behavior or mod structure
  - Implementing features that interact with the game
- The decompiled source is from the **latest compatible game version** (1.21) and can be fully trusted
- Game assets in `VS_1.21_assets/` contain accurate JSON schemas for blocks, items, recipes, etc.

### Reference Code Usage
- Reference code exists to help you understand the Vintage Story ecosystem
- Only search/reference these folders when explicitly mentioned or when working on game integration features
- Never modify files in reference folders

## Security & Best Practices

### Security Guidelines
- **Never commit secrets** or API keys to the repository
- Handle user data and file paths securely
- Validate all user inputs, especially file paths and mod metadata
- Be cautious with file operations (reading/writing mod files)
- Use safe XML/JSON parsing for mod metadata

### Error Handling
- Implement proper exception handling for file I/O operations
- Provide meaningful error messages to users
- Use the `ModManagerMessageBox` service for user notifications
- Log errors appropriately for debugging

### Performance Considerations
- Optimize UI responsiveness for large mod collections
- Use async/await for I/O operations
- Implement lazy loading where appropriate
- Consider memory usage when loading mod metadata

## Contributing Workflow

### Making Changes
1. Focus on **minimal, surgical changes** to accomplish the goal
2. Maintain consistency with existing code patterns
3. Update related documentation if needed
4. Test changes thoroughly before committing

### Testing Requirements
- Build must succeed without warnings (using `-warnaserror`)
- Manually test UI changes when applicable
- Verify no breaking changes to existing functionality
- Test with actual Vintage Story mod files when relevant

### Pull Request Standards
- Clear, descriptive commit messages
- Reference related issues
- Include context about what changed and why
- Keep changes focused and scoped

## Known Constraints & Limitations

### Platform
- **Windows-only application** (WPF is Windows-specific)
- Requires .NET 8.0 runtime
- Interacts with local file system for mod management

### Development Environment
- Designed for Visual Studio or VS Code with C# extensions
- Requires Windows SDK for WPF development
- Git for version control

## Additional Notes
- **Project Name**: The assembly is named `VintageStoryModManager` but displays as "Simple VS Manager" in Windows Task Manager
- **Company**: Interzone  
- **Version**: Managed centrally in `Directory.Build.props` (currently 1.6.3)
- **Icon**: `SVSM.ico` in the VintageStoryModManager directory (169KB)
- **File Description**: Shows as "Simple VS Manager" in Windows Task Manager
- **Key configuration**: DevConfig.cs contains developer-tunable values (like MaxConcurrentDatabaseRefreshes)
- **No .editorconfig**: The repository does not have an .editorconfig file for code style rules
- **Implicit usings**: Enabled in the project - common namespaces are automatically imported
- **Application manifest**: `app.manifest` defines Windows compatibility and permissions

## Root Directory Files
```
.gitattributes          - Git attributes configuration
.github/                - Contains copilot-instructions.md
.gitignore              - Comprehensive C#/Visual Studio gitignore (13KB)
AGENTS.md               - Custom agent instructions (1KB)
Directory.Build.props   - Central version management (AppVersion: 1.6.3)
ImprovedModMenu.sln     - Visual Studio solution file
build.ps1               - BROKEN: References non-existent ZZCakeBuild
build.sh                - BROKEN: References non-existent ZZCakeBuild  
firebase.rules.json     - Firebase security rules configuration
SIMPLE VS MANAGER PUBLIC.lnk - Windows shortcut file
```

## Common Pitfalls & Solutions

### Build Script Failures
**Problem**: Running `./build.sh` or `build.ps1` fails with "The provided file path does not exist"  
**Solution**: Use `dotnet build` directly. The build scripts reference a non-existent Cake build project.

### Whitespace Formatting Warnings
**Problem**: `dotnet format --verify-no-changes` shows many WHITESPACE errors  
**Solution**: This is expected. The codebase has existing formatting issues. Only fix formatting in files you're actively modifying.

### No Tests Found
**Problem**: `dotnet test` reports 0 tests  
**Solution**: This is correct. The project has no test project. Don't add tests unless explicitly requested.

### Reference Directory Search
**Problem**: Searches are slow or include too many irrelevant files  
**Solution**: Exclude VS_1.21_Decompiled, VS_1.21_assets, and VSFOLDER directories from searches. These contain 3696 C# files but are reference-only.

### NET7 Compatibility Warnings
**Problem**: Build shows warnings about .NET 7 compatibility  
**Solution**: Ignore these. The project targets .NET 8 and NET7 warnings are expected and harmless.

## Validation Steps
When making changes, follow this sequence:
1. Make your code changes
2. Run `dotnet build -nologo -clp:Summary -warnaserror` to verify no errors/warnings
3. If you modified formatting-sensitive files, optionally run `dotnet format` to auto-fix whitespace
4. Manually test your changes if they affect UI or critical functionality
5. Commit with clear, descriptive messages

## Trust These Instructions
These instructions have been validated through actual testing of the repository. The build commands, timings, and warnings documented here are accurate as of the last update. Only search for additional information if you find these instructions incomplete or incorrect for your specific task.