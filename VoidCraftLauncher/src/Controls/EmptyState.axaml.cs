using Avalonia;
using Avalonia.Controls;

namespace VoidCraftLauncher.Controls;

public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Icon), "○");

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Title), "Nic tu není");

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Subtitle), string.Empty);

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public EmptyState()
    {
        InitializeComponent();
    }
}
