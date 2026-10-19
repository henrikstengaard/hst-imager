using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Partition layout of media selection shown in partition layout bar and list, shared by info, read, write, transfer
/// and compare views.
/// </summary>
public class MediaPartitionLayoutViewModel : ViewModelBase
{
    private MediaInfo? _mediaInfo;
    private List<DiskPartitionTable> _tables = [];
    private ObservableCollection<PartitionSegmentViewModel> _segments = [];
    private PartitionSegmentViewModel? _selectedSegment;

    /// <summary>
    /// Partition layout set by owner of media selection using set layout.
    /// </summary>
    public MediaPartitionLayoutViewModel(MediaSelectionViewModel media)
    {
        Media = media;
    }

    /// <summary>
    /// Partition layout read, when media info of media selection is loaded.
    /// </summary>
    public MediaPartitionLayoutViewModel(IMediaService mediaService, MediaSelectionViewModel media) : this(media)
    {
        this.WhenAnyValue(x => x.Media.Media)
            .Subscribe(x => _ = ReadAsync(mediaService, x));
    }

    public MediaSelectionViewModel Media { get; }

    /// <summary>
    /// Name of media used in title and description, title of media selection is used if not set.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Description with name and type of media as argument.
    /// </summary>
    public string DescriptionFormat { get; init; } = "Partition layout of {0}.";

    private string MediaName =>
        $"{(Name ?? Media.Options.Title).ToLowerInvariant()} {(Media.IsImageFile ? "image file" : "physical disk")}";

    public string Title => string.Concat(MediaName[..1].ToUpperInvariant(), MediaName[1..]);

    public string Description => string.Format(DescriptionFormat, MediaName);

    public ObservableCollection<PartitionSegmentViewModel> Segments
    {
        get => _segments;
        private set => this.RaiseAndSetIfChanged(ref _segments, value);
    }

    public PartitionSegmentViewModel? SelectedSegment
    {
        get => _selectedSegment;
        set => this.RaiseAndSetIfChanged(ref _selectedSegment, value);
    }

    public long DiskSize => _mediaInfo?.DiskSize ?? 0;

    public bool HasDiskInfo => _mediaInfo?.DiskInfo != null;

    /// <summary>
    /// Start and end cylinder columns are shown, when disk has a rigid disk block or PiStorm rigid disk block.
    /// </summary>
    public bool ShowCylinders => _tables.Any(x => x.Layout.IsRdb);

    public string SectorsOrCylindersText =>
        DiskPartitionTables.FormatSectorsOrCylinders(_tables.Select(x => x.Layout));

    public void SetLayout(MediaInfo? mediaInfo, List<DiskPartitionTable> tables)
    {
        _mediaInfo = mediaInfo;
        _tables = tables;
        Segments = new ObservableCollection<PartitionSegmentViewModel>(DiskPartitionTables.BuildSegments(tables));
        SelectedSegment = null;
        this.RaisePropertyChanged(nameof(DiskSize));
        this.RaisePropertyChanged(nameof(HasDiskInfo));
        this.RaisePropertyChanged(nameof(ShowCylinders));
        this.RaisePropertyChanged(nameof(SectorsOrCylindersText));
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(Description));
    }

    private async Task ReadAsync(IMediaService mediaService, MediaInfo? mediaInfo)
    {
        // errors reading PiStorm disks are ignored, as disk is still shown without them
        List<DiskPartitionTable> tables = [];
        var hasLayout = mediaInfo?.DiskInfo != null;
        try
        {
            if (hasLayout)
                tables = await DiskPartitionTables.ReadAsync(mediaService, mediaInfo, Media.Byteswap);
        }
        catch (Exception)
        {
            // partition layout isn't shown, if partition tables can't be read
            hasLayout = false;
        }

        // ignore result, if media was changed while reading partition tables
        if (!ReferenceEquals(mediaInfo, Media.Media)) return;

        SetLayout(hasLayout ? mediaInfo : null, tables);
    }
}
