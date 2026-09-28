using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Hst.Imager.AvaloniaApp.Controls;

/// <summary>
/// Panel placing children in columns next to each other, when there is room for columns of at least
/// minimum column width. Otherwise children are stacked vertically.
/// </summary>
public class ResponsiveColumns : Panel
{
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<ResponsiveColumns, int>(nameof(MaxColumns), 2);

    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ResponsiveColumns, double>(nameof(MinColumnWidth), 400);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<ResponsiveColumns, double>(nameof(ColumnSpacing), 16);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<ResponsiveColumns, double>(nameof(RowSpacing), 8);

    static ResponsiveColumns()
    {
        AffectsMeasure<ResponsiveColumns>(MaxColumnsProperty, MinColumnWidthProperty, ColumnSpacingProperty,
            RowSpacingProperty);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private int GetColumns(double width) => double.IsInfinity(width)
        ? 1
        : Math.Clamp((int)((width + ColumnSpacing) / (MinColumnWidth + ColumnSpacing)), 1, Math.Max(1, MaxColumns));

    private double GetColumnWidth(double width, int columns) => columns == 1
        ? width
        : (width - ColumnSpacing * (columns - 1)) / columns;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = GetColumns(availableSize.Width);
        var columnWidth = GetColumnWidth(availableSize.Width, columns);
        var children = Children.Where(x => x.IsVisible).ToList();

        double height = 0, width = 0;
        for (var row = 0; row * columns < children.Count; row++)
        {
            double rowHeight = 0, rowWidth = 0;
            foreach (var child in children.Skip(row * columns).Take(columns))
            {
                child.Measure(new Size(columnWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                rowWidth += child.DesiredSize.Width;
            }

            height += (row > 0 ? RowSpacing : 0) + rowHeight;
            width = Math.Max(width, rowWidth + ColumnSpacing * (columns - 1));
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = GetColumns(finalSize.Width);
        var columnWidth = GetColumnWidth(finalSize.Width, columns);
        var children = Children.Where(x => x.IsVisible).ToList();

        double y = 0;
        for (var row = 0; row * columns < children.Count; row++)
        {
            var rowChildren = children.Skip(row * columns).Take(columns).ToList();
            var rowHeight = rowChildren.Max(x => x.DesiredSize.Height);
            for (var column = 0; column < rowChildren.Count; column++)
            {
                rowChildren[column].Arrange(new Rect(column * (columnWidth + ColumnSpacing), y, columnWidth,
                    rowHeight));
            }

            y += rowHeight + RowSpacing;
        }

        return finalSize;
    }
}
