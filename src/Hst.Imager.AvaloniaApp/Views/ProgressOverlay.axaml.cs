using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Views;

public partial class ProgressOverlay : UserControl
{
    private ProgressViewModel? _viewModel;

    /// <summary>
    /// Details list follows output and scrolls down with new lines, while scrolled to the bottom.
    /// </summary>
    private bool _followDetails = true;

    private bool _scrollPending;

    private const double FollowTolerance = 24;

    public ProgressOverlay()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.DetailLines.CollectionChanged -= OnDetailLinesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ProgressViewModel;

        if (_viewModel != null)
        {
            _viewModel.DetailLines.CollectionChanged += OnDetailLinesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // show latest lines, when details are expanded
        if (e.PropertyName == nameof(ProgressViewModel.IsDetailsExpanded) && _viewModel?.IsDetailsExpanded == true)
        {
            _followDetails = true;
            ScrollToEnd();
        }
    }

    private void OnDetailLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _followDetails = true;
            return;
        }

        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        // follow output, if scrolled to the bottom before new lines are laid out. scroll viewer still has
        // extent from before new lines were added, so user scrolling up stops following and scrolling back to the
        // bottom follows output again
        if (!_scrollPending)
        {
            _followDetails = IsScrolledToBottom();
        }

        if (_followDetails)
        {
            ScrollToEnd();
        }
    }

    private bool IsScrolledToBottom()
    {
        var scrollViewer = DetailsList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer == null || !DetailsList.IsEffectivelyVisible)
        {
            return true;
        }

        // tolerance of about one line, as extent of virtualized list is estimated and can change after scrolling
        return scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - FollowTolerance;
    }

    /// <summary>
    /// Scroll to last line after layout is updated with new lines. Lines are added in batches, so only one scroll
    /// is pending at a time.
    /// </summary>
    private void ScrollToEnd()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scrollPending = false;
            var count = _viewModel?.DetailLines.Count ?? 0;
            if (!_followDetails || count == 0 || !DetailsList.IsEffectivelyVisible)
            {
                return;
            }

            DetailsList.ScrollIntoView(count - 1);
            DetailsList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
