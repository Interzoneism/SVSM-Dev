using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VintageStoryModManager.Views;

public partial class MainWindowAvalonia : Window
{
    public MainWindowAvalonia()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
