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

## Build & Test

### Build Commands
```bash
# Primary build command (recommended)
dotnet build -nologo -clp:Summary -warnaserror
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
- **Solution File**: `ImprovedModMenu.sln`
- **Output**: Windows executable named `Simple VS Manager.exe`

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

## Contributing Workflow

### Making Changes
1. Focus on the big picture - what is this trying to accomplish and what issues could be solved or what improvements could be made to get there?
2. Question functions or workflows that do not make sense, these could be legacy or left over code by other agents
3. Test changes thoroughly before committing

### Testing Requirements
- Build must succeed without warnings (using `-warnaserror`)
- Verify no breaking changes to existing functionality

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
- Check the solution file `ImprovedModMenu.sln` for project structure
- Consult WPF and ModernWpfUI documentation for UI-related questions
