using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class PartitionView : UserControl
{
    static PartitionView()
    {
        // text changed in hidden sections, like partition table and disk size when loading, isn't measured again
        // when section is shown. invalidate measure of section's descendants after section is shown and bindings
        // are updated
        IsVisibleProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (!control.IsVisible || control.FindAncestorOfType<PartitionView>() == null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var descendant in control.GetVisualDescendants().OfType<Layoutable>())
                    descendant.InvalidateMeasure();
            }, DispatcherPriority.Background);
        });
    }

    private PartitionViewModel? _viewModel;

    public PartitionView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Data grid columns aren't in logical tree and can't bind to view model, so cylinder columns are shown by view
    /// when disk has a rigid disk block.
    /// </summary>
    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as PartitionViewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateCylinderColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PartitionViewModel.ShowCylinders))
            UpdateCylinderColumns();
    }

    private void UpdateCylinderColumns()
    {
        var showCylinders = _viewModel?.ShowCylinders ?? false;
        foreach (var column in PartitionsGrid.Columns.Where(x => x.Tag is "cylinder"))
            column.IsVisible = showCylinders;
    }

    /// <summary>
    /// Commit size values on enter by moving focus to partition layout, as size values are updated on lost focus.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not Control control)
            return;

        var textBox = control as TextBox ?? control.FindAncestorOfType<TextBox>();
        if (textBox == null || !textBox.Classes.Contains("commit-on-enter"))
            return;

        LayoutBar.Focus();
        e.Handled = true;
    }
}
