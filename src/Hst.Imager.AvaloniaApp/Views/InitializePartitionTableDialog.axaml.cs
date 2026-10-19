using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class InitializePartitionTableDialog : Window
{
    public InitializePartitionTableDialog()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(false);
        InitializeButton.Click += (_, _) => Close(true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Commit rigid disk block size on enter by moving focus, as size is updated on lost focus.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || e.Source is not TextBox)
            return;

        CancelButton.Focus();
        e.Handled = true;
    }
}
