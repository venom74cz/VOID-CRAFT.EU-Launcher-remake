using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoidCraftLauncher.Controls;

public partial class UpdatePromptSheet : UserControl
{
    public UpdatePromptSheet()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
