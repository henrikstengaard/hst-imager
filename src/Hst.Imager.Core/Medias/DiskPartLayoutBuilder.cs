namespace Hst.Imager.Core.Medias
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Commands;
    using DiscUtils.Partitions;

    /// <summary>
    /// Builds partition table parts and disk parts layout including unallocated parts from partition table info.
    /// </summary>
    public static class DiskPartLayoutBuilder
    {
        public static IEnumerable<PartInfo> CreateDiskParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
        {
            var allocatedParts = new List<PartInfo>();
            if (diskInfo.GptPartitionTablePart != null)
            {
                allocatedParts.AddRange(
                    diskInfo.GptPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
            }

            if (diskInfo.MbrPartitionTablePart != null)
            {
                var mbrParts = diskInfo.MbrPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated)
                    .ToList();

                if (mbrParts.All(x => x.BiosType != BiosPartitionTypes.GptProtective.ToString()) &&
                    diskInfo.GptPartitionTablePart == null)
                {
                    allocatedParts.AddRange(mbrParts);
                }
            }

            if (diskInfo.RdbPartitionTablePart != null)
            {
                allocatedParts.AddRange(
                    diskInfo.RdbPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
            }

            // recalculate percent size against disk size for allocated parts
            foreach (var allocatedPart in allocatedParts)
            {
                allocatedPart.PercentSize = Math.Round(((double)100 / diskInfo.Size) * allocatedPart.Size);
            }

            return allocatedParts.Concat(CreateUnallocatedParts(diskInfo.Size, diskInfo.Size / 512, 0,
                partitionTableTypeContext, allocatedParts, true, false)).OrderBy(x => x.StartOffset).ToList();
        }

        public static PartitionTablePart CreatePartitionTablePart(
            DiskInfo diskInfo,
            PartitionTableInfo partitionTableInfo,
            PartitionTableType partitionTableTypeContext)
        {
            if (partitionTableInfo == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    PartitionType = partitionTableInfo.Type.ToString(),
                    FileSystem = string.Empty,
                    PartitionTableType = partitionTableInfo.Type,
                    PartType = PartType.PartitionTable,
                    Size = partitionTableInfo.Reserved.Size,
                    StartOffset = partitionTableInfo.Reserved.StartOffset,
                    EndOffset = partitionTableInfo.Reserved.EndOffset,
                    StartSector = partitionTableInfo.Reserved.StartSector,
                    EndSector = partitionTableInfo.Reserved.EndSector,
                    StartCylinder = partitionTableInfo.Reserved.StartCylinder,
                    EndCylinder = partitionTableInfo.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * partitionTableInfo.Reserved.Size)
                }
            }.Concat(partitionTableInfo.Partitions.Select(x => new PartInfo
            {
                PartitionType = x.PartitionType,
                FileSystem = x.FileSystem,
                Size = x.Size,
                VolumeSize = x.VolumeSize,
                VolumeFree = x.VolumeFree,
                PartitionTableType = partitionTableInfo.Type,
                PartType = PartType.Partition,
                BiosType = x.BiosType?.ToString() ?? string.Empty,
                GuidType = x.GuidType?.ToString() ?? string.Empty,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size),
                IsActive = x.IsActive,
                IsPrimary = x.IsPrimary,
                StartChs = x.StartChs != null ? new ChsAddress
                {
                    Cylinder = x.StartChs.Cylinder,
                    Head = x.StartChs.Head,
                    Sector = x.StartChs.Sector
                } : null,
                EndChs = x.EndChs != null ? new ChsAddress
                {
                    Cylinder = x.EndChs.Cylinder,
                    Head = x.EndChs.Head,
                    Sector = x.EndChs.Sector
                } : null
            })).ToList();

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = partitionTableInfo.Type,
                DiskGeometry = partitionTableInfo.DiskGeometry != null ? new DiskGeometry
                {
                    Capacity = partitionTableInfo.DiskGeometry.Capacity,
                    TotalSectors = partitionTableInfo.DiskGeometry.TotalSectors,
                    BytesPerSector = partitionTableInfo.DiskGeometry.BytesPerSector,
                    HeadsPerCylinder = partitionTableInfo.DiskGeometry.HeadsPerCylinder,
                    Cylinders = partitionTableInfo.DiskGeometry.Cylinders,
                    SectorsPerTrack = partitionTableInfo.DiskGeometry.SectorsPerTrack
                } : null,
                Size = partitionTableInfo.Size,
                Sectors = partitionTableInfo.Sectors,
                Cylinders = 0,
                Parts = parts.Concat(CreateUnallocatedParts(partitionTableInfo.Size, partitionTableInfo.Sectors, 0,
                    partitionTableTypeContext, parts, true, false)).OrderBy(x => x.StartOffset).ToList()
            };
        }

        public static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
            PartitionTableType partitionTableTypeContext, IEnumerable<PartInfo> parts, bool useSectors,
            bool useCylinders)
        {
            if (diskSize <= 0)
            {
                yield break;
            }

            if (partitionTableTypeContext == PartitionTableType.GuidPartitionTable)
            {
                parts = parts.Where(x => x.BiosType != BiosPartitionTypes.GptProtective.ToString());
            }

            var orderedParts = parts.OrderBy(x => x.StartOffset);
            var overlappingParts = MergeOverlappingParts(orderedParts).ToList();
            var unallocatedParts = new List<PartInfo>();

            var offset = 0L;
            var sector = 0L;
            var cylinder = 0L;
            foreach (var overlappingPart in overlappingParts)
            {
                if (overlappingPart.StartOffset > offset)
                {
                    var unallocatedSize = overlappingPart.StartOffset - offset;
                    yield return new PartInfo
                    {
                        PartitionType = PartType.Unallocated.ToString(),
                        FileSystem = string.Empty,
                        PartitionTableType = PartitionTableType.None,
                        PartType = PartType.Unallocated,
                        Size = unallocatedSize,
                        StartOffset = offset,
                        EndOffset = overlappingPart.StartOffset - 1,
                        StartSector = overlappingPart.StartSector == 0 ? 0 : sector,
                        EndSector = overlappingPart.StartSector == 0 ? 0 : overlappingPart.StartSector - 1,
                        StartCylinder = overlappingPart.StartCylinder == 0 ? 0 : cylinder,
                        EndCylinder = overlappingPart.StartCylinder == 0 ? 0 : overlappingPart.StartCylinder - 1,
                        PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                    };
                }

                offset = overlappingPart.EndOffset + 1;
                sector = useSectors ? overlappingPart.EndSector + 1 : 0;
                cylinder = useCylinders ? overlappingPart.EndCylinder + 1 : 0;
            }

            if (offset < diskSize)
            {
                var unallocatedSize = diskSize - offset;
                yield return new PartInfo
                {
                    PartitionType = PartType.Unallocated.ToString(),
                    FileSystem = string.Empty,
                    PartitionTableType = PartitionTableType.None,
                    PartType = PartType.Unallocated,
                    Size = unallocatedSize,
                    StartOffset = offset,
                    EndOffset = diskSize - 1,
                    StartSector = sectors == 0 ? 0 : sector,
                    EndSector = sectors == 0 ? 0 : sectors - 1,
                    StartCylinder = cylinders == 0 ? 0 : cylinder,
                    EndCylinder = cylinders == 0 ? 0 : cylinders - 1,
                    PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                };
            }
        }

        private static IEnumerable<PartInfo> MergeOverlappingParts(IEnumerable<PartInfo> parts)
        {
            var mergedParts = new List<PartInfo>();

            PartInfo currentPart = null;
            foreach (var part in parts)
            {
                if (currentPart == null)
                {
                    currentPart = part;
                    continue;
                }

                if (!IsOverlapping(part.StartOffset, part.EndOffset, currentPart.StartOffset,
                        currentPart.EndOffset))
                {
                    mergedParts.Add(currentPart);
                    currentPart = part;
                    continue;
                }

                currentPart = new PartInfo
                {
                    StartOffset = Math.Min(part.StartOffset, currentPart.StartOffset),
                    EndOffset = Math.Max(part.EndOffset, currentPart.EndOffset),
                    StartSector = Math.Min(part.StartSector, currentPart.StartSector),
                    EndSector = Math.Max(part.EndSector, currentPart.EndSector),
                    StartCylinder = Math.Min(part.StartCylinder, currentPart.StartCylinder),
                    EndCylinder = Math.Max(part.EndCylinder, currentPart.EndCylinder)
                };
                currentPart.Size = currentPart.EndOffset - currentPart.StartOffset + 1;
            }

            if (currentPart != null)
            {
                mergedParts.Add(currentPart);
            }

            return mergedParts;
        }

        private static bool IsOverlapping(long start1, long end1, long start2, long end2)
        {
            return (start1 <= end2) && (start2 <= end1);
        }
    }
}
