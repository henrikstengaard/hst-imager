using System.Collections.Generic;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// Planned changes to partition table of a physical disk or image file applied by imaging service.
/// </summary>
public class PartitionPlan
{
    /// <summary>
    /// Path to disk or PiStorm disk (mbr partition with rigid disk block) to partition.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Path is the whole disk, not a part of it like a PiStorm disk.
    /// </summary>
    public bool IsDiskPath { get; set; } = true;

    public bool Byteswap { get; set; }

    /// <summary>
    /// Start offset of master boot record partition added by a preceding plan, which contains PiStorm disk to
    /// partition. Path is the disk and partition number of master boot record partition is read, when plan is applied.
    /// </summary>
    public long? ContainerStartOffset { get; set; }

    public PartitionTableType TableType { get; set; }

    /// <summary>
    /// Initialize a new empty partition table, which erases existing partition tables.
    /// </summary>
    public bool Initialize { get; set; }

    /// <summary>
    /// Keep existing master boot record, when initializing rigid disk block to create a hybrid disk.
    /// Only sectors used by rigid disk block are erased.
    /// </summary>
    public bool KeepMasterBootRecord { get; set; }

    /// <summary>
    /// Sector rigid disk block is written to, when initializing rigid disk block (0-15).
    /// </summary>
    public int RdbBlockLo { get; set; }

    /// <summary>
    /// Size of rigid disk block, when initializing rigid disk block. 0 uses whole disk.
    /// </summary>
    public long RdbSize { get; set; }

    public long DiskSize { get; set; }

    /// <summary>
    /// Cylinder size in bytes for rigid disk block used to calculate start cylinder of partitions.
    /// </summary>
    public long CylinderSize { get; set; }

    /// <summary>
    /// Numbers of existing partitions to delete.
    /// </summary>
    public List<int> DeletePartitionNumbers { get; set; } = [];

    /// <summary>
    /// New partitions to add and format.
    /// </summary>
    public List<PlannedPartition> AddPartitions { get; set; } = [];

    /// <summary>
    /// Existing partitions to format.
    /// </summary>
    public List<PlannedPartition> FormatPartitions { get; set; } = [];

    /// <summary>
    /// Path or url to media with pfs3aio file system for PFS\3 and PDS\3 partitions in rigid disk block.
    /// </summary>
    public string? Pfs3FileSystemPath { get; set; }

    /// <summary>
    /// Path to media with FastFileSystem for DOS\3 and DOS\7 partitions in rigid disk block.
    /// </summary>
    public string? FastFileSystemPath { get; set; }

    public bool UseExperimental { get; set; }

    public bool HasChanges => Initialize || DeletePartitionNumbers.Count > 0 || AddPartitions.Count > 0 ||
                              FormatPartitions.Count > 0;
}

public class PlannedPartition
{
    public long StartOffset { get; set; }
    public long Size { get; set; }

    /// <summary>
    /// File system: fat32, exfat, ntfs for master boot record and guid partition table.
    /// pds3, pfs3, dos3, dos7 for rigid disk block.
    /// </summary>
    public string FileSystem { get; set; } = string.Empty;

    /// <summary>
    /// Volume name.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Device name for rigid disk block partitions, e.g. DH0.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Active for master boot record and bootable for rigid disk block partitions.
    /// </summary>
    public bool Bootable { get; set; }

    /// <summary>
    /// Master boot record partition is a PiStorm partition with bios type 0x76 containing a rigid disk block,
    /// which is not formatted.
    /// </summary>
    public bool IsPiStorm { get; set; }
}
