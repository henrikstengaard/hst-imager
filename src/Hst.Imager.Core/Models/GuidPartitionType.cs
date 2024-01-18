using System;

namespace Hst.Imager.Core.Models;

public record GuidPartitionType
{
    public Guid GuidType { get; set; }
    public string PartitionType { get; set; }
}