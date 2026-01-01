# WPF to Avalonia Conversion Checklist

## Quick Reference Guide for Component Conversion

This checklist provides a step-by-step workflow for converting each component from WPF to Avalonia.

---

## Pre-Conversion Checklist (WPF Project)

Before converting to Avalonia, prepare the WPF codebase:

### Component Extraction

- [ ] **Extract Menu System**
  - [ ] Create `Views/Components/MainMenu.xaml`
  - [ ] Move menu XAML from MainWindow
  - [ ] Update MainWindow to reference component
  - [ ] Test menu functionality
  - [ ] Commit changes

- [ ] **Extract Mod Info Panel**
  - [ ] Create `Views/Components/ModInfoPanel.xaml`
  - [ ] Move panel XAML and drag logic
  - [ ] Update MainWindow to reference component
  - [ ] Test drag functionality
  - [ ] Commit changes

- [ ] **Extract Progress Overlays**
  - [ ] Create `Views/Components/ProgressOverlay.xaml`
  - [ ] Make reusable overlay component
  - [ ] Update MainWindow to use component
  - [ ] Test overlay visibility
  - [ ] Commit changes

- [ ] **Extract Filter Bar**
  - [ ] Create `Views/Components/FilterBar.xaml`
  - [ ] Move filter controls
  - [ ] Update MainWindow to reference component
  - [ ] Test filter functionality
  - [ ] Commit changes

- [ ] **Extract Tab Content**
  - [ ] Create `Views/InstalledModsTab.xaml`
  - [ ] Create `Views/ModlistsTab.xaml`
  - [ ] Move tab XAML
  - [ ] Update MainWindow to reference components
  - [ ] Test tab switching and functionality
  - [ ] Commit changes

- [ ] **Extract Status Bar**
  - [ ] Create `Views/Components/StatusBar.xaml`
  - [ ] Move status bar XAML
  - [ ] Update MainWindow to reference component
  - [ ] Test status updates
  - [ ] Commit changes

### Verification

- [ ] WPF application still runs correctly
- [ ] All features work as before
- [ ] No regressions introduced
- [ ] MainWindow.xaml reduced to ~525 lines
- [ ] Code is committed and tested

---

## Avalonia Setup Checklist

### Project Creation

- [ ] Create new Avalonia MVVM project
- [ ] Configure solution structure
- [ ] Add Avalonia.Themes.Fluent package
- [ ] Add CommunityToolkit.Mvvm package (if not using ReactiveUI)
- [ ] Set up dependency injection (optional)
- [ ] Configure build targets (Windows, Linux, macOS)

### Theme Setup

- [ ] Create `Resources/Themes/` directory
- [ ] Port WPF color resources to Avalonia
- [ ] Create `ModernStyles.axaml` for custom styles
- [ ] Implement theme switching logic
- [ ] Test dark/light theme switching

### Shared Code

- [ ] Copy ViewModels to Avalonia project
- [ ] Copy Services to Avalonia project
- [ ] Copy Models to Avalonia project
- [ ] Copy Helpers/Utilities to Avalonia project
- [ ] Update namespace references if needed
- [ ] Verify ViewModels work with Avalonia

---

## Component Conversion Checklist

Use this checklist for **each component** you convert:

### 1. Preparation

- [ ] Identify component to convert
- [ ] Review WPF XAML structure
- [ ] Review WPF code-behind
- [ ] Identify WPF-specific features (triggers, styles, etc.)
- [ ] Plan conversion approach

### 2. XAML Conversion

- [ ] Create `.axaml` file in Avalonia project
- [ ] Update XML namespace declarations
  ```xml
  xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  ```
- [ ] Copy XAML structure from WPF
- [ ] Update control names if needed (e.g., `ItemsSource` → `Items`)
- [ ] Convert resource references
  ```xml
  <!-- WPF -->
  <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="..." />
  </ResourceDictionary.MergedDictionaries>
  
  <!-- Avalonia -->
  <UserControl.Styles>
      <StyleInclude Source="..." />
  </UserControl.Styles>
  ```
- [ ] Convert style triggers to pseudo-classes
  ```xml
  <!-- WPF -->
  <Style.Triggers>
      <Trigger Property="IsMouseOver" Value="True">
  
  <!-- Avalonia -->
  <Style Selector="Button:pointerover">
  ```
- [ ] Update namespace references
  ```xml
  <!-- WPF -->
  xmlns:local="clr-namespace:..."
  
  <!-- Avalonia -->
  xmlns:local="using:..."
  ```
- [ ] Test XAML compiles

### 3. Code-Behind Conversion

- [ ] Create `.axaml.cs` file
- [ ] Update using statements
  ```csharp
  // WPF
  using System.Windows;
  using System.Windows.Controls;
  
  // Avalonia
  using Avalonia;
  using Avalonia.Controls;
  ```
- [ ] Update class declaration
  ```csharp
  // WPF
  public partial class MyControl : UserControl
  
  // Avalonia (same)
  public partial class MyControl : UserControl
  ```
- [ ] Convert dependency properties to styled properties (if needed)
  ```csharp
  // WPF
  public static readonly DependencyProperty MyProperty =
      DependencyProperty.Register(...);
  
  // Avalonia
  public static readonly StyledProperty<T> MyProperty =
      AvaloniaProperty.Register<MyControl, T>(...);
  ```
- [ ] Update event signatures
  ```csharp
  // WPF
  private void OnClick(object sender, RoutedEventArgs e)
  
  // Avalonia (same, but nullable)
  private void OnClick(object? sender, RoutedEventArgs e)
  ```
- [ ] Convert mouse events to pointer events
  ```csharp
  // WPF
  MouseLeftButtonDown, MouseMove, MouseLeftButtonUp
  
  // Avalonia
  PointerPressed, PointerMoved, PointerReleased
  ```
- [ ] Update visual tree navigation
  ```csharp
  // WPF
  VisualTreeHelper.GetChild(element, index)
  
  // Avalonia
  element.GetVisualChildren()
  ```
- [ ] Test code compiles

### 4. ViewModel Binding

- [ ] Set DataContext if needed
- [ ] Update compiled bindings (recommended)
  ```xml
  <UserControl xmlns:vm="using:..."
               x:DataType="vm:MyViewModel">
      <TextBlock Text="{CompiledBinding MyProperty}" />
  </UserControl>
  ```
- [ ] Test data binding works
- [ ] Verify property change notifications work

### 5. Styling

- [ ] Apply Fluent theme styles
- [ ] Add custom styles if needed
- [ ] Test hover states (`:pointerover`)
- [ ] Test pressed states (`:pressed`)
- [ ] Test disabled states (`:disabled`)
- [ ] Verify colors match design

### 6. Event Handling

- [ ] Convert event handlers to commands (preferred)
  ```csharp
  // WPF - Event handler
  Click="OnClick"
  
  // Avalonia - Command binding
  Command="{Binding ClickCommand}"
  ```
- [ ] For unavoidable events, update signatures
- [ ] Test all user interactions work

### 7. Testing

- [ ] Component renders correctly
- [ ] All controls are interactive
- [ ] Data binding updates UI
- [ ] Commands execute properly
- [ ] Styles apply correctly
- [ ] No visual glitches
- [ ] No runtime errors

### 8. Integration

- [ ] Reference component in parent view
- [ ] Test component in context
- [ ] Verify interactions with other components
- [ ] Test on target platforms (Windows, Linux, macOS)

### 9. Documentation

- [ ] Document any conversion challenges
- [ ] Note any workarounds implemented
- [ ] Update component documentation if needed

### 10. Commit

- [ ] Commit converted component
- [ ] Write descriptive commit message
- [ ] Link to conversion task/issue

---

## Specific Component Checklists

### Menu System Conversion

- [ ] Convert Menu and MenuItem controls (similar in Avalonia)
- [ ] Update menu click handlers to commands
  ```csharp
  // Create commands in ViewModel
  [RelayCommand]
  private async Task SavePresetAsync() { ... }
  ```
- [ ] Update menu item bindings
  ```xml
  <MenuItem Header="Save Preset" Command="{Binding SavePresetCommand}" />
  ```
- [ ] Test menu hierarchy
- [ ] Test keyboard shortcuts
- [ ] Test submenu opening

### DataGrid Conversion

- [ ] Create Avalonia DataGrid
- [ ] Change `ItemsSource` to `Items`
  ```xml
  <!-- WPF -->
  <DataGrid ItemsSource="{Binding Mods}">
  
  <!-- Avalonia -->
  <DataGrid Items="{Binding Mods}">
  ```
- [ ] Convert column definitions
- [ ] Update template columns
- [ ] Test sorting
- [ ] Test filtering (if applicable)
- [ ] Test row selection
- [ ] Test keyboard navigation

### File Dialog Conversion

- [ ] Replace OpenFileDialog
  ```csharp
  var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { ... });
  ```
- [ ] Replace SaveFileDialog
  ```csharp
  var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { ... });
  ```
- [ ] Replace FolderBrowserDialog
  ```csharp
  var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { ... });
  ```
- [ ] Test file selection
- [ ] Test file filtering
- [ ] Test cancellation

### Drag and Drop Conversion

- [ ] Set `DragDrop.AllowDrop="True"`
- [ ] Override `OnDragOver`
  ```csharp
  protected override void OnDragOver(DragEventArgs e)
  {
      e.DragEffects = e.Data.Contains(DataFormats.FileNames) 
          ? DragDropEffects.Copy 
          : DragDropEffects.None;
  }
  ```
- [ ] Override `OnDrop`
  ```csharp
  protected override void OnDrop(DragEventArgs e)
  {
      var files = e.Data.GetFileNames();
      // Handle files
  }
  ```
- [ ] Test drag over visual feedback
- [ ] Test drop handling
- [ ] Test with different data formats

### Custom Control Conversion (e.g., ToggleSwitch)

- [ ] Create UserControl in Avalonia
- [ ] Implement visual structure
- [ ] Add styled properties
  ```csharp
  public static readonly StyledProperty<bool> IsOnProperty =
      AvaloniaProperty.Register<ToggleSwitch, bool>(nameof(IsOn));
  ```
- [ ] Implement interaction logic
- [ ] Add animations/transitions
- [ ] Test state changes
- [ ] Test binding

---

## Platform-Specific Testing

### Windows

- [ ] Application launches
- [ ] All features work
- [ ] File dialogs work
- [ ] Drag and drop works
- [ ] Theme switching works
- [ ] Performance is acceptable

### Linux

- [ ] Application launches
- [ ] All features work
- [ ] File dialogs work (native or GTK)
- [ ] Drag and drop works
- [ ] Theme switching works
- [ ] Fonts render correctly
- [ ] No platform-specific crashes

### macOS

- [ ] Application launches
- [ ] All features work
- [ ] File dialogs work (native)
- [ ] Drag and drop works
- [ ] Theme switching works
- [ ] Fonts render correctly
- [ ] Menu bar integration works (if applicable)

---

## Common Pitfalls

### Avoid These Mistakes

- [ ] ❌ Don't use `ItemsSource` in DataGrid (use `Items`)
- [ ] ❌ Don't use WPF-style triggers (use pseudo-classes)
- [ ] ❌ Don't use `MouseEventArgs` (use `PointerEventArgs`)
- [ ] ❌ Don't forget to make event parameters nullable (`object?`)
- [ ] ❌ Don't assume WPF control properties exist in Avalonia
- [ ] ❌ Don't forget to test on all target platforms
- [ ] ❌ Don't mix WPF and Avalonia namespaces
- [ ] ❌ Don't forget to update xmlns in XAML

### Best Practices

- [ ] ✅ Use compiled bindings for better performance
- [ ] ✅ Prefer commands over event handlers
- [ ] ✅ Use Avalonia DevTools for debugging
- [ ] ✅ Test each component in isolation first
- [ ] ✅ Follow MVVM pattern strictly
- [ ] ✅ Use ReactiveUI for complex scenarios (optional)
- [ ] ✅ Leverage Avalonia's styling system
- [ ] ✅ Write unit tests for ViewModels

---

## Progress Tracking Template

Copy this template to track your conversion progress:

```markdown
## Conversion Progress

### Pre-Conversion (WPF)
- [ ] Menu System extracted
- [ ] Mod Info Panel extracted
- [ ] Progress Overlays extracted
- [ ] Filter Bar extracted
- [ ] Tab Content extracted
- [ ] Status Bar extracted
- [ ] All tests passing

### Avalonia Setup
- [ ] Project created
- [ ] Themes configured
- [ ] Shared code copied
- [ ] Build succeeds

### Components Converted
- [ ] Status Bar (Week 3)
- [ ] Progress Overlays (Week 3)
- [ ] Filter Bar (Week 4)
- [ ] Menu System (Week 5-6)
- [ ] Mod Info Panel (Week 6-7)
- [ ] Installed Mods Tab (Week 8-9)
- [ ] Modlists Tab (Week 9-10)
- [ ] Mod Browser Tab (Week 10)
- [ ] MainWindow Assembly (Week 11-12)

### Platform Testing
- [ ] Windows tested
- [ ] Linux tested (optional)
- [ ] macOS tested (optional)

### Final Steps
- [ ] All features working
- [ ] Performance optimized
- [ ] Documentation updated
- [ ] Release ready
```

---

## Quick Reference: Common Conversions

| Task | WPF Code | Avalonia Code |
|------|----------|---------------|
| Namespace | `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` | `xmlns="https://github.com/avaloniaui"` |
| Using | `using System.Windows;` | `using Avalonia;` |
| DataGrid Items | `ItemsSource="{Binding...}"` | `Items="{Binding...}"` |
| Mouse Event | `MouseLeftButtonDown` | `PointerPressed` |
| Style Trigger | `<Trigger Property="IsMouseOver"...>` | `<Style Selector="Button:pointerover">` |
| File Dialog | `new OpenFileDialog()` | `StorageProvider.OpenFilePickerAsync()` |
| Visual Tree | `VisualTreeHelper.GetChild()` | `element.GetVisualChildren()` |
| Dependency Property | `DependencyProperty.Register()` | `AvaloniaProperty.Register()` |

---

**Document Version**: 1.0  
**Last Updated**: 2026-01-01  
**Author**: GitHub Copilot Analysis  
**Project**: Simple VS Manager (SVSM)
