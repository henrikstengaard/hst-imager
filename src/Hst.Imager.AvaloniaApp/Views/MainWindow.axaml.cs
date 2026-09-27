using System;
using Avalonia.Controls;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // app started elevated on macOS is started directly as root and not by launch services,
        // so it isn't activated and main window can be opened behind other windows
        if (Hst.Core.OperatingSystem.IsMacOs() && viewModel.IsElevated)
        {
            Opened += BringToFront;
        }
    }

    private void BringToFront(object? sender, EventArgs e)
    {
        Opened -= BringToFront;
        Topmost = true;
        Activate();
        Topmost = false;
    }
}
