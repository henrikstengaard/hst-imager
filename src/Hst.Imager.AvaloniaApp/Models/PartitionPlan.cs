using System.Collections.Generic;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// Planned changes to partition table of a physical disk or image file applied by imaging service.
/// </summary>
public class PartitionPlan
{
    /// <summary>
    /// Path to disk or PiStorm disk (mbr or gpt partition with rigid disk block) to partition.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Path is the whole disk, not a part of it like a PiStorm disk.
    /// </summary>
    public bool IsDiskPath { get; set; } = true;

    public bool Byteswap { get; set; }

    /// <summary>
    /// Start offset of master boot record or guid partition table partition added by a preceding plan, which contains
    /// PiStorm disk to partition. Path is the disk and partition number of partition is read from partition table of
    /// container table type, when plan is applied.
    /// </summary>
    public long? ContainerStartOffset { get; set; }

    /// <summary>
    /// Partition table type of partition containing PiStorm disk to partition, e.g. master boot record. None for disk.
    /// </summary>
    public PartitionTableType ContainerTableType { get; set; }

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

    /// <summary>
    /// New size of existing rigid disk block to resize to, e.g. after an image file is written to a larger disk.
    /// Rigid disk block is resized after partitions are deleted and before partitions are added. Null, if rigid disk
    /// block isn't resized.
    /// </summary>
    public long? ResizeRdbSize { get; set; }

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
    /// Existing rigid disk block partitions to update properties of, e.g. device name, buffers or max transfer.
    /// </summary>
    public List<PlannedPartitionUpdate> UpdatePartitions { get; set; } = [];

    /// <summary>
    /// Path or url to media with pfs3aio file system for PFS\3 and PDS\3 partitions in rigid disk block.
    /// </summary>
    public string? Pfs3FileSystemPath { get; set; }

    /// <summary>
    /// Path to media with FastFileSystem for DOS\3 and DOS\7 partitions in rigid disk block.
    /// </summary>
    public string? FastFileSystemPath { get; set; }

    public bool UseExperimental { get; set; }

    /// <summary>
    /// Existing file systems in rigid disk block to update.
    /// </summary>
    public List<PlannedFileSystemUpdate> UpdateFileSystems { get; set; } = [];

    /// <summary>
    /// Numbers of existing file systems in rigid disk block to delete.
    /// </summary>
    public List<int> DeleteFileSystemNumbers { get; set; } = [];

    /// <summary>
    /// New file systems to add to or import to rigid disk block.
    /// </summary>
    public List<PlannedFileSystem> AddFileSystems { get; set; } = [];

    public bool HasChanges => Initialize || ResizeRdbSize.HasValue || DeletePartitionNumbers.Count > 0 || AddPartitions.Count > 0 ||
                              FormatPartitions.Count > 0 || UpdatePartitions.Count > 0 || UpdateFileSystems.Count > 0 ||
                              DeleteFileSystemNumbers.Count > 0 || AddFileSystems.Count > 0;
}

/// <summary>
/// Update of existing file system in rigid disk block. Properties not set are not changed.
/// </summary>
public class PlannedFileSystemUpdate
{
    public int Number { get; set; }

    /// <summary>
    /// Dos type, e.g. PDS3. Dos type of partitions using file system is also updated.
    /// </summary>
    public string? DosType { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// Path to file system file to replace data of file system with.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Version of file system (number before . in version).
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// Revision of file system (number after . in version).
    /// </summary>
    public int? Revision { get; set; }
}

/// <summary>
/// New file system added from a file system file or imported from media like lha, adf or iso.
/// </summary>
public class PlannedFileSystem
{
    /// <summary>
    /// Path to file system file or path or url to media to import file system from.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    public string DosType { get; set; } = string.Empty;

    /// <summary>
    /// Name of file system, which is also used to find file system in media.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// File system is imported from media instead of added from a file system file.
    /// </summary>
    public bool IsImport { get; set; }

    /// <summary>
    /// Number of existing file system in rigid disk block to clone. File system is exported before file systems are
    /// updated or deleted and added from exported file.
    /// </summary>
    public int? CloneNumber { get; set; }

    /// <summary>
    /// Version of file system, which is used when adding a file system file without a version string and set after
    /// file system is added or imported.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// Revision of file system, which is used when adding a file system file without a version string and set after
    /// file system is added or imported.
    /// </summary>
    public int? Revision { get; set; }
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
    /// Partition type of new partition, bios type for master boot record, e.g. 0x0c, and guid for guid partition
    /// table. Default partition type of file system is used, if empty.
    /// </summary>
    public string PartitionType { get; set; } = string.Empty;

    /// <summary>
    /// Partition is formatted with file system after it's added. New partitions with a partition type, which isn't
    /// formatted, like a Linux partition or a SmartFileSystem partition, are only added.
    /// </summary>
    public bool Format { get; set; } = true;

    /// <summary>
    /// Master boot record partition with bios type 0x76 or guid partition table partition with partition type guid
    /// 3F82EEBC-87C9-4097-8165-89D6540557C0 is a PiStorm partition containing a rigid disk block, which is not
    /// formatted.
    /// </summary>
    public bool IsPiStorm { get; set; }

    /// <summary>
    /// Properties of new rigid disk block partitions, e.g. buffers, max transfer and file system block size.
    /// </summary>
    public RdbPartitionProperties RdbProperties { get; set; } = RdbPartitionProperties.Default;
}

/// <summary>
/// Update of existing rigid disk block partition. Properties not set are not changed.
/// </summary>
public class PlannedPartitionUpdate
{
    /// <summary>
    /// Number of partition before partitions are deleted.
    /// </summary>
    public int Number { get; set; }

    public string? DeviceName { get; set; }
    public bool? Bootable { get; set; }
    public int? BootPriority { get; set; }
    public bool? NoMount { get; set; }
    public uint? Buffers { get; set; }
    public uint? MaxTransfer { get; set; }
    public uint? Mask { get; set; }
    public uint? Reserved { get; set; }
    public uint? PreAlloc { get; set; }
    public int? FileSystemBlockSize { get; set; }
}

/// <summary>
/// Properties of rigid disk block partition stored in its partition block.
/// </summary>
/// <param name="Buffers">Number of buffers used by file system.</param>
/// <param name="MaxTransfer">Max number of bytes transferred by device at a time.</param>
/// <param name="Mask">Address mask of memory device can transfer to with dma.</param>
/// <param name="BootPriority">Boot priority of bootable partitions.</param>
/// <param name="NoMount">Partition isn't mounted on boot.</param>
/// <param name="Reserved">Blocks reserved at start of partition.</param>
/// <param name="PreAlloc">Blocks reserved at end of partition.</param>
/// <param name="FileSystemBlockSize">Block size of file system in bytes.</param>
public record RdbPartitionProperties(uint Buffers, uint MaxTransfer, uint Mask, int BootPriority, bool NoMount,
    uint Reserved, uint PreAlloc, int FileSystemBlockSize)
{
    /// <summary>
    /// Default properties of new partitions, same as partition block defaults.
    /// </summary>
    public static readonly RdbPartitionProperties Default = new(30, 0x1fe00, 0x7ffffffe, 0, false, 2, 0, 512);
}
