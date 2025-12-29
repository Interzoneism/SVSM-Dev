# Changelog

All notable changes to Simple VS Manager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - TBD

### Major Release
This is a major release representing significant improvements and optimizations to Simple VS Manager.

### Performance Improvements
- Optimized startup performance with improved database refresh strategies
- Reduced concurrent operations limits for better UI responsiveness during heavy load
- Implemented batched database info updates for smoother UI performance
- Enhanced incremental mod loading with optimized batch sizes
- Improved parallel mod discovery with increased batch size from 16 to 32

### Architecture Improvements
- Updated to .NET 8.0 framework
- Enhanced MVVM architecture with CommunityToolkit.Mvvm 8.4.0
- Modern WPF UI with ModernWpfUI 0.9.6
- Improved service layer organization and separation of concerns

### Technical Details
- **Framework**: .NET 8.0 for Windows
- **UI**: WPF with ModernWpfUI theming
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Dependencies**: Updated to latest stable versions
  - QuestPDF 2025.12.0
  - YamlDotNet 16.3.0
  - HtmlAgilityPack 1.12.4

### Developer Experience
- Improved code formatting and consistency
- Enhanced error handling and logging
- Better configuration management
- Single-file publish support for easier distribution

### Configuration Changes
- MaxConcurrentDatabaseRefreshes: 8 → 4
- MaxConcurrentUserReportRefreshes: 8 → 4
- InstalledModsIncrementalBatchSize: 64 → 32
- ModDiscoveryBatchSize: 16 → 32

---

## [1.4.0] - Previous Release

For changes prior to 2.0.0, please refer to the git commit history.
