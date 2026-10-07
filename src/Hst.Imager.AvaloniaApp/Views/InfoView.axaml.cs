using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class InfoView : UserControl
{
    private InfoViewModel? _viewModel;

    public InfoView()
    {
        InitializeComponent();
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
        _viewModel = DataContext as InfoViewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateCylinderColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InfoViewModel.ShowCylinders))
            UpdateCylinderColumns();
    }

    private void UpdateCylinderColumns()
    {
        var showCylinders = _viewModel?.ShowCylinders ?? false;
        foreach (var column in PartitionsGrid.Columns.Where(x => x.Tag is "cylinder"))
            column.IsVisible = showCylinders;
    }
}
