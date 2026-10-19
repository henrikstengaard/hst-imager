using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class RdbFileSystemsDialog : Window
{
    public RdbFileSystemsDialog()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(false);
        OkButton.Click += (_, _) => Close(true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Commit values on enter by moving focus, as values are updated on lost focus.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || e.Source is not Control control)
            return;

        var textBox = control as TextBox ?? control.FindAncestorOfType<TextBox>();
        if (textBox == null || !textBox.Classes.Contains("commit-on-enter"))
            return;

        CancelButton.Focus();
        e.Handled = true;
    }
}
