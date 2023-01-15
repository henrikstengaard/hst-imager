namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using Models.FileSystems;

public class EntriesInfo
{
    public string Path { get; set; }
    public IEnumerable<Entry> Entries { get; set; }
    public bool Recursive { get; set; }
}