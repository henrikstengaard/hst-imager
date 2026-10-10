using System;
using System.Globalization;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Resize rigid disk block dialog to change size of an existing rigid disk block, e.g. when an image file with a rigid
/// disk block is written to a larger image file or physical disk. Expanding requires free space after rigid disk
/// block and shrinking requires free space at end of rigid disk block after its last partition.
/// </summary>
public class ResizeRigidDiskBlockViewModel : ViewModelBase
{
    private readonly long _cylinderSize;
    private readonly string? _sizeLimitName;
    private long _size;

    /// <param name="title">Title of dialog.</param>
    /// <param name="originalSize">Size of rigid disk block read from disk.</param>
    /// <param name="size">Size of rigid disk block, which can be resized by a pending operation.</param>
    /// <param name="minSize">Min size, which is end of last partition in rigid disk block.</param>
    /// <param name="maxSize">Max size, which is end of free space after rigid disk block.</param>
    /// <param name="cylinderSize">Size of cylinders rigid disk block is aligned to.</param>
    /// <param name="diskSize">Size of disk rigid disk block is on.</param>
    /// <param name="diskName">Name of disk rigid disk block is on, e.g. disk or a master boot record partition for a
    /// PiStorm rigid disk block.</param>
    /// <param name="sizeLimitName">Name of master boot record partition after rigid disk block limiting max size or
    /// null, if max size is limited by end of disk.</param>
    public ResizeRigidDiskBlockViewModel(string title, long originalSize, long size, long minSize, long maxSize,
        long cylinderSize, long diskSize, string diskName, string? sizeLimitName)
    {
        Title = title;
        OriginalSize = originalSize;
        MinSize = minSize;
        MaxSize = maxSize;
        DiskSize = diskSize;
        DiskName = diskName;
        _cylinderSize = cylinderSize;
        _sizeLimitName = sizeLimitName;
        _size = size;

        UseMinSizeCommand = ReactiveCommand.Create(() => SetSize(MinSize));
        UseMaxSizeCommand = ReactiveCommand.Create(() => SetSize(MaxSize));
        UseOriginalSizeCommand = ReactiveCommand.Create(() => SetSize(OriginalSize));
    }

    public string Title { get; }

    public long OriginalSize { get; }
    public long MinSize { get; }
    public long MaxSize { get; }
    public long DiskSize { get; }
    public string DiskName { get; }

    /// <summary>
    /// Size of rigid disk block aligned down to cylinders. Size read from disk is kept as is.
    /// </summary>
    public long Size => _size == OriginalSize || _cylinderSize <= 0 ? _size : _size / _cylinderSize * _cylinderSize;

    /// <summary>
    /// Size of rigid disk block in MB. Min, max and size read from disk are used as is, when their formatted value is
    /// entered, as they can't be represented exactly in MB with two decimals.
    /// </summary>
    public string SizeText
    {
        get => FormatMb(_size);
        set
        {
            if (value == FormatMb(MaxSize))
                _size = MaxSize;
            else if (value == FormatMb(MinSize))
                _size = MinSize;
            else if (value == FormatMb(OriginalSize))
                _size = OriginalSize;
            else if ((decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) ||
                      decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) &&
                     number >= 0)
                _size = (long)(number * PartitionLayout.MiB);

            RaiseSizeChanged();
        }
    }

    public string SizeHelpText =>
        $"Min size {MediaOptions.FormatBytes(MinSize)}, which is end of last partition in Rigid Disk Block. Max size {MediaOptions.FormatBytes(MaxSize)}, which is {(_sizeLimitName == null ? $"end of {DiskName}" : $"start of {_sizeLimitName}")}. Size is aligned to cylinders of {MediaOptions.FormatBytes(_cylinderSize)}.";

    public string SizeInfoText =>
        $"Rigid Disk Block is {MediaOptions.FormatBytes(OriginalSize)} and {DiskName} is {MediaOptions.FormatBytes(DiskSize)}.";

    /// <summary>
    /// Description of resize, when size is valid.
    /// </summary>
    public string ResultText => HasError
        ? string.Empty
        : Size == OriginalSize
            ? "Size is same as Rigid Disk Block on disk, so it isn't resized."
            : Size > OriginalSize
                ? $"Rigid Disk Block is expanded from {MediaOptions.FormatBytes(OriginalSize)} to {MediaOptions.FormatBytes(Size)} adding {MediaOptions.FormatBytes(Size - OriginalSize)} of unallocated space at end of Rigid Disk Block."
                : $"Rigid Disk Block is shrunk from {MediaOptions.FormatBytes(OriginalSize)} to {MediaOptions.FormatBytes(Size)} freeing {MediaOptions.FormatBytes(OriginalSize - Size)} of unallocated space at end of Rigid Disk Block.";

    public bool HasResultText => !string.IsNullOrEmpty(ResultText);

    /// <summary>
    /// Expanding requires free space after rigid disk block and shrinking requires free space at end of rigid disk
    /// block after its last partition.
    /// </summary>
    public string ErrorMessage
    {
        get
        {
            if (Size == OriginalSize)
                return string.Empty;

            if (MaxSize < MinSize)
                return $"Rigid Disk Block can't be resized, as its partitions use space up to {MediaOptions.FormatBytes(MinSize)}, which is beyond {(_sizeLimitName == null ? $"end of {DiskName}" : $"start of {_sizeLimitName}")}.";

            if (Size > MaxSize)
                return _sizeLimitName == null
                    ? $"Expanding Rigid Disk Block to {MediaOptions.FormatBytes(Size)} requires free space after Rigid Disk Block, but {DiskName} only has space for a Rigid Disk Block of {MediaOptions.FormatBytes(MaxSize)}. Reduce size."
                    : $"Expanding Rigid Disk Block to {MediaOptions.FormatBytes(Size)} requires free space after Rigid Disk Block, but there's only free space up to {MediaOptions.FormatBytes(MaxSize)}, where {_sizeLimitName} starts. Reduce size or delete Master Boot Record partitions after Rigid Disk Block first.";

            if (Size < MinSize)
                return $"Shrinking Rigid Disk Block to {MediaOptions.FormatBytes(Size)} requires free space at end of Rigid Disk Block, but its partitions use space up to {MediaOptions.FormatBytes(MinSize)}. Increase size or delete Rigid Disk Block partitions at end of Rigid Disk Block first.";

            return string.Empty;
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanResize => !HasError;

    /// <summary>
    /// Shrink rigid disk block to end of its last partition.
    /// </summary>
    public ReactiveCommand<Unit, Unit> UseMinSizeCommand { get; }

    public ReactiveCommand<Unit, Unit> UseMaxSizeCommand { get; }
    public ReactiveCommand<Unit, Unit> UseOriginalSizeCommand { get; }

    private void SetSize(long size)
    {
        _size = size;
        RaiseSizeChanged();
    }

    private static string FormatMb(long bytes) =>
        ((decimal)bytes / PartitionLayout.MiB).ToString("0.##", CultureInfo.CurrentCulture);

    private void RaiseSizeChanged()
    {
        this.RaisePropertyChanged(nameof(SizeText));
        this.RaisePropertyChanged(nameof(Size));
        this.RaisePropertyChanged(nameof(ErrorMessage));
        this.RaisePropertyChanged(nameof(HasError));
        this.RaisePropertyChanged(nameof(CanResize));
        this.RaisePropertyChanged(nameof(ResultText));
        this.RaisePropertyChanged(nameof(HasResultText));
    }
}
