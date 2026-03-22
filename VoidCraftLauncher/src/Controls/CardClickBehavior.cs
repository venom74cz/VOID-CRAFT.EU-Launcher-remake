using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VoidCraftLauncher.Controls;

/// <summary>
/// Attached behavior: routes PointerPressed on a card container to a command,
/// while ignoring clicks that land on child buttons (e.g. Play button).
/// Eliminates the need for code-behind pointer event handlers.
/// 
/// Usage in XAML:
///   CardClickBehavior.Command="{Binding SomeCommand}"
///   CardClickBehavior.CommandParameter="{Binding}"
///   CardClickBehavior.IgnoreClass="PlayButton"
/// </summary>
public static class CardClickBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(CardClickBehavior));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(CardClickBehavior));

    public static readonly AttachedProperty<string> IgnoreClassProperty =
        AvaloniaProperty.RegisterAttached<Control, string>("IgnoreClass", typeof(CardClickBehavior), "PlayButton");

    static CardClickBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static void SetCommand(Control element, ICommand? value) => element.SetValue(CommandProperty, value);
    public static ICommand? GetCommand(Control element) => element.GetValue(CommandProperty);

    public static void SetCommandParameter(Control element, object? value) => element.SetValue(CommandParameterProperty, value);
    public static object? GetCommandParameter(Control element) => element.GetValue(CommandParameterProperty);

    public static void SetIgnoreClass(Control element, string value) => element.SetValue(IgnoreClassProperty, value);
    public static string GetIgnoreClass(Control element) => element.GetValue(IgnoreClassProperty);

    private static void OnCommandChanged(Control sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is ICommand)
        {
            sender.PointerPressed += OnPointerPressed;
        }
        else
        {
            sender.PointerPressed -= OnPointerPressed;
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        var ignoreClass = GetIgnoreClass(control);

        // Walk visual tree upward: if we hit a button or the ignored class, bail out
        var source = e.Source as Control;
        while (source != null && source != control)
        {
            if (source is Button) return;
            if (!string.IsNullOrEmpty(ignoreClass) && source.Classes.Contains(ignoreClass)) return;
            source = source.GetVisualParent() as Control;
        }

        var command = GetCommand(control);
        var parameter = GetCommandParameter(control);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
            e.Handled = true;
        }
    }
}
