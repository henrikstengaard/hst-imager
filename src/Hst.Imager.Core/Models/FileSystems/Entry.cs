namespace Hst.Imager.Core.Models.FileSystems;

using System;

public class Entry
{
    public string Name { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public EntryType Type { get; set; }
    public DateTime? Date { get; set; }
    public string Attributes { get; set; }
}