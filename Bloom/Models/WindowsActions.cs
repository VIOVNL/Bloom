namespace Bloom.Models;

public record ActionDefinition(
    string Key,
    string Label,
    string Description,
    LucideIcon Icon,
    string DefaultColor
);

public static class WindowsActions
{
    public static readonly ActionDefinition[] All =
    {
        new("@@screenshot",   "Screenshot",       "Launch Snipping Tool",     LucideIcon.Camera,     "#FFD43B"),
        new("@@lock",         "Lock Screen",      "Lock workstation",         LucideIcon.LockIcon,   "#339AF0"),
        new("@@sleep",        "Sleep",            "Put PC to sleep",          LucideIcon.Moon,       "#5C7CFA"),
        new("@@hibernate",    "Hibernate",        "Hibernate PC",             LucideIcon.Moon,       "#3F51B5"),
        new("@@shutdown",     "Shut Down",        "Shut down PC",             LucideIcon.Power,      "#FF6B6B"),
        new("@@restart",      "Restart",          "Restart PC",               LucideIcon.RotateCw,   "#FFA94D"),
        new("@@taskmgr",      "Task Manager",     "Open Task Manager",        LucideIcon.Terminal,   "#4CAF50"),
        new("@@settings",     "Settings",         "Open Windows Settings",    LucideIcon.Settings,   "#9C27B0"),
        new("@@explorer",     "File Explorer",    "Open File Explorer",       LucideIcon.Folder,     "#FF9800"),
        new("@@calc",         "Calculator",       "Open Calculator",          LucideIcon.Calculator, "#FF5722"),
        new("@@controlpanel", "Control Panel",    "Open Control Panel",       LucideIcon.Settings,   "#795548"),
        new("@@emptybin",     "Empty Recycle Bin", "Empty the Recycle Bin",   LucideIcon.Trash2,     "#607D8B"),
        new("@@notepad",      "Notepad",           "Open Notepad",            LucideIcon.Pencil,     "#8BC34A"),
        new("@@clipboard",    "Clipboard",         "Open Clipboard History",  LucideIcon.File,       "#00BCD4"),
        new("@@showdesktop",  "Show Desktop",      "Toggle Show Desktop",     LucideIcon.House,      "#339AF0"),
        new("@@run",          "Run",               "Open Run Dialog",         LucideIcon.Terminal,   "#FF9800"),
        new("@@emoji",        "Emoji Picker",      "Open Emoji Picker",       LucideIcon.Star,       "#FFD43B"),
        new("@@minimizeall",  "Minimize All",      "Minimize All Windows",    LucideIcon.Minus,      "#607D8B"),
        new("@@signout",      "Sign Out",          "Sign out of Windows",     LucideIcon.User,       "#E91E63"),
        new("@@magnifier",    "Magnifier",         "Open Magnifier",          LucideIcon.Search,     "#00BCD4"),

        // Window & workspace management
        new("@@taskview",     "Task View",         "Open Task View",           LucideIcon.Layers,     "#5C7CFA"),
        new("@@snapleft",     "Snap Left",         "Snap window to left",      LucideIcon.ArrowLeft,  "#339AF0"),
        new("@@snapright",    "Snap Right",        "Snap window to right",     LucideIcon.ArrowRight, "#339AF0"),
        new("@@maximize",     "Maximize",          "Maximize current window",  LucideIcon.Maximize,   "#4CAF50"),
        new("@@newdesktop",   "New Desktop",       "Create virtual desktop",   LucideIcon.Plus,       "#9C27B0"),
        new("@@closedesktop", "Close Desktop",     "Close virtual desktop",    LucideIcon.X,          "#FF6B6B"),
        new("@@prevdesktop",  "Prev Desktop",      "Previous virtual desktop", LucideIcon.ArrowLeft,  "#00BCD4"),
        new("@@nextdesktop",  "Next Desktop",      "Next virtual desktop",     LucideIcon.ArrowRight, "#00BCD4"),
    };
}
