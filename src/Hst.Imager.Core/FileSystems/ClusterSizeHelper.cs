using System.Collections.Generic;
using Hst.Core;
using Hst.Imager.Core.FileSystems.ExFat;
using Hst.Imager.Core.FileSystems.Fat32;

namespace Hst.Imager.Core.FileSystems;

/// <summary>
/// Cluster sizes for file systems formatted with selectable cluster size: FAT32 and exFAT.
/// A cluster size of 0 uses optimal cluster size calculated from partition size.
/// </summary>
public static class ClusterSizeHelper
{
    /// <summary>
    /// Cluster sizes in bytes supported by FAT32
    /// </summary>
    public static readonly IReadOnlyList<int> Fat32ClusterSizes = CreateClusterSizes(512, 65536);

    /// <summary>
    /// Cluster sizes in bytes supported by exFAT
    /// </summary>
    public static readonly IReadOnlyList<int> ExFatClusterSizes = CreateClusterSizes(512, ExFatFormatter.MaxClusterSize);

    /// <summary>
    /// Validate cluster size for file system
    /// </summary>
    /// <param name="fileSystem">Name of file system</param>
    /// <param name="isFat32">File system is FAT32</param>
    /// <param name="isExFat">File system is exFAT</param>
    /// <param name="clusterSize">Cluster size in bytes (0 = optimal cluster size)</param>
    /// <param name="bytesPerSector">Bytes per sector</param>
    /// <returns></returns>
    public static Result Validate(string fileSystem, bool isFat32, bool isExFat, int clusterSize, int bytesPerSector)
    {
        if (clusterSize == 0)
        {
            return new Result();
        }

        if (!isFat32 && !isExFat)
        {
            return new Result(new Error($"Cluster size is not supported for file system '{fileSystem}', only FAT32 and exFAT"));
        }

        if (isFat32 && !Fat32Formatter.IsValidClusterSize(clusterSize, bytesPerSector))
        {
            return new Result(new Error(
                $"Invalid FAT32 cluster size {clusterSize}. Must be a power of 2 between {(bytesPerSector > 512 ? bytesPerSector : 512)} and 65536"));
        }

        if (isExFat && !ExFatFormatter.IsValidClusterSize(clusterSize, bytesPerSector))
        {
            return new Result(new Error(
                $"Invalid exFAT cluster size {clusterSize}. Must be a power of 2 between {bytesPerSector} and {ExFatFormatter.MaxClusterSize}"));
        }

        return new Result();
    }

    /// <summary>
    /// Format cluster size for display, e.g. 512 bytes, 32 KB or 1 MB
    /// </summary>
    /// <param name="clusterSize">Cluster size in bytes (0 = optimal cluster size)</param>
    /// <returns></returns>
    public static string Format(int clusterSize)
    {
        return clusterSize switch
        {
            <= 0 => "Auto",
            < 1024 => $"{clusterSize} bytes",
            < 1024 * 1024 => $"{clusterSize / 1024} KB",
            _ => $"{clusterSize / (1024 * 1024)} MB"
        };
    }

    private static List<int> CreateClusterSizes(int min, int max)
    {
        var clusterSizes = new List<int>();
        for (var clusterSize = min; clusterSize <= max; clusterSize *= 2)
        {
            clusterSizes.Add(clusterSize);
        }

        return clusterSizes;
    }
}
