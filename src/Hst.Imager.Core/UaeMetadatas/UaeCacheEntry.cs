using System;

namespace Hst.Imager.Core.UaeMetadatas;

public class UaeCacheEntry
{
    public string EntryPathComponent { get; set; }
    public string UaeEntryPath { get; set; }
}

public class UaeMetadataEntry
{
    public string[] PathComponents { get; set; }
    public string[] UaePathComponents { get; set; }
    public DateTime Date { get; set; }
    public int ProtectionBits { get; set; }
    public string Comment { get; set; }
}