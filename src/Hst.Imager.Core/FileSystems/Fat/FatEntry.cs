using System;

namespace Hst.Imager.Core.FileSystems.Fat;

public class FatEntry
{
    public string Name { get; set; }
    public byte Attribute { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime ModificationDate { get; set; }
    public ushort LowFirstCluster { get; set; }
    public ushort HighFirstCluster { get; set; }
    public uint Size { get; set; }
    public DateTime? LastAccessDate { get; set; }
}