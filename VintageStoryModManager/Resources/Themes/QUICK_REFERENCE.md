# Menu Colors - Quick Reference Card

This is a quick reference for all menu-related color brushes. For detailed documentation, see `MENU_COLORS_GUIDE.md`.

## Location
File: `VintageStoryModManager/Resources/Themes/DarkVsTheme.xaml`  
Lines: ~175-213

---

## 📋 Complete List

### 🎨 Main Menu Bar
```xml
Brush.Menu.Background           → Palette.BaseSurface.Brighter (#FF735b43)
Brush.Menu.Border               → Palette.Bevel.Highlight (#80FFFFFF)
Brush.Menu.Text                 → Palette.Text.Primary (#FFC8BCAE)
```

### 📝 Menu Items - Backgrounds
```xml
Brush.Menu.Item.Background.Normal    → Transparent
Brush.Menu.Item.Background.Hover     → Palette.BaseSurface.Raised (#FF4D3D2D)
Brush.Menu.Item.Background.Pressed   → Palette.BaseSurface.HoverGlow (#FF5A4530)
```

### 📝 Menu Items - Borders & Text
```xml
Brush.Menu.Item.Border.Hover         → Palette.Bevel.Highlight (#80FFFFFF)
Brush.Menu.Item.Text                 → Palette.Text.Primary (#FFC8BCAE)
Brush.Menu.Item.Text.Disabled        → Palette.Text.Placeholder (#45C8BCAE)
```

### 📂 Submenus
```xml
Brush.Menu.Submenu.Background        → Palette.BaseSurface.Brighter (#FF735b43)
Brush.Menu.Submenu.Border            → Palette.Bevel.Highlight (#80FFFFFF)
```

### ✨ Accents
```xml
Brush.Menu.Glyph                     → Palette.Accent.Primary (#FF479BBE)
Brush.Menu.Separator                 → Palette.Text.Primary (#FFC8BCAE)
```

---

## 🎯 Quick Customization

### Change menu bar color
```xml
<SolidColorBrush x:Key="Brush.Menu.Background" Color="#YOUR_COLOR" />
```

### Change hover highlight
```xml
<SolidColorBrush x:Key="Brush.Menu.Item.Background.Hover" Color="#YOUR_COLOR" />
```

### Change submenu arrow color
```xml
<SolidColorBrush x:Key="Brush.Menu.Glyph" Color="#YOUR_COLOR" />
```

---

## 🎨 Color Format

Colors use the format: `#AARRGGBB`
- AA = Alpha (transparency): 00 (transparent) to FF (opaque)
- RR = Red: 00 to FF
- GG = Green: 00 to FF
- BB = Blue: 00 to FF

Examples:
- `#FFFF0000` = Solid red
- `#80FFFFFF` = 50% transparent white
- `#FF479BBE` = Solid blue (the current accent)

---

## 🔍 Visual Map

```
┌─────────────────────────────────────────────┐
│  Menu Bar (Brush.Menu.Background/Border)   │
├─────────────────────────────────────────────┤
│  ┌───────────────────────────────────────┐  │
│  │  Menu Item (Brush.Menu.Item.*)       │  │
│  │  - Normal background                  │  │
│  │  - Hover background (on mouse over)   │  │
│  │  - Pressed background (on click)      │  │
│  │  - Text color                         │  │
│  ├───────────────────────────────────────┤  │
│  │  Separator (Brush.Menu.Separator)     │  │
│  ├───────────────────────────────────────┤  │
│  │  Submenu ► (Brush.Menu.Glyph)         │  │
│  │    ┌─────────────────────────────┐    │  │
│  │    │ Submenu (Brush.Menu.Sub*)   │    │  │
│  │    │ - Background                 │    │  │
│  │    │ - Border                     │    │  │
│  │    └─────────────────────────────┘    │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

---

## 📚 See Also
- **Detailed Guide:** `MENU_COLORS_GUIDE.md`
- **Changes Summary:** `CHANGES_SUMMARY.md`
- **Theme File:** `DarkVsTheme.xaml`
