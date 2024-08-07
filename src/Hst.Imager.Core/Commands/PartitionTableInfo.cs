﻿namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;

    public class PartitionTableInfo
    {
        public DiskGeometryInfo DiskGeometry { get; set; }
        public long Size { get; set; }
        public long Sectors { get; set; }
        public long Cylinders { get; set; }
        public PartitionTableType Type { get; set; }
        public IEnumerable<PartitionInfo> Partitions { get; set; }
        public PartitionTableReservedInfo Reserved { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public long StartSector { get; set; }
        public long EndSector { get; set; }
        public long StartCylinder { get; set; }
        public long EndCylinder { get; set; }
    }
}