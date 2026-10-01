using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Controls;

/// <summary>
/// Visual view of partition layout similar to gparted, showing partitions and unallocated space proportional to
/// their size. Segments are selected by clicking. New partitions are resized by dragging their edges and moved by
/// dragging them. Hovering unallocated space shows an add icon and clicking it adds a partition.
/// Segments of multiple partition tables are shown as one disk, where partition tables in partitions like PiStorm rigid
/// disk blocks are shown nested in the partition containing them. A band above segments shows the area of each
/// partition table in its color and nested partition tables have a header in their color.
/// </summary>
public class PartitionLayoutBar : Control
{
    private const double EdgeGrabWidth = 6;
    private const double MinSegmentWidth = 3;
    private const double BorderWidth = 3;

    // height of container partition header, which nested segments are shown below
    private const double NestedHeaderHeight = 18;

    // height of band above segments showing partition tables
    private const double TableBandHeight = 18;

    public static readonly StyledProperty<long> DiskSizeProperty =
        AvaloniaProperty.Register<PartitionLayoutBar, long>(nameof(DiskSize));

    public static readonly StyledProperty<IEnumerable<PartitionSegmentViewModel>?> SegmentsProperty =
        AvaloniaProperty.Register<PartitionLayoutBar, IEnumerable<PartitionSegmentViewModel>?>(nameof(Segments));

    public static readonly StyledProperty<PartitionSegmentViewModel?> SelectedSegmentProperty =
        AvaloniaProperty.Register<PartitionLayoutBar, PartitionSegmentViewModel?>(nameof(SelectedSegment),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> AddPartitionCommandProperty =
        AvaloniaProperty.Register<PartitionLayoutBar, ICommand?>(nameof(AddPartitionCommand));

    private DragState? _drag;

    // start offset of hovered unallocated space, as segments are rebuilt when layout changes
    private long? _hoverStart;

    static PartitionLayoutBar()
    {
        AffectsRender<PartitionLayoutBar>(DiskSizeProperty, SegmentsProperty, SelectedSegmentProperty);
        FocusableProperty.OverrideDefaultValue<PartitionLayoutBar>(true);
    }

    public PartitionLayoutBar()
    {
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>
    /// Size of disk shown in bar.
    /// </summary>
    public long DiskSize
    {
        get => GetValue(DiskSizeProperty);
        set => SetValue(DiskSizeProperty, value);
    }

    public IEnumerable<PartitionSegmentViewModel>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public PartitionSegmentViewModel? SelectedSegment
    {
        get => GetValue(SelectedSegmentProperty);
        set => SetValue(SelectedSegmentProperty, value);
    }

    /// <summary>
    /// Command executed with unallocated segment clicked to add a partition to it.
    /// </summary>
    public ICommand? AddPartitionCommand
    {
        get => GetValue(AddPartitionCommandProperty);
        set => SetValue(AddPartitionCommandProperty, value);
    }

    private bool CanAddPartitionTo(PartitionSegmentViewModel? segment) =>
        segment != null && AddPartitionCommand != null && segment.Layout.CanAddPartitionTo(segment);

    private PartitionSegmentViewModel? HoveredSegment => _hoverStart == null
        ? null
        : Segments?.FirstOrDefault(x => x.IsUnallocated && x.DiskStart == _hoverStart && CanAddPartitionTo(x));

    private enum DragMode
    {
        Start,
        End,
        Move
    }

    private sealed record DragState(PartitionLayout Layout, PartitionEntryViewModel Partition, DragMode Mode,
        double StartX, long OriginalStart, long OriginalEnd);

    // ─── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Size shown in bar from offset 0, which includes unusable space before and after usable space.
    /// </summary>
    private long ViewSize => Math.Max(DiskSize, Segments?.Select(x => x.DiskEnd).DefaultIfEmpty(0).Max() ?? 0);

    private double ToX(long offset) => ViewSize <= 0 ? 0 : Bounds.Width * ((double)offset / ViewSize);

    private long ToBytes(double width) => ViewSize <= 0 || Bounds.Width <= 0
        ? 0
        : (long)(width / Bounds.Width * ViewSize);

    /// <summary>
    /// Get rectangle of segment below partition table band. Nested segments are placed below header of partition
    /// containing them.
    /// </summary>
    private Rect GetSegmentRect(PartitionSegmentViewModel segment)
    {
        var x0 = ToX(segment.DiskStart);
        var x1 = Math.Max(ToX(segment.DiskEnd), x0 + MinSegmentWidth);
        var y = TableBandHeight + segment.Depth * NestedHeaderHeight;
        var height = Math.Max(0, Bounds.Height - y - segment.Depth * BorderWidth);
        return new Rect(x0, y, x1 - x0, height);
    }

    // ─── Rendering ────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var panelBrush = GetBrush("AppPanelBackgroundBrush", Brushes.White);
        var dividerBrush = GetBrush("AppDividerBrush", Brushes.Gray);
        var textBrush = GetBrush("AppStrongForegroundBrush", Brushes.Black);
        var selectedBrush = GetBrush("AppPrimaryBackgroundBrush", Brushes.DodgerBlue);

        // unusable space is drawn as divider background
        context.FillRectangle(dividerBrush, bounds);

        // nested segments are drawn on top of partition containing them
        var segments = Segments?.OrderBy(x => x.Depth).ToList() ?? [];
        DrawTableBand(context, segments);
        foreach (var segment in segments)
        {
            var rect = GetSegmentRect(segment);
            var color = Color.Parse(segment.Color);
            var colorBrush = new SolidColorBrush(color);

            context.FillRectangle(colorBrush, rect);

            var inner = rect.Deflate(BorderWidth);
            if (inner.Width <= 0 || inner.Height <= 0)
                continue;

            if (segment.Partition == null)
            {
                context.FillRectangle(new SolidColorBrush(color, 0.35), inner);
            }
            else
            {
                context.FillRectangle(panelBrush, inner);

                // used space of existing partitions
                var partition = segment.Partition!;
                if (partition.UsedSize is > 0 && !partition.FormatRequested && partition.Size > 0)
                {
                    var usedWidth = inner.Width * Math.Min(1, (double)partition.UsedSize.Value / partition.Size);
                    context.FillRectangle(new SolidColorBrush(color, 0.45),
                        new Rect(inner.X, inner.Y, usedWidth, inner.Height));
                }

                // new partitions are marked with dashed border
                if (partition.IsNew)
                {
                    var dashPen = new Pen(colorBrush, 1, new DashStyle([4, 3], 0));
                    context.DrawRectangle(dashPen, inner.Deflate(3));
                }
            }

            if (segment.IsContainer)
                DrawContainerHeader(context, segment, inner);
            else
                DrawSegmentText(context, segment, inner, textBrush);
        }

        // hovered unallocated space shows add icon
        var hovered = HoveredSegment;
        if (hovered != null)
            DrawAddOverlay(context, GetSegmentRect(hovered), selectedBrush, panelBrush, textBrush);

        // selected segment is drawn last, so it's on top of neighbours
        var selected = SelectedSegment;
        if (selected != null && segments.Contains(selected))
        {
            var rect = GetSegmentRect(selected);
            context.DrawRectangle(new Pen(selectedBrush, 3), rect.Deflate(1.5));
        }

        context.DrawRectangle(new Pen(dividerBrush, 1), bounds.Deflate(0.5));
    }

    private void DrawSegmentText(DrawingContext context, PartitionSegmentViewModel segment, Rect rect,
        IBrush textBrush)
    {
        if (rect.Width < 24)
            return;

        var typeface = new Typeface(FontFamily.Default);
        var name = CreateText(segment.Name, typeface, 12, FontWeight.SemiBold, textBrush);
        var details = CreateText(
            string.IsNullOrEmpty(segment.FileSystem) ? segment.SizeText : $"{segment.FileSystem}, {segment.SizeText}",
            typeface, 11, FontWeight.Normal, textBrush);

        var totalHeight = name.Height + details.Height;
        var y = rect.Y + Math.Max(0, (rect.Height - totalHeight) / 2);

        using (context.PushClip(rect))
        {
            context.DrawText(name, new Point(rect.X + Math.Max(4, (rect.Width - name.Width) / 2), y));
            context.DrawText(details,
                new Point(rect.X + Math.Max(4, (rect.Width - details.Width) / 2), y + name.Height));
        }
    }

    /// <summary>
    /// Draw band above segments with area of each partition table on disk in its color and named by it, so it's
    /// visible which partitions belong to which partition table, e.g. rigid disk block and master boot record of a
    /// hybrid disk. Area of partition table spans its partitions and unallocated space.
    /// </summary>
    private void DrawTableBand(DrawingContext context, IEnumerable<PartitionSegmentViewModel> segments)
    {
        var tables = segments
            .Where(x => x.Depth == 0 && x.Layout.HasPartitionTable)
            .GroupBy(x => x.Layout)
            .Select(x => (Layout: x.Key, Start: x.Min(s => s.DiskStart), End: x.Max(s => s.DiskEnd)));

        foreach (var (layout, start, end) in tables)
        {
            var x0 = ToX(start);
            var x1 = Math.Max(ToX(end), x0 + MinSegmentWidth);
            var rect = new Rect(x0, 0, x1 - x0, TableBandHeight - 2);
            context.FillRectangle(new SolidColorBrush(Color.Parse(PartitionLayout.GetTableTypeColor(layout.TableType))),
                rect);
            DrawTableName(context, layout, rect);
        }
    }

    /// <summary>
    /// Draw header of partition containing a nested partition table in color of nested partition table, e.g. a
    /// PiStorm rigid disk block in a master boot record partition.
    /// </summary>
    private static void DrawContainerHeader(DrawingContext context, PartitionSegmentViewModel segment, Rect rect)
    {
        var layout = segment.NestedLayout!;
        var header = new Rect(rect.X, rect.Y, rect.Width, NestedHeaderHeight - BorderWidth);
        if (header.Width <= 0)
            return;

        context.FillRectangle(new SolidColorBrush(Color.Parse(PartitionLayout.GetTableTypeColor(layout.TableType))),
            header);
        DrawTableName(context, layout, header, $" in {segment.Name}, {segment.SizeText}");
    }

    /// <summary>
    /// Draw name of partition table left aligned in rectangle. Abbreviation is used, when name doesn't fit.
    /// </summary>
    private static void DrawTableName(DrawingContext context, PartitionLayout layout, Rect rect, string suffix = "")
    {
        var typeface = new Typeface(FontFamily.Default);
        var name = layout.HasPartitionTable
            ? $"{layout.TableTypeName}{(layout.IsInitialize ? " (new)" : string.Empty)}"
            : "No partition table";
        var abbreviation = layout.HasPartitionTable
            ? PartitionLayout.GetTableTypeAbbreviation(layout.TableType)
            : string.Empty;

        var text = new[] { name + suffix, name, abbreviation }
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => CreateText(x, typeface, 11, FontWeight.SemiBold, Brushes.White))
            .FirstOrDefault(x => x.Width + 8 <= rect.Width);
        if (text == null)
            return;

        using (context.PushClip(rect))
            context.DrawText(text, new Point(rect.X + 4, rect.Y + Math.Max(0, (rect.Height - text.Height) / 2)));
    }

    private void DrawAddOverlay(DrawingContext context, Rect rect, IBrush accentBrush, IBrush panelBrush,
        IBrush textBrush)
    {
        var inner = rect.Deflate(BorderWidth);
        if (inner.Width <= 0 || inner.Height <= 0)
            return;

        context.FillRectangle(panelBrush, inner);
        context.FillRectangle(new SolidColorBrush(((ISolidColorBrush)accentBrush).Color, 0.15), inner);

        var label = CreateText($"Add {HoveredSegment?.Layout.TableTypeName} partition", new Typeface(FontFamily.Default), 11,
            FontWeight.SemiBold, textBrush);
        var showLabel = inner.Width >= label.Width + 8;

        // circle with plus icon above label
        const double radius = 9;
        var center = new Point(inner.Center.X,
            showLabel ? inner.Center.Y - label.Height / 2 - 1 : inner.Center.Y);
        var pen = new Pen(accentBrush, 2);
        using (context.PushClip(inner))
        {
            context.DrawEllipse(accentBrush, null, center, radius, radius);
            var plusPen = new Pen(Brushes.White, 2);
            context.DrawLine(plusPen, center + new Point(-4.5, 0), center + new Point(4.5, 0));
            context.DrawLine(plusPen, center + new Point(0, -4.5), center + new Point(0, 4.5));
            context.DrawRectangle(pen, inner.Deflate(1));

            if (showLabel)
                context.DrawText(label, new Point(inner.Center.X - label.Width / 2, center.Y + radius + 2));
        }
    }

    private static FormattedText CreateText(string text, Typeface typeface, double size, FontWeight weight,
        IBrush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(typeface.FontFamily, FontStyle.Normal, weight), size, brush);

    private IBrush GetBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : fallback;

    // ─── Pointer ──────────────────────────────────────────────────────────────

    private (PartitionSegmentViewModel? Segment, DragMode? Mode) HitTest(Point point)
    {
        // nested segments are hit before partition containing them
        var segments = Segments?.OrderByDescending(x => x.Depth).ToList() ?? [];

        // edges of new partitions are prioritized, so small partitions can be resized
        foreach (var segment in segments.Where(x => x.Partition is { IsNew: true }))
        {
            var rect = GetSegmentRect(segment);
            var grab = Math.Min(EdgeGrabWidth, rect.Width / 3);
            if (Math.Abs(point.X - rect.Left) <= grab)
                return (segment, DragMode.Start);
            if (Math.Abs(point.X - rect.Right) <= grab)
                return (segment, DragMode.End);
        }

        foreach (var segment in segments)
        {
            if (GetSegmentRect(segment).Contains(point))
                return (segment, segment.Partition is { IsNew: true } ? DragMode.Move : null);
        }

        return (null, null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        Focus();
        var (segment, mode) = HitTest(point.Position);
        if (segment == null)
            return;

        SelectedSegment = segment;

        if (CanAddPartitionTo(segment))
        {
            if (AddPartitionCommand!.CanExecute(segment))
                AddPartitionCommand.Execute(segment);
            SetHover(null);
            e.Handled = true;
            return;
        }

        if (mode != null && segment.Partition != null)
        {
            _drag = new DragState(segment.Layout, segment.Partition, mode.Value, point.Position.X,
                segment.Partition.Start, segment.Partition.End);
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        if (_drag == null)
        {
            var (segment, mode) = HitTest(position);
            var canAdd = mode == null && CanAddPartitionTo(segment);
            SetHover(canAdd ? segment!.DiskStart : null);
            Cursor = mode switch
            {
                DragMode.Start or DragMode.End => new Cursor(StandardCursorType.SizeWestEast),
                DragMode.Move => new Cursor(StandardCursorType.SizeAll),
                _ => canAdd ? new Cursor(StandardCursorType.Hand) : Cursor.Default
            };
            ToolTip.SetTip(this, canAdd ? $"Add {segment!.Layout.TableTypeName} partition" : null);
            return;
        }

        var delta = ToBytes(position.X - _drag.StartX);
        switch (_drag.Mode)
        {
            case DragMode.Start:
                _drag.Layout.ResizeStart(_drag.Partition, _drag.OriginalStart + delta);
                break;
            case DragMode.End:
                _drag.Layout.ResizeEnd(_drag.Partition, _drag.OriginalEnd + delta);
                break;
            case DragMode.Move:
                _drag.Layout.Move(_drag.Partition, _drag.OriginalStart + delta);
                break;
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag == null)
            return;

        _drag = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    private void SetHover(long? start)
    {
        if (_hoverStart == start)
            return;
        _hoverStart = start;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = null;
    }
}
