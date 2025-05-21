using System;

namespace Hst.Imager.Core.FileSystems.Fat32;

public class Fat32Entry
{
    public string Name { get; set; }
    public byte Attribute { get; set; }
    public DateTime CreationDate { get; set; }
}