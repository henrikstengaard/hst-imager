namespace Hst.Imager.Core.Models.FileSystems;

using System;
using System.Collections.Generic;

public class Entry
{
    public string Name { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public EntryType Type { get; set; }
    public DateTime? Date { get; set; }
    public IDictionary<string, string> Properties { get; set; }
    public string Attributes { get; set; }

    public Entry()
    {
        Properties = new Dictionary<string, string>();
    }
}