using System.Collections.Generic;
using System.Linq;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Free byte ranges of a file system determined by reading its allocation structures.
/// </summary>
public class FileSystemAllocation
{
    public FileSystemAllocation(string fileSystem, IReadOnlyList<ByteRange> freeRanges)
    {
        FileSystem = fileSystem;
        FreeRanges = freeRanges;
        FreeBytes = freeRanges.Sum(x => x.Length);
    }

    /// <summary>
    /// Name of file system, e.g. FAT32, PFS3 or DOS3.
    /// </summary>
    public string FileSystem { get; }

    /// <summary>
    /// Absolute free byte ranges.
    /// </summary>
    public IReadOnlyList<ByteRange> FreeRanges { get; }

    /// <summary>
    /// Total number of free bytes.
    /// </summary>
    public long FreeBytes { get; }
}
