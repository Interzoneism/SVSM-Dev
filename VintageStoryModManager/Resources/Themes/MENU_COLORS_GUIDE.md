# Menu Styling Guide

This guide explains all the menu colors defined in `DarkVsTheme.xaml` and how to customize them.

## Menu Colors Overview

All menu-related colors are grouped together in the `DarkVsTheme.xaml` file under the section "Menu, context menu, and tool tip UI" (around line 175-210).

## Menu Structure

The application menu has three main components:
1. **Main Menu Bar** - The top-level menu bar containing items like "File", "Edit", etc.
2. **Menu Items** - Individual items within dropdowns
3. **Submenus** - Nested dropdown menus

---

## Main Menu Bar Colors

These control the appearance of the top-level menu bar:

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Background` | Background color of the main menu bar | `Palette.BaseSurface.Brighter` (#FF735b43) |
| `Brush.Menu.Border` | Border color around the menu bar | `Palette.Bevel.Highlight` (#80FFFFFF) |
| `Brush.Menu.Text` | Text color for menu bar items | `Palette.Text.Primary` (#FFC8BCAE) |

**Example:** To make the menu bar darker, change the `Palette.BaseSurface.Brighter` color.

---

## Menu Item Colors

These control the appearance of items within dropdown menus:

### Background Colors

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Item.Background.Normal` | Default background for menu items | `Transparent` |
| `Brush.Menu.Item.Background.Hover` | Background when hovering over a menu item | `Palette.BaseSurface.Raised` (#FF4D3D2D) |
| `Brush.Menu.Item.Background.Pressed` | Background when clicking a menu item | `Palette.BaseSurface.HoverGlow` (#FF5A4530) |

### Border Colors

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Item.Border.Hover` | Border color when hovering over menu items | `Palette.Bevel.Highlight` (#80FFFFFF) |

### Text Colors

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Item.Text` | Default text color for menu items | `Palette.Text.Primary` (#FFC8BCAE) |
| `Brush.Menu.Item.Text.Disabled` | Text color for disabled menu items | `Palette.Text.Placeholder` (#45C8BCAE) |

**Example:** To make menu items more prominent on hover, increase the brightness of `Palette.BaseSurface.Raised`.

---

## Submenu Dropdown Colors

These control the appearance of submenu popups:

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Submenu.Background` | Background of submenu dropdowns | `Palette.BaseSurface.Brighter` (#FF735b43) |
| `Brush.Menu.Submenu.Border` | Border around submenu dropdowns | `Palette.Bevel.Highlight` (#80FFFFFF) |

**Example:** To make submenus stand out from the main menu, use a different background color.

---

## Menu Accent Colors

These control small details and accents:

| Brush Key | Purpose | Default Value |
|-----------|---------|---------------|
| `Brush.Menu.Glyph` | Color for arrow glyphs (►) in submenus | `Palette.Accent.Primary` (#FF479BBE) |
| `Brush.Menu.Separator` | Color for separator lines between menu items | `Palette.Text.Primary` (#FFC8BCAE) |

**Example:** To make submenu arrows blue, change `Palette.Accent.Primary`.

---

## How to Customize Menu Colors

### Method 1: Change Palette Colors (Affects Multiple Elements)

Edit the palette colors at the top of `DarkVsTheme.xaml` (lines 9-24):

```xml
<Color x:Key="Palette.BaseSurface.Brighter">#FF735b43</Color>
<Color x:Key="Palette.BaseSurface.Raised">#FF4D3D2D</Color>
<Color x:Key="Palette.BaseSurface.HoverGlow">#FF5A4530</Color>
<!-- etc. -->
```

**Pros:** Changes apply consistently across the entire theme  
**Cons:** May affect other UI elements that use the same palette colors

### Method 2: Override Individual Brush Colors (Menu-Specific)

Override the specific brush you want to change:

```xml
<!-- Original definition -->
<SolidColorBrush x:Key="Brush.Menu.Background" Color="{DynamicResource Palette.BaseSurface.Brighter}" />

<!-- Override by changing the color reference -->
<SolidColorBrush x:Key="Brush.Menu.Background" Color="#FF8A6B50" />
```

**Pros:** Only affects menu elements  
**Cons:** Less consistent with the overall theme

---

## Common Customization Examples

### Example 1: Make menus lighter

```xml
<!-- Change these palette colors to lighter values -->
<Color x:Key="Palette.BaseSurface.Brighter">#FF9A7B63</Color>
<Color x:Key="Palette.BaseSurface.Raised">#FF6D5D4D</Color>
```

### Example 2: Add a colored highlight on hover

```xml
<!-- Change hover background to a blue tint -->
<SolidColorBrush x:Key="Brush.Menu.Item.Background.Hover" Color="#FF4A5B6E" />
```

### Example 3: Make disabled items more obvious

```xml
<!-- Make disabled text more transparent -->
<SolidColorBrush 
    x:Key="Brush.Menu.Item.Text.Disabled" 
    Opacity="0.3"
    Color="{DynamicResource Palette.Text.Primary}" />
```

### Example 4: Custom accent color for glyphs

```xml
<!-- Change arrow glyphs to orange -->
<SolidColorBrush x:Key="Brush.Menu.Glyph" Color="#FFFF8C42" />
```

---

## Color Palette Reference

Here are the default palette colors used by menu elements:

| Palette Key | Hex Value | RGB | Description |
|-------------|-----------|-----|-------------|
| `Palette.BaseSurface.Brighter` | #FF735b43 | (115, 91, 67) | Lighter brown surface |
| `Palette.BaseSurface.Shadowed` | #FF403529 | (64, 53, 41) | Dark brown shadow |
| `Palette.BaseSurface.Raised` | #FF4D3D2D | (77, 61, 45) | Medium brown surface |
| `Palette.BaseSurface.HoverGlow` | #FF5A4530 | (90, 69, 48) | Warm brown glow |
| `Palette.Accent.Primary` | #FF479BBE | (71, 155, 190) | Blue accent |
| `Palette.Text.Primary` | #FFC8BCAE | (200, 188, 174) | Light tan text |
| `Palette.Bevel.Highlight` | #80FFFFFF | (255, 255, 255, 50%) | Semi-transparent white |
| `Palette.Bevel.Shadow` | #40000000 | (0, 0, 0, 25%) | Semi-transparent black |

---

## Testing Your Changes

After making changes:

1. Save the `DarkVsTheme.xaml` file
2. Build the application: `dotnet build`
3. Run the application: The menu should reflect your changes
4. Test these scenarios:
   - Hover over menu bar items (File, Edit, etc.)
   - Click to open dropdown menus
   - Hover over menu items in dropdowns
   - Test disabled menu items
   - Test submenus (items with arrows)

---

## Troubleshooting

### Problem: Changes don't appear
**Solution:** Ensure you're editing `VintageStoryModManager/Resources/Themes/DarkVsTheme.xaml` and rebuild the project.

### Problem: Colors look wrong
**Solution:** Check that you're using the correct format: `#AARRGGBB` where AA is alpha (opacity), RR is red, GG is green, BB is blue.

### Problem: Application crashes after changes
**Solution:** Check for XML syntax errors. Each `<SolidColorBrush>` must be properly closed with `/>` or `</SolidColorBrush>`.

---

## Additional Resources

- WPF Color Reference: [Microsoft Docs - Colors](https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.colors)
- Color Picker Tools: 
  - [HTML Color Codes](https://htmlcolorcodes.com/)
  - [ColorHexa](https://www.colorhexa.com/)
  - Windows built-in color picker (Win+Shift+C on Windows 11)

---

## File Location

This guide corresponds to:
- **Theme File:** `VintageStoryModManager/Resources/Themes/DarkVsTheme.xaml`
- **Menu Section:** Lines 175-210 (approximately)
- **Style Templates:** Lines 468-695 (approximately)
