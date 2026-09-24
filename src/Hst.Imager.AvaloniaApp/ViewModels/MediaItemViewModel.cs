using Humanizer;
using Humanizer.Bytes;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class MediaItemViewModel
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long DiskSize { get; set; }
    public bool IsPhysicalDrive { get; set; }
    public MediaInfo? MediaInfo { get; set; }

    public string DisplayName => Name;
    public string SizeFormatted => DiskSize > 0 ? Humanizer.Bytes.ByteSize.FromBytes(DiskSize).Humanize("#.#") : string.Empty;
    public string Label => string.IsNullOrEmpty(SizeFormatted) ? Name : $"{Name} ({SizeFormatted})";
}
