# Avalonia Technical Conversion Guide

## Companion Document to AVALONIA_CONVERSION_REPORT.md

This technical guide provides **detailed code examples** and **specific conversion patterns** for migrating the Simple VS Manager from WPF to Avalonia.

---

## Table of Contents

1. [XAML Syntax Differences](#1-xaml-syntax-differences)
2. [Namespace Mappings](#2-namespace-mappings)
3. [Control Conversions](#3-control-conversions)
4. [Styling Differences](#4-styling-differences)
5. [Data Binding](#5-data-binding)
6. [Event to Command Migration](#6-event-to-command-migration)
7. [Dependency Properties](#7-dependency-properties)
8. [Visual Tree Navigation](#8-visual-tree-navigation)
9. [File Dialogs](#9-file-dialogs)
10. [Drag and Drop](#10-drag-and-drop)
11. [Canvas and Positioning](#11-canvas-and-positioning)
12. [DataGrid Migration](#12-datagrid-migration)
13. [ModernWpfUI Alternatives](#13-modernwpfui-alternatives)

---

## 1. XAML Syntax Differences

### Window Declaration

```xml
<!-- WPF -->
<Window
    x:Class="VintageStoryModManager.Views.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.modernwpf.com/2019"
    Title="Simple VS Manager"
    Width="1558" Height="719">
</Window>

<!-- Avalonia -->
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:VintageStoryModManager.ViewModels"
    x:Class="VintageStoryModManager.Views.MainWindow"
    Title="Simple VS Manager"
    Width="1558" Height="719">
</Window>
```

### Resource Dictionaries

```xml
<!-- WPF -->
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/Resources/MainWindowStyles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>

<!-- Avalonia -->
<Window.Styles>
    <StyleInclude Source="/Resources/MainWindowStyles.axaml" />
</Window.Styles>

<!-- Or for resources: -->
<Window.Resources>
    <ResourceInclude Source="/Resources/MainWindowResources.axaml" />
</Window.Resources>
```

### Dynamic Resources

```xml
<!-- WPF -->
Background="{DynamicResource Brush.Panel.Primary.Background.Solid}"

<!-- Avalonia (identical) -->
Background="{DynamicResource Brush.Panel.Primary.Background.Solid}"
```

---

## 2. Namespace Mappings

### C# Namespaces

| WPF Namespace | Avalonia Namespace | Notes |
|---------------|-------------------|-------|
| `System.Windows` | `Avalonia` | Core types |
| `System.Windows.Controls` | `Avalonia.Controls` | Controls |
| `System.Windows.Data` | `Avalonia.Data` | Data binding |
| `System.Windows.Input` | `Avalonia.Input` | Input handling |
| `System.Windows.Media` | `Avalonia.Media` | Graphics/brushes |
| `System.Windows.Markup` | `Avalonia.Markup.Xaml` | XAML loading |
| `System.ComponentModel` | `System.ComponentModel` | Unchanged |
| `Microsoft.Win32` (dialogs) | `Avalonia.Controls` | File dialogs |

### XAML Namespace Declarations

```xml
<!-- WPF -->
xmlns:local="clr-namespace:VintageStoryModManager.Views"
xmlns:viewModels="clr-namespace:VintageStoryModManager.ViewModels"

<!-- Avalonia -->
xmlns:local="using:VintageStoryModManager.Views"
xmlns:vm="using:VintageStoryModManager.ViewModels"
```

---

## 3. Control Conversions

### Common Controls

Most controls have direct equivalents:

| WPF Control | Avalonia Control | Compatibility |
|-------------|-----------------|---------------|
| `Button` | `Button` | ✅ Identical |
| `TextBlock` | `TextBlock` | ✅ Identical |
| `TextBox` | `TextBox` | ✅ Identical |
| `CheckBox` | `CheckBox` | ✅ Identical |
| `ComboBox` | `ComboBox` | ✅ Identical |
| `ListBox` | `ListBox` | ✅ Identical |
| `ListView` | `ListBox` (different) | ⚠️ Use `ListBox` with templates |
| `DataGrid` | `DataGrid` | ⚠️ Different API |
| `Menu` | `Menu` | ✅ Similar |
| `MenuItem` | `MenuItem` | ✅ Similar |
| `TabControl` | `TabControl` | ✅ Identical |
| `TabItem` | `TabItem` | ✅ Identical |
| `Border` | `Border` | ✅ Identical |
| `Grid` | `Grid` | ✅ Identical |
| `StackPanel` | `StackPanel` | ✅ Identical |
| `Canvas` | `Canvas` | ✅ Identical |
| `ScrollViewer` | `ScrollViewer` | ✅ Similar |
| `ProgressBar` | `ProgressBar` | ✅ Identical |
| `ToolTip` | `ToolTip` | ✅ Similar |

### ModernWpfUI Controls

| ModernWpfUI Control | Avalonia Equivalent | Notes |
|---------------------|-------------------|-------|
| `ToggleSwitch` | Custom control needed | Create user control |
| `NumberBox` | `NumericUpDown` | Built-in |
| `ContentDialog` | `Window` (modal) | Use `ShowDialog()` |
| `NavigationView` | `SplitView` + custom | Recreate pattern |

### ToggleSwitch Example

The MainWindow uses `ToggleSwitch` extensively for the "Active" column. This needs a custom implementation:

```xml
<!-- WPF (ModernWpfUI) -->
<ui:ToggleSwitch
    IsOn="{Binding IsActive, Mode=TwoWay}"
    Toggled="ActiveToggle_OnToggled" />

<!-- Avalonia - Create custom control -->
<!-- First, create Views/Controls/ToggleSwitch.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="VintageStoryModManager.Views.Controls.ToggleSwitch">
    <Border Background="{DynamicResource ToggleSwitchBackground}"
            CornerRadius="10"
            Height="20" Width="40"
            Cursor="Hand"
            PointerPressed="OnPointerPressed">
        <Border x:Name="Thumb"
                Background="{DynamicResource ToggleSwitchThumb}"
                CornerRadius="8"
                Width="16" Height="16"
                HorizontalAlignment="Left"
                Margin="2,2,2,2">
            <Border.Transitions>
                <Transitions>
                    <DoubleTransition Property="Margin.Left" Duration="0:0:0.2" />
                </Transitions>
            </Border.Transitions>
        </Border>
    </Border>
</UserControl>
```

```csharp
// ToggleSwitch.axaml.cs
public partial class ToggleSwitch : UserControl
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<ToggleSwitch, bool>(nameof(IsOn));

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Toggled;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        IsOn = !IsOn;
        Toggled?.Invoke(this, new RoutedEventArgs());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty && Thumb != null)
        {
            Thumb.Margin = IsOn ? new Thickness(22, 2, 2, 2) : new Thickness(2, 2, 2, 2);
        }
    }
}
```

---

## 4. Styling Differences

### Style Declaration

```xml
<!-- WPF -->
<Style x:Key="CloseButton" TargetType="{x:Type Button}">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="Red" />
        </Trigger>
    </Style.Triggers>
</Style>

<!-- Avalonia (uses pseudo-classes instead of triggers) -->
<Style Selector="Button.closeButton">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
</Style>
<Style Selector="Button.closeButton:pointerover">
    <Setter Property="Background" Value="Red" />
</Style>

<!-- Usage -->
<Button Classes="closeButton" />
```

### Pseudo-Classes

Avalonia uses CSS-like pseudo-classes:

| WPF Trigger | Avalonia Pseudo-Class |
|-------------|---------------------|
| `IsMouseOver="True"` | `:pointerover` |
| `IsPressed="True"` | `:pressed` |
| `IsEnabled="False"` | `:disabled` |
| `IsChecked="True"` | `:checked` |
| `IsFocused="True"` | `:focus` |

### Data Triggers

```xml
<!-- WPF -->
<DataTrigger Binding="{Binding IsActive}" Value="True">
    <Setter Property="Background" Value="Green" />
</DataTrigger>

<!-- Avalonia (use MultiBinding or Converter) -->
<Style Selector="Border.modItem[IsActive=True]">
    <Setter Property="Background" Value="Green" />
</Style>

<!-- Or with converter -->
<Border Background="{Binding IsActive, Converter={StaticResource BoolToBackgroundConverter}}" />
```

---

## 5. Data Binding

### Binding Modes

```xml
<!-- WPF and Avalonia - Identical syntax -->
<TextBox Text="{Binding SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

<!-- Avalonia specific - Compiled bindings (faster) -->
<TextBox Text="{CompiledBinding SearchText}" />
```

### Compiled Bindings (Recommended)

Avalonia supports compiled bindings for better performance:

```xml
<!-- Enable compiled bindings in the window -->
<Window xmlns:vm="using:VintageStoryModManager.ViewModels"
        x:DataType="vm:MainViewModel">
    
    <!-- Use CompiledBinding -->
    <TextBlock Text="{CompiledBinding StatusMessage}" />
</Window>
```

### Element Binding

```xml
<!-- WPF -->
<TextBox x:Name="SearchBox" />
<TextBlock Text="{Binding ElementName=SearchBox, Path=Text}" />

<!-- Avalonia (identical) -->
<TextBox x:Name="SearchBox" />
<TextBlock Text="{Binding #SearchBox.Text}" />
<!-- Or -->
<TextBlock Text="{Binding $parent[Window].DataContext.StatusMessage}" />
```

---

## 6. Event to Command Migration

### Menu Items

```csharp
// WPF - Event handler in code-behind
private void SavePresetMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    // Logic here
}
```

```xml
<!-- WPF XAML -->
<MenuItem Header="Save Preset" Click="SavePresetMenuItem_OnClick" />
```

**Convert to:**

```csharp
// Avalonia - Command in ViewModel
public class MainViewModel : ViewModelBase
{
    [RelayCommand]
    private async Task SavePresetAsync()
    {
        // Logic here
    }
}
```

```xml
<!-- Avalonia XAML -->
<MenuItem Header="Save Preset" Command="{Binding SavePresetCommand}" />
```

### Button Clicks

```xml
<!-- WPF -->
<Button Content="Refresh" Click="RefreshButton_OnClick" />

<!-- Avalonia -->
<Button Content="Refresh" Command="{Binding RefreshCommand}" />
```

### Complex Event Handlers

For events that need access to event args (e.g., drag/drop, key events), keep them in code-behind:

```csharp
// Avalonia - Keep in code-behind if needed
protected override void OnKeyDown(KeyEventArgs e)
{
    base.OnKeyDown(e);
    if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
    {
        SelectAllMods();
        e.Handled = true;
    }
}
```

---

## 7. Dependency Properties

### WPF Dependency Properties

```csharp
public static readonly DependencyProperty IsDataBackupInProgressProperty =
    DependencyProperty.Register(
        nameof(IsDataBackupInProgress),
        typeof(bool),
        typeof(MainWindow),
        new PropertyMetadata(false));

public bool IsDataBackupInProgress
{
    get => (bool)GetValue(IsDataBackupInProgressProperty);
    set => SetValue(IsDataBackupInProgressProperty, value);
}
```

### Avalonia StyledProperty

```csharp
public static readonly StyledProperty<bool> IsDataBackupInProgressProperty =
    AvaloniaProperty.Register<MainWindow, bool>(
        nameof(IsDataBackupInProgress),
        defaultValue: false);

public bool IsDataBackupInProgress
{
    get => GetValue(IsDataBackupInProgressProperty);
    set => SetValue(IsDataBackupInProgressProperty, value);
}
```

### Attached Properties

```csharp
// WPF
public static readonly DependencyProperty BoundModProperty =
    DependencyProperty.RegisterAttached(
        "BoundMod",
        typeof(ModListItemViewModel),
        typeof(MainWindow));

public static void SetBoundMod(DependencyObject element, ModListItemViewModel value)
    => element.SetValue(BoundModProperty, value);

public static ModListItemViewModel GetBoundMod(DependencyObject element)
    => (ModListItemViewModel)element.GetValue(BoundModProperty);

// Avalonia
public static readonly AttachedProperty<ModListItemViewModel> BoundModProperty =
    AvaloniaProperty.RegisterAttached<MainWindow, Control, ModListItemViewModel>("BoundMod");

public static void SetBoundMod(Control element, ModListItemViewModel value)
    => element.SetValue(BoundModProperty, value);

public static ModListItemViewModel GetBoundMod(Control element)
    => element.GetValue(BoundModProperty);
```

---

## 8. Visual Tree Navigation

### Finding Child Elements

```csharp
// WPF
private ScrollViewer? FindDescendantScrollViewer(DependencyObject? current)
{
    if (current is null) return null;
    if (current is ScrollViewer viewer) return viewer;

    var childCount = VisualTreeHelper.GetChildrenCount(current);
    for (var i = 0; i < childCount; i++)
    {
        var result = FindDescendantScrollViewer(VisualTreeHelper.GetChild(current, i));
        if (result != null) return result;
    }
    return null;
}

// Avalonia (using extension methods)
using Avalonia.VisualTree;

private ScrollViewer? FindDescendantScrollViewer(Visual? current)
{
    if (current is null) return null;
    if (current is ScrollViewer viewer) return viewer;

    foreach (var child in current.GetVisualChildren())
    {
        var result = FindDescendantScrollViewer(child);
        if (result != null) return result;
    }
    return null;
}

// Or use built-in method
var scrollViewer = this.FindDescendantOfType<ScrollViewer>();
```

### Finding Ancestor

```csharp
// WPF
private T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
{
    while (current != null)
    {
        if (current is T match) return match;
        current = VisualTreeHelper.GetParent(current);
    }
    return null;
}

// Avalonia
using Avalonia.VisualTree;

private T? FindAncestor<T>(Visual? current) where T : class
{
    return current?.FindAncestorOfType<T>();
}
```

---

## 9. File Dialogs

### Open File Dialog

```csharp
// WPF
using Microsoft.Win32;

var dialog = new OpenFileDialog
{
    Filter = "Mod files (*.zip;*.cs)|*.zip;*.cs|All files (*.*)|*.*",
    Title = "Select a mod file",
    Multiselect = false
};

if (dialog.ShowDialog() == true)
{
    var filePath = dialog.FileName;
    // Use filePath
}

// Avalonia
using Avalonia.Controls;
using Avalonia.Platform.Storage;

var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
{
    Title = "Select a mod file",
    AllowMultiple = false,
    FileTypeFilter = new[]
    {
        new FilePickerFileType("Mod files")
        {
            Patterns = new[] { "*.zip", "*.cs" }
        },
        FilePickerFileTypes.All
    }
});

if (files.Count > 0)
{
    var filePath = files[0].Path.LocalPath;
    // Use filePath
}
```

### Save File Dialog

```csharp
// WPF
var dialog = new SaveFileDialog
{
    Filter = "JSON files (*.json)|*.json",
    Title = "Save modlist",
    DefaultExt = ".json",
    FileName = "modlist.json"
};

if (dialog.ShowDialog() == true)
{
    var filePath = dialog.FileName;
    // Save to filePath
}

// Avalonia
var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
{
    Title = "Save modlist",
    SuggestedFileName = "modlist.json",
    DefaultExtension = "json",
    FileTypeChoices = new[]
    {
        new FilePickerFileType("JSON files")
        {
            Patterns = new[] { "*.json" }
        }
    }
});

if (file != null)
{
    var filePath = file.Path.LocalPath;
    // Save to filePath
}
```

### Folder Browser Dialog

```csharp
// WPF
using System.Windows.Forms;

var dialog = new FolderBrowserDialog
{
    Description = "Select VintagestoryData folder",
    ShowNewFolderButton = true
};

if (dialog.ShowDialog() == DialogResult.OK)
{
    var folderPath = dialog.SelectedPath;
    // Use folderPath
}

// Avalonia
var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
{
    Title = "Select VintagestoryData folder",
    AllowMultiple = false
});

if (folders.Count > 0)
{
    var folderPath = folders[0].Path.LocalPath;
    // Use folderPath
}
```

---

## 10. Drag and Drop

### Drag Over Event

```csharp
// WPF
private void MainWindow_OnPreviewDragOver(object sender, DragEventArgs e)
{
    e.Effects = DragDropEffects.None;
    
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        e.Effects = DragDropEffects.Copy;
    }
    
    e.Handled = true;
}

// Avalonia
protected override void OnDragOver(DragEventArgs e)
{
    base.OnDragOver(e);
    
    e.DragEffects = DragDropEffects.None;
    
    if (e.Data.Contains(DataFormats.FileNames))
    {
        e.DragEffects = DragDropEffects.Copy;
    }
    
    e.Handled = true;
}
```

### Drop Event

```csharp
// WPF
private void MainWindow_OnPreviewDrop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
        {
            // Handle file
        }
    }
    e.Handled = true;
}

// Avalonia
protected override void OnDrop(DragEventArgs e)
{
    base.OnDrop(e);
    
    if (e.Data.Contains(DataFormats.FileNames))
    {
        var files = e.Data.GetFileNames();
        if (files != null)
        {
            foreach (var file in files)
            {
                // Handle file
            }
        }
    }
    
    e.Handled = true;
}
```

### Enable Drag/Drop in XAML

```xml
<!-- WPF -->
<Window AllowDrop="True"
        PreviewDragOver="MainWindow_OnPreviewDragOver"
        PreviewDrop="MainWindow_OnPreviewDrop">
</Window>

<!-- Avalonia -->
<Window DragDrop.AllowDrop="True">
    <!-- Override OnDragOver and OnDrop in code-behind -->
</Window>
```

---

## 11. Canvas and Positioning

### Canvas Attached Properties

```xml
<!-- WPF and Avalonia - Identical -->
<Canvas>
    <Border Canvas.Left="100" Canvas.Top="50" Width="200" Height="100">
        <TextBlock Text="Draggable Panel" />
    </Border>
</Canvas>
```

### Setting Position in Code

```csharp
// WPF
Canvas.SetLeft(element, 100);
Canvas.SetTop(element, 50);

// Avalonia - Identical
Canvas.SetLeft(element, 100);
Canvas.SetTop(element, 50);
```

### Mouse Drag Implementation

```csharp
// WPF
private Point _dragOffset;
private bool _isDragging;

private void Element_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not Border border) return;
    _isDragging = true;
    _dragOffset = e.GetPosition(border);
    border.CaptureMouse();
    e.Handled = true;
}

private void Element_OnMouseMove(object sender, MouseEventArgs e)
{
    if (!_isDragging || sender is not Border border) return;
    
    var position = e.GetPosition(RootGrid);
    var left = position.X - _dragOffset.X;
    var top = position.Y - _dragOffset.Y;
    
    Canvas.SetLeft(border, left);
    Canvas.SetTop(border, top);
    e.Handled = true;
}

private void Element_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (!_isDragging || sender is not Border border) return;
    
    _isDragging = false;
    border.ReleaseMouseCapture();
    e.Handled = true;
}

// Avalonia
private Point _dragOffset;
private bool _isDragging;

private void Element_OnPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (sender is not Border border) return;
    _isDragging = true;
    _dragOffset = e.GetPosition(border);
    e.Pointer.Capture(border);
    e.Handled = true;
}

private void Element_OnPointerMoved(object? sender, PointerEventArgs e)
{
    if (!_isDragging || sender is not Border border) return;
    
    var position = e.GetPosition(RootGrid);
    var left = position.X - _dragOffset.X;
    var top = position.Y - _dragOffset.Y;
    
    Canvas.SetLeft(border, left);
    Canvas.SetTop(border, top);
    e.Handled = true;
}

private void Element_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
{
    if (!_isDragging || sender is not Border border) return;
    
    _isDragging = false;
    e.Pointer.Capture(null);
    e.Handled = true;
}
```

---

## 12. DataGrid Migration

### Basic DataGrid

```xml
<!-- WPF -->
<DataGrid
    ItemsSource="{Binding Mods}"
    AutoGenerateColumns="False"
    CanUserAddRows="False"
    SelectionMode="Extended">
    
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding DisplayName}" Width="*" />
        <DataGridTextColumn Header="Version" Binding="{Binding Version}" Width="100" />
    </DataGrid.Columns>
</DataGrid>

<!-- Avalonia -->
<DataGrid
    Items="{Binding Mods}"
    AutoGenerateColumns="False"
    CanUserAddRows="False"
    SelectionMode="Extended">
    
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding DisplayName}" Width="*" />
        <DataGridTextColumn Header="Version" Binding="{Binding Version}" Width="100" />
    </DataGrid.Columns>
</DataGrid>
```

**Key Differences:**
- `ItemsSource` → `Items` (different property name)
- Column templates work similarly
- Styling is different (use Avalonia styles)

### Template Column

```xml
<!-- WPF -->
<DataGridTemplateColumn Header="Active">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <ui:ToggleSwitch IsOn="{Binding IsActive, Mode=TwoWay}" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>

<!-- Avalonia -->
<DataGridTemplateColumn Header="Active">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <local:ToggleSwitch IsOn="{Binding IsActive, Mode=TwoWay}" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### Row Selection

```csharp
// WPF
private void ModsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    var selectedMods = ModsDataGrid.SelectedItems.Cast<ModListItemViewModel>().ToList();
    // Handle selection
}

// Avalonia
private void ModsDataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (ModsDataGrid.SelectedItems is IList selectedItems)
    {
        var selectedMods = selectedItems.Cast<ModListItemViewModel>().ToList();
        // Handle selection
    }
}
```

---

## 13. ModernWpfUI Alternatives

### Themes

```csharp
// WPF - ModernWpfUI
ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;

// Avalonia - Fluent Theme
// In App.axaml
<Application.Styles>
    <FluentTheme Mode="Dark" />
</Application.Styles>

// Or programmatically
App.Current.RequestedThemeVariant = ThemeVariant.Dark;
```

### Accent Colors

```csharp
// WPF - ModernWpfUI
ModernWpf.ThemeManager.Current.AccentColor = Colors.Blue;

// Avalonia - Custom resources
// Define in App.axaml resources:
<SolidColorBrush x:Key="SystemAccentColor">#FF0078D4</SolidColorBrush>
```

### Recreating ModernWpfUI Styles

ModernWpfUI provides pre-styled controls. In Avalonia, you'll need to recreate these styles:

```xml
<!-- Example: Modern Button Style -->
<Style Selector="Button.modern">
    <Setter Property="Background" Value="{DynamicResource ButtonBackground}" />
    <Setter Property="Foreground" Value="{DynamicResource ButtonForeground}" />
    <Setter Property="BorderBrush" Value="{DynamicResource ButtonBorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="CornerRadius" Value="4" />
</Style>

<Style Selector="Button.modern:pointerover">
    <Setter Property="Background" Value="{DynamicResource ButtonBackgroundPointerOver}" />
</Style>

<Style Selector="Button.modern:pressed">
    <Setter Property="Background" Value="{DynamicResource ButtonBackgroundPressed}" />
</Style>
```

**Recommendation**: Create a `ModernStyles.axaml` resource dictionary with all custom styles that replicate ModernWpfUI appearance.

---

## Additional Resources

- **Avalonia Documentation**: https://docs.avaloniaui.net/
- **Avalonia Samples**: https://github.com/AvaloniaUI/Avalonia.Samples
- **WPF to Avalonia Guide**: https://docs.avaloniaui.net/docs/guides/platforms/windows/wpf-to-avalonia
- **Avalonia Discord**: https://discord.gg/avaloniaui

---

## Summary Checklist

When converting a component from WPF to Avalonia:

- [ ] Update window/control declaration (xmlns)
- [ ] Convert namespace imports (using statements)
- [ ] Replace `ItemsSource` with `Items` in DataGrid
- [ ] Convert triggers to pseudo-classes in styles
- [ ] Update file dialog code to use `StorageProvider`
- [ ] Replace `MouseEventArgs` with `PointerEventArgs`
- [ ] Convert dependency properties to styled properties
- [ ] Migrate visual tree navigation to use Avalonia methods
- [ ] Replace ModernWpfUI controls with Avalonia equivalents
- [ ] Convert event handlers to commands where appropriate
- [ ] Test on target platforms (Windows, Linux, macOS)

---

**Document Version**: 1.0  
**Last Updated**: 2026-01-01  
**Author**: GitHub Copilot Analysis  
**Project**: Simple VS Manager (SVSM)
