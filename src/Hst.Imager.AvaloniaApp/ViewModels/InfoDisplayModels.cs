using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

// ─── Details base ────────────────────────────────────────────────────────

public abstract class DetailSectionBase : ReactiveObject
{
    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set => this.RaiseAndSetIfChanged(ref _isExpanded, value); }
    public string Title { get; set; } = string.Empty;
    protected DetailSectionBase(bool expanded = true) => _isExpanded = expanded;
}

public record PropertyRow(string Name, string Value);

// ─── Disk info ────────────────────────────────────────────────────────────────

public class DiskInfoDetailSection : DetailSectionBase
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool IsSparseFile { get; set; }
    public string SparseFileSize { get; set; } = string.Empty;

    public IReadOnlyList<PropertyRow> Rows
    {
        get
        {
            var rows = new List<PropertyRow>
            {
                new("Name", Name),
                new("Path", Path),
                new("Size", Size)
            };
            if (IsSparseFile)
            {
                rows.Add(new("Sparse file size", SparseFileSize));
            }
            return rows;
        }
    }
}

// ─── Geometry (MBR / GPT) ─────────────────────────────────────────────────────

public class GeometryDetailSection : DetailSectionBase
{
    public string Capacity { get; set; } = string.Empty;
    public string SectorSize { get; set; } = string.Empty;
    public string TotalSectors { get; set; } = string.Empty;
    public string Cylinders { get; set; } = string.Empty;
    public string HeadsPerCylinder { get; set; } = string.Empty;
    public string SectorsPerTrack { get; set; } = string.Empty;

    public IReadOnlyList<PropertyRow> Rows =>
    [
        new("Size", Capacity),
        new("Sector size", SectorSize),
        new("Total sectors", TotalSectors),
        new("Cylinders", Cylinders),
        new("Heads per cyl", HeadsPerCylinder),
        new("Sectors per track", SectorsPerTrack)
    ];
}

// ─── MBR partitions ───────────────────────────────────────────────────────────

public class MbrPartitionRow
{
    public string Number { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string StartSector { get; set; } = string.Empty;
    public string EndSector { get; set; } = string.Empty;
    public string Active { get; set; } = string.Empty;
    public string Primary { get; set; } = string.Empty;
}

public class MbrPartitionsDetailSection : DetailSectionBase
{
    public ObservableCollection<MbrPartitionRow> Rows { get; set; } = [];
}

// ─── GPT partitions ───────────────────────────────────────────────────────────

public class GptPartitionRow
{
    public string Number { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string StartSector { get; set; } = string.Empty;
    public string EndSector { get; set; } = string.Empty;
}

public class GptPartitionsDetailSection : DetailSectionBase
{
    public ObservableCollection<GptPartitionRow> Rows { get; set; } = [];
}

// ─── RDB header info ──────────────────────────────────────────────────────────

public class RdbInfoDetailSection : DetailSectionBase
{
    public string Product { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Cylinders { get; set; } = string.Empty;
    public string Heads { get; set; } = string.Empty;
    public string Sectors { get; set; } = string.Empty;
    public string BlockSize { get; set; } = string.Empty;
    public string StartCylinder { get; set; } = string.Empty;
    public string EndCylinder { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string RdbBlockLo { get; set; } = string.Empty;
    public string RdbBlockHi { get; set; } = string.Empty;

    public IReadOnlyList<PropertyRow> Rows =>
    [
        new("Product", Product),
        new("Vendor", Vendor),
        new("Revision", Revision),
        new("Size", Size),
        new("Cylinders", Cylinders),
        new("Heads", Heads),
        new("Sectors", Sectors),
        new("Block size", BlockSize),
        new("Start cylinder", StartCylinder),
        new("End cylinder", EndCylinder),
        new("Flags", Flags),
        new("Host id", HostId),
        new("RDB block lo", RdbBlockLo),
        new("RDB block hi", RdbBlockHi)
    ];
}

// ─── RDB file systems ─────────────────────────────────────────────────────────

public class RdbFileSystemRow
{
    public string Number { get; set; } = string.Empty;
    public string DosType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FileSystemName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
}

public class RdbFileSystemsDetailSection : DetailSectionBase
{
    public ObservableCollection<RdbFileSystemRow> Rows { get; set; } = [];
}

// ─── RDB partitions ───────────────────────────────────────────────────────────

public class RdbPartitionRow
{
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string StartCyl { get; set; } = string.Empty;
    public string EndCyl { get; set; } = string.Empty;
    public string TotalCyl { get; set; } = string.Empty;
    public string Heads { get; set; } = string.Empty;
    public string BlocksPerTrack { get; set; } = string.Empty;
    public string Buffers { get; set; } = string.Empty;
    public string FsBlockSize { get; set; } = string.Empty;
    public string Reserved { get; set; } = string.Empty;
    public string PreAlloc { get; set; } = string.Empty;
    public string Bootable { get; set; } = string.Empty;
    public string BootPriority { get; set; } = string.Empty;
    public string NoMount { get; set; } = string.Empty;
    public string DosType { get; set; } = string.Empty;
    public string Mask { get; set; } = string.Empty;
    public string MaxTransfer { get; set; } = string.Empty;
}

public class RdbPartitionsDetailSection : DetailSectionBase
{
    public ObservableCollection<RdbPartitionRow> Rows { get; set; } = [];
}
