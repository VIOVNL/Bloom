# Bloom — Collected App Information

## Overview
Bloom is a radial petal launcher for Windows. It sits as a floating button on your desktop. Click it, and your apps, folders, commands, system actions, and keyboard shortcuts fan out in a circular flower pattern around it.

## Version
- Current: 1.0.5
- .NET 10, Avalonia UI 11.3.12, CommunityToolkit.MVVM 8.2.1
- Auto-updates via Velopack

## 5 Item Types

### Software
Launch any .exe or application. Supports optional command-line arguments. Icons auto-extracted from executables via Win32 API.

### Folder
Open any directory instantly in Explorer. Quick access to your most-used folders.

### Command
Execute any terminal/command-line command in the background. No need to open a command prompt first.

### System Action (28 built-in)
Trigger Windows system actions with one click:
- **System**: Screenshot (Win+Shift+S), Lock, Sleep, Hibernate, Shutdown, Restart, Sign Out
- **Utilities**: Task Manager, Settings, Explorer, Calculator, Control Panel, Notepad, Magnifier
- **Quick Access**: Clipboard (Win+V), Show Desktop (Win+D), Run Dialog (Win+R), Emoji Picker (Win+.), Minimize All (Win+M), Empty Recycle Bin
- **Virtual Desktops**: Task View (Win+Tab), New Desktop (Ctrl+Win+D), Close Desktop (Ctrl+Win+F4), Previous/Next Desktop
- **Window Management**: Snap Left (Win+Left), Snap Right (Win+Right), Maximize (Win+Up)

### Keyboard Shortcut
Record any key combination and replay it with one click. Supports all modifier keys (Ctrl, Shift, Alt, Win), function keys (F1-F12), numpad, and 40+ special keys.

## Icon System
- 1000+ Lucide icons (stroke-based, not filled)
- Auto-extract icons from .exe files
- Custom icon files (PNG)
- Text fallback (first letter of label)

## Color Palette
52 predefined colors in 4 rows:
- Row 1: Light pastels (14 colors)
- Row 2: Medium tones (14 colors)
- Row 3: Vivid colors (14 colors)
- Row 4: Dark colors (10 colors)
Plus any custom hex color.

## Label Modes
- Below: Labels displayed below each petal
- Tooltip: Labels shown on hover (default)
- Overlay: Labels overlaid on petal icons
- Hidden: No labels shown

## Animation System
- Staggered bloom open: 20ms between each petal, petals scale from 0 to 1 with CubicEaseOut
- Staggered close: 30ms reverse order, scale to 0.15 with CubicEaseIn
- Mouse repel: Petals push away from cursor within 60px radius, max 6px displacement
- Hover: 1.1x scale on hovered petal

## Layout Engine
- Smart multi-layer layout:
  - 1-5 items: single ring
  - 6-10: two rings (60/40 split)
  - 11-24: two rings (35/65 split)
  - 25+: three rings (15/35/50 split)
- Edge-aware: detects screen boundaries, adjusts spread angle and rotation
- Minimum 90-degree spread maintained

## Settings
- Dark/Light theme
- Start with Windows (registry-based)
- Auto-update checking
- Close on focus loss (UnBloom)
- Window position persistence (debounced 300ms)

## Configuration
- Stored as JSON (Bloom.json)
- Location: app directory if writable, else %LOCALAPPDATA%\Bloom\
- 8 default items on first launch: Chrome, Notepad, Calculator, Terminal, Documents folder, System Info, Screenshot action, Task Manager shortcut

## Default Items
| Label | Type | Path | Icon | Color |
|-------|------|------|------|-------|
| Chrome | Software | chrome | globe | #4285F4 |
| Notepad | Software | notepad | file | #FFC107 |
| Calculator | Software | calc | calculator | #FF5722 |
| Terminal | Software | cmd /k echo Welcome | terminal | #4CAF50 |
| Documents | Folder | MyDocuments | folder | #FF9800 |
| System Info | Command | msinfo32 | monitor | #9C27B0 |
| Screenshot | Action | @@screenshot | camera | #FFD43B |
| Task Manager | Shortcut | Ctrl+Shift+Esc | keyboard | #339AF0 |

## Technical Details
- Borderless transparent window with glass aesthetic
- Petal size: 42px, Button size: 52px
- Repel radius: 60px, Repel strength: 6px
- Draggable bloom button with position persistence
- Right-click to edit petals
- Cross-VM messaging via WeakReferenceMessenger
- Win32 interop: LockWorkStation, SHEmptyRecycleBin, keybd_event, ExtractIcon
- Update URL: https://bloom.viov.nl/updates/
- Lightweight and fast — launches instantly

## Changelog

### v1.0.5 (March 2026) — Current
- Radial petal launcher with smooth staggered animations
- 5 item types: Software, Folders, Commands, System Actions, Keyboard Shortcuts
- 28 built-in system actions
- 1000+ Lucide icons
- 52 customizable petal colors
- Mouse-following petal repel effect
- Draggable bloom button with position persistence
- Edge-aware multi-layer layout engine
- Always-on-top floating bloom button
- Dark and Light themes
- 4 label display modes
- Auto-update via Velopack
- Start with Windows option
- Auto-close on focus loss
- Window position persistence
- Icon extraction from executables
- Custom icon file support
- Tab-based multi-type item editor
- Right-click to edit petals
- JSON configuration with fallback paths
- Windows 10 and 11 support
- Under 1 MB footprint
