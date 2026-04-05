using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VoidCraftLauncher.ViewModels;

namespace VoidCraftLauncher.Controls;

public partial class NavRail : UserControl
{
    public NavRail()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        ViewModel?.SetNavRailExpanded(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsPointerOver)
        {
            ViewModel?.SetNavRailExpanded(false);
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        ViewModel?.SetNavRailExpanded(true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsPointerOver && !IsKeyboardFocusWithin)
        {
            ViewModel?.SetNavRailExpanded(false);
        }
    }
}
