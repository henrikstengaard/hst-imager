using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hst.Imager.Core.FileSystems;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Cluster sizes, which can be selected when formatting FAT32 and exFAT file systems. Value is cluster size in bytes
/// and auto (0) calculates optimal cluster size from partition size.
/// </summary>
public static class ClusterSizeOptions
{
    public const int Auto = 0;

    private static readonly List<SelectOption> Fat32Options = Create(ClusterSizeHelper.Fat32ClusterSizes);
    private static readonly List<SelectOption> ExFatOptions = Create(ClusterSizeHelper.ExFatClusterSizes);

    /// <summary>
    /// Cluster size can be selected for file system, which are fat32 and exfat.
    /// </summary>
    public static bool IsSupported(string fileSystem) => fileSystem is "fat32" or "exfat";

    /// <summary>
    /// Cluster size options for file system with auto as first option or empty, if file system doesn't support
    /// selecting cluster size.
    /// </summary>
    public static List<SelectOption> GetOptions(string fileSystem) => fileSystem switch
    {
        "fat32" => Fat32Options,
        "exfat" => ExFatOptions,
        _ => []
    };

    /// <summary>
    /// Cluster size in bytes of option or auto, if option is not set.
    /// </summary>
    public static int GetClusterSize(SelectOption? option) =>
        option != null && int.TryParse(option.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            ? size
            : Auto;

    private static List<SelectOption> Create(IEnumerable<int> clusterSizes) =>
    [
        new() { Title = "Auto (optimal)", Value = Auto.ToString(CultureInfo.InvariantCulture) },
        ..clusterSizes.Select(x => new SelectOption
        {
            Title = ClusterSizeHelper.Format(x),
            Value = x.ToString(CultureInfo.InvariantCulture)
        })
    ];
}
