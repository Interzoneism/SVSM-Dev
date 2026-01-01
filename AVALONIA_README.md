# Avalonia Conversion Documentation

## Overview

This directory contains comprehensive documentation for converting the **Simple VS Manager** from WPF to Avalonia UI.

---

## Documents

### 1. [AVALONIA_CONVERSION_REPORT.md](AVALONIA_CONVERSION_REPORT.md)
**Strategic Conversion Plan** - 25KB

The main strategic document covering:
- **Executive Summary**: Current state analysis
- **Part 1: Pre-Conversion Refactoring**: How to split MainWindow into components
- **Part 2: Conversion Order and Steps**: 6-phase conversion approach
- **Part 3: Code-Behind Migration**: What to convert vs. what can stay
- **Part 4: Conversion Roadmap**: 20-week timeline estimate
- **Part 5: Recommended Next Steps**: Actionable items
- **Part 6: Additional Recommendations**: Best practices

**Read this first** to understand the overall strategy.

---

### 2. [AVALONIA_TECHNICAL_GUIDE.md](AVALONIA_TECHNICAL_GUIDE.md)
**Technical Code Examples** - 25KB

Detailed technical guide with code examples:
- **XAML Syntax Differences**: Window declarations, resources, namespaces
- **Namespace Mappings**: Complete mapping of WPF to Avalonia namespaces
- **Control Conversions**: Control-by-control conversion guide
- **Styling Differences**: Triggers → Pseudo-classes
- **Data Binding**: Standard and compiled bindings
- **Event to Command Migration**: Converting event handlers to MVVM commands
- **Dependency Properties**: Converting to styled properties
- **Visual Tree Navigation**: WPF VisualTreeHelper → Avalonia methods
- **File Dialogs**: Complete file dialog examples
- **Drag and Drop**: Full drag/drop implementation
- **Canvas and Positioning**: Layout and positioning
- **DataGrid Migration**: DataGrid-specific conversions
- **ModernWpfUI Alternatives**: Recreating styled controls

**Use this** as a reference while converting code.

---

### 3. [AVALONIA_CONVERSION_CHECKLIST.md](AVALONIA_CONVERSION_CHECKLIST.md)
**Conversion Checklists** - 13KB

Step-by-step checklists for the conversion process:
- **Pre-Conversion Checklist**: Component extraction tasks
- **Avalonia Setup Checklist**: Project setup steps
- **Component Conversion Checklist**: Universal checklist for converting any component
- **Specific Component Checklists**: Menu, DataGrid, File Dialogs, Drag/Drop, Custom Controls
- **Platform-Specific Testing**: Windows, Linux, macOS verification
- **Common Pitfalls**: Mistakes to avoid
- **Best Practices**: Recommended approaches
- **Progress Tracking Template**: Copy-paste template for tracking progress
- **Quick Reference**: Common conversion patterns

**Use this** during the actual conversion work to ensure you don't miss steps.

---

## Quick Start

### For Project Managers / Stakeholders

1. Read **AVALONIA_CONVERSION_REPORT.md** sections:
   - Executive Summary
   - Part 4: Conversion Roadmap (Timeline)
   - Part 6: Conclusion

**Key Takeaway**: ~5 months (20 weeks) for full conversion with proper planning.

### For Developers Starting Pre-Conversion Refactoring

1. Read **AVALONIA_CONVERSION_REPORT.md** → Part 1: Pre-Conversion Refactoring
2. Use **AVALONIA_CONVERSION_CHECKLIST.md** → Pre-Conversion Checklist
3. Start with extracting the **Menu System** first

### For Developers Converting Components

1. Review **AVALONIA_TECHNICAL_GUIDE.md** relevant sections
2. Follow **AVALONIA_CONVERSION_CHECKLIST.md** → Component Conversion Checklist
3. Refer to **Quick Reference** for common patterns
4. Test each component thoroughly before moving to the next

---

## Conversion Strategy Summary

### Phase 0: Pre-Conversion (4 weeks)
**Goal**: Split MainWindow into reusable components in WPF

**Deliverables**:
- MainMenu.xaml (~450 lines extracted)
- ModInfoPanel.xaml (~300 lines extracted)
- ProgressOverlay.xaml (~200 lines extracted)
- FilterBar.xaml (~250 lines extracted)
- Tab components (~1,200 lines extracted)
- StatusBar.xaml (~150 lines extracted)
- **Result**: MainWindow reduced from 2,575 lines → ~525 lines (79.6% reduction)

### Phase 1: Foundation (2 weeks)
**Goal**: Set up Avalonia project structure

**Deliverables**:
- Avalonia MVVM project created
- Themes configured (Fluent theme + custom styles)
- Shared code (ViewModels, Services, Models) copied
- Build succeeds

### Phase 2: Simple Components (2 weeks)
**Goal**: Convert simplest components to build confidence

**Deliverables**:
- Status Bar (Avalonia)
- Progress Overlays (Avalonia)
- Filter Bar (Avalonia)

### Phase 3: Complex Components (3 weeks)
**Goal**: Convert components with complex interactions

**Deliverables**:
- Menu System (Avalonia)
- Mod Info Panel (Avalonia)

### Phase 4: Tab Content (3 weeks)
**Goal**: Convert the main content areas

**Deliverables**:
- Installed Mods Tab with DataGrid (Avalonia)
- Modlists Tab (Avalonia)
- Mod Browser Tab (Avalonia)

### Phase 5: MainWindow Assembly (2 weeks)
**Goal**: Integrate all components into MainWindow

**Deliverables**:
- MainWindow.axaml assembled
- Window lifecycle converted
- Integration tested

### Phase 6: Platform Features (2 weeks)
**Goal**: Cross-platform testing and optimization

**Deliverables**:
- File dialogs working
- Clipboard operations working
- External process launching working
- Platform-specific testing complete

### Testing & Refinement (2 weeks)
**Goal**: Bug fixes, performance tuning, polish

**Deliverables**:
- All tests passing
- No regressions
- Performance optimized
- Ready for release

---

## Key Statistics

### Current WPF Codebase

| Component | Lines |
|-----------|-------|
| MainWindow.xaml | 2,575 |
| MainWindow.xaml.cs | 14,232 |
| UI Elements | ~643 |

### After Pre-Conversion Refactoring

| Component | Lines (XAML) |
|-----------|--------------|
| MainWindow.xaml | ~525 |
| MainMenu.xaml | ~450 |
| ModInfoPanel.xaml | ~300 |
| ProgressOverlay.xaml | ~200 |
| FilterBar.xaml | ~250 |
| Tab Components | ~1,200 |
| StatusBar.xaml | ~150 |

### Code-Behind Breakdown

| Category | Lines | Strategy |
|----------|-------|----------|
| Business Logic | 5,000 (35%) | **Keep** - No changes |
| Data Structures | 500 (3.5%) | **Keep** - No changes |
| Utilities | 800 (5.6%) | **Keep** - No changes |
| UI Interaction | 3,500 (24.6%) | **Convert** - Update to Avalonia |
| WPF-Specific APIs | 2,500 (17.6%) | **Convert** - Replace with Avalonia |
| Should Refactor | 2,000 (14%) | **Refactor** - Move to ViewModel |
| **Total** | **14,232** | |
| **Unchanged** | **6,300 (44%)** | |
| **Conversion Needed** | **8,000 (56%)** | |

---

## Critical Challenges

1. **DataGrid Complexity** ⚠️
   - Avalonia DataGrid has different API
   - May require custom columns
   - **Mitigation**: Prototype early

2. **ModernWpfUI Dependency** ⚠️
   - No direct Avalonia equivalent
   - Need to recreate styles
   - **Mitigation**: Create ModernStyles.axaml

3. **Drag-and-Drop Differences** ⚠️
   - Different event model
   - **Mitigation**: Rewrite using Avalonia patterns

4. **Testing Complexity** ⚠️
   - Large surface area to test
   - **Mitigation**: Test components in isolation

---

## Success Criteria

- [ ] All UI components converted to Avalonia
- [ ] Feature parity with WPF version
- [ ] All tests passing
- [ ] Performance equal or better than WPF
- [ ] Cross-platform support (Windows, Linux, macOS) - if desired
- [ ] User settings and data migration working
- [ ] No regressions in functionality
- [ ] Application ready for release

---

## Next Steps

### Immediate (This Week)

1. Create feature branch: `feature/avalonia-pre-conversion-refactoring`
2. Extract **Menu System** as first component
3. Set up prototype Avalonia project to validate approach
4. Document current theme/styling for recreation

### Short-Term (Next 2-4 Weeks)

1. Complete all component extractions in WPF
2. Validate WPF version still works after refactoring
3. Convert simple components to Avalonia (Status Bar, Overlays)
4. Identify any blockers early

### Medium-Term (Weeks 5-12)

1. Convert remaining components to Avalonia
2. Assemble MainWindow
3. Comprehensive testing
4. Performance optimization

### Long-Term (Weeks 13-20)

1. Platform-specific testing
2. User acceptance testing
3. Documentation updates
4. Release planning

---

## Additional Resources

### Avalonia Documentation
- **Official Docs**: https://docs.avaloniaui.net/
- **WPF to Avalonia Guide**: https://docs.avaloniaui.net/docs/guides/platforms/windows/wpf-to-avalonia
- **Samples**: https://github.com/AvaloniaUI/Avalonia.Samples

### Community
- **Avalonia Discord**: https://discord.gg/avaloniaui
- **GitHub Discussions**: https://github.com/AvaloniaUI/Avalonia/discussions

### Tools
- **Avalonia for Visual Studio**: Extension for Visual Studio
- **Avalonia for Rider**: Built-in support in JetBrains Rider
- **Avalonia DevTools**: Runtime debugging tool

---

## Questions?

If you have questions about the conversion process:

1. **Strategic Questions**: Refer to AVALONIA_CONVERSION_REPORT.md
2. **Technical Questions**: Refer to AVALONIA_TECHNICAL_GUIDE.md
3. **Process Questions**: Refer to AVALONIA_CONVERSION_CHECKLIST.md
4. **Community Support**: Join Avalonia Discord

---

**Last Updated**: 2026-01-01  
**Status**: Planning Phase  
**Next Milestone**: Begin Pre-Conversion Refactoring
