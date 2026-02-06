using System;

namespace Hst.Imager.Core.UaeMetadatas;

public class UaeCacheEntry
{
    public string EntryPathComponent { get; set; }
    public string UaeEntryPath { get; set; }
    public string NormalEntryPath { get; set; }
}

public class UaeMetadataEntry
{
    public string DirPath { get; set; }
    
    /// <summary>
    /// Normal path components, where path component is normalized for the operating system,
    /// e.g. special characters and reserved names are replaced with underscores are prefixes.
    /// </summary>
    public string[] NormalPathComponents { get; set; }
    
    /// <summary>
    /// UAE path components, where path components may contain special characters and
    /// reserved names, as they are stored in UAE metadata.
    /// </summary>
    public string[] UaePathComponents { get; set; }
    public DateTime? Date { get; set; }
    public int? ProtectionBits { get; set; }
    public string Comment { get; set; }
    public bool UaeMetadataExists { get; set; }
}