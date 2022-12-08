namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using Models.FileSystems;

public class EntriesInfo
{
    public string DiskPath { get; set; }
    public string FileSystemPath { get; set; }
    public IEnumerable<Entry> Entries { get; set; }
}