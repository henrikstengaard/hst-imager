using System.Collections.Generic;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Result of examining a disk for free and used sectors.
/// </summary>
public class DiskAllocation
{
    public DiskAllocation(AllocationMap allocationMap, IReadOnlyList<VolumeAllocation> volumes)
    {
        AllocationMap = allocationMap;
        Volumes = volumes;
    }

    /// <summary>
    /// Map of free byte ranges for all volumes with known file systems.
    /// </summary>
    public AllocationMap AllocationMap { get; }

    /// <summary>
    /// Volumes examined.
    /// </summary>
    public IReadOnlyList<VolumeAllocation> Volumes { get; }
}
