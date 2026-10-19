using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class MediaSelectionDialog : Window
{
    public MediaSelectionDialog()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(false);
        OkButton.Click += (_, _) => Close(true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close(false);
        e.Handled = true;
    }
}
