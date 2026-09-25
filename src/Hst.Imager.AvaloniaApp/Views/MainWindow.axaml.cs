using Avalonia.Controls;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
