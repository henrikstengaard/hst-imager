namespace Hst.Imager.Core.Models.FileSystems;

using System;
using System.Collections.Generic;

public class Entry
{
    public string Name { get; set; }
    public string FormattedName { get; set; }
    /// <summary>
    /// raw path for internal use
    /// </summary>
    public string RawPath { get; set; }
    /// <summary>
    /// Path to entry (dir and filename) split into components for os independency
    /// </summary>
    public string[] RelativePathComponents { get; set; }
    public string[] FullPathComponents { get; set; }
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