namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Result of examining a volume (partition or whole disk) for free and used sectors.
/// </summary>
public class VolumeAllocation
{
    /// <summary>
    /// Name of volume, e.g. "Rigid Disk Block partition DH0".
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Absolute start offset of volume in bytes.
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Size of volume in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// File system allocation, null if file system is unknown or couldn't be read.
    /// </summary>
    public FileSystemAllocation FileSystemAllocation { get; init; }

    /// <summary>
    /// Reason file system allocation couldn't be determined.
    /// </summary>
    public string Message { get; init; }

    public bool IsKnownFileSystem => FileSystemAllocation != null;
}
