using Avalonia.Controls;
using Avalonia.Input;
using Bloom.Models;

namespace Bloom.Views;

public partial class TutorialWindow : Window
{
    private int _step;
    private readonly Border[] _dots;

    private static readonly (string Title, string Description, string IconColor)[] Steps =
    {
        ("Welcome to Bloom",
         "Your apps live in the floating flower button.\nLeft-click it to open your launcher.",
         "#339AF0"),

        ("Launch Your Apps",
         "Click any petal to launch it.\nHover over a petal to see its name.",
         "#51CF66"),

        ("Right-Click for More",
         "Right-click the flower to access settings,\nadd new items, report bugs, and more.",
         "#FF922B"),

        ("Edit Your Petals",
         "Right-click any petal to change its label,\nicon, color, or remove it entirely.",
         "#B197FC"),

        ("Drag & Drop",
         "Drag files, folders, or shortcuts directly\nonto the flower to add them instantly.",
         "#FF6B6B"),

        ("Organize with Groups",
         "Create groups to organize your items into\nfolders that open as nested blooms.",
         "#CC5DE8"),

        ("Keyboard Hotkeys",
         "Open Hotkeys from Settings to assign global\nshortcuts. Toggle petals, toggle the bloom\nwindow, or launch any item with a key combo.",
         "#FF6B6B"),

        ("Move It Anywhere",
         "Drag the flower to reposition it\nanywhere on your screen.",
         "#22B8CF"),

        ("Stay Up to Date",
         "When an update is available, an Update petal\nappears in the right-click menu. Click it to update.",
         "#FFD43B"),

        ("You're All Set!",
         "Start adding your favorite apps\nand make Bloom your own.",
         "#51CF66"),
    };

    private static readonly string[] StepIcons =
    {
        LucideIcon.Flower2.PathData,
        LucideIcon.MousePointerClick.PathData,
        LucideIcon.Settings.PathData,
        LucideIcon.Paintbrush.PathData,
        LucideIcon.FilePlus.PathData,
        LucideIcon.FolderPlus.PathData,
        LucideIcon.Keyboard.PathData,
        LucideIcon.Move.PathData,
        LucideIcon.Download.PathData,
        LucideIcon.Sparkles.PathData,
    };

    public TutorialWindow()
    {
        InitializeComponent();

        _dots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5, Dot6, Dot7, Dot8, Dot9 };

        // Draggable
        Shell.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        NextBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (_step < Steps.Length - 1)
            {
                _step++;
                UpdateStep();
            }
            else
            {
                Close(true);
            }
        };

        BackBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (_step > 0)
            {
                _step--;
                UpdateStep();
            }
        };

        SkipLink.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Close(false);
        };

        UpdateStep();
    }

    private void UpdateStep()
    {
        var (title, desc, iconColor) = Steps[_step];

        StepTitle.Text = title;
        StepDesc.Text = desc;
        StepIcon.Data = Avalonia.Media.Geometry.Parse(StepIcons[_step]);
        StepIcon.Stroke = Avalonia.Media.Brush.Parse(iconColor);

        // Dots
        for (int i = 0; i < _dots.Length; i++)
        {
            if (i == _step)
                _dots[i].Classes.Add("active");
            else
                _dots[i].Classes.Remove("active");
        }

        // Back button visibility
        BackBtn.IsVisible = _step > 0;

        // Next button text
        NextBtnText.Text = _step == Steps.Length - 1 ? "Get Started" : "Next";
    }
}
