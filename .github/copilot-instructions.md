# GitHub Copilot Instructions for ImprovedModMenu

## Project Overview
This is a **WPF desktop application** for managing Vintage Story mods. The application is called "Simple VS Manager" and provides a portable, user-friendly interface for:
- Browsing, installing, and managing Vintage Story mods
- Activating/deactivating mods
- Organizing mod load order
- Viewing mod metadata and dependencies

### Technology Stack
- **Framework**: .NET 8.0 (Windows-targeted WPF application)
- **UI Framework**: WPF with ModernWpfUI for modern styling
- **Architecture**: MVVM pattern using CommunityToolkit.Mvvm
- **Language**: C# with nullable reference types enabled

## Repository Structure
- `VintageStoryModManager/` - Main application code (118 C# files, 23 XAML files)
  - `Views/` - WPF views and UI components
  - `ViewModels/` - MVVM view models
  - `Models/` - Data models
  - `Services/` - Business logic and service layer
  - `Converters/` - WPF value converters
  - `Resources/` - UI resources, themes, and assets
- `VSFOLDER/` - **Reference only**: Game dependency DLL files (do not modify)
- `VS_1.21_assets/` - **Reference only**: Vintage Story game assets (JSON for blocktypes, recipes, shapes, etc.)
- `VS_1.21_Decompiled/` - **Reference only**: Decompiled Vintage Story game source code for API reference
- `ZZCakeBuild/` - Build automation scripts (do not build or test directly)
- `.github/` - GitHub configuration and Copilot instructions

**Important**: The folders `VSFOLDER`, `VS_1.21_assets`, and `VS_1.21_Decompiled` are reference code only. Do not search, modify, or include them in changes unless explicitly requested.

## Build & Test

### Build Commands
```bash
# Primary build command (recommended)
dotnet build -nologo -clp:Summary -warnaserror

# The build.sh and build.ps1 scripts run Cake build automation - do not use for regular builds
```

### Test Commands
```bash
# Run tests (if any exist)
dotnet test --nologo --verbosity=minimal
```

### Linting (Optional)
```bash
# Format verification
dotnet format --verify-no-changes
```

### Important Build Notes
- **Target Framework**: .NET 8.0 for Windows
- **Ignore NET7 warnings**: Focus is on NET8; all NET7 compatibility warnings should be ignored
- **Do not build ZZCakeBuild**: The Cake build project and Program.cs are for automation only
- **Solution File**: `ImprovedModMenu.sln`
- **Output**: Windows executable named `VintageStoryModManager.exe`

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
- **Project Name**: The assembly is named `VintageStoryModManager` but displays as "Simple VS Manager"
- **Company**: Interzone
- **Version**: Managed centrally in `Directory.Build.props` (currently 1.4.0)
- **Icon**: `SVSM.ico` is the application icon
- **File Description**: Shows as "Simple VS Manager" in Windows Task Manager

## Questions & Support
- Review `AGENTS.md` for custom agent configurations
- Check the solution file `ImprovedModMenu.sln` for project structure
- Consult WPF and ModernWpfUI documentation for UI-related questions