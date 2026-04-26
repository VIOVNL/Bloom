using Avalonia.Controls;
using Avalonia.Input;
using Bloom.Helpers;

namespace Bloom.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        WireClose();
        WireDrag();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void WireClose()
    {
        var closeBtn = this.FindControl<Border>("CloseBtn")!;
        closeBtn.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Close();
        };
        KeyboardHelper.WireActivate(closeBtn, Close);
    }

    private void WireDrag()
    {
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
    }
}
