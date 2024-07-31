using System;
using System.Collections.Generic;
using System.Linq;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.PartitionTables;

public static class DiskPartHelper
{
    public static IEnumerable<PartInfo> CreateDiskParts(DiskInfo diskInfo, PartitionTableType partitionTableTypeContext)
    {
        var allocatedParts = new List<PartInfo>();
        if (diskInfo.GptPartitionTablePart != null)
        {
            allocatedParts.AddRange(
                diskInfo.GptPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
        }

        if (diskInfo.MbrPartitionTablePart != null)
        {
            allocatedParts.AddRange(
                diskInfo.MbrPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
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

        return CreateUnallocatedParts(diskInfo.Size, diskInfo.Size / 512, 0,
            partitionTableTypeContext, allocatedParts, true, false);
    }

    public static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
        PartitionTableType partitionTableTypeContext, IEnumerable<PartInfo> parts, bool useSectors,
        bool useCylinders)
    {
        if (diskSize <= 0)
        {
            throw new ArgumentException($"Invalid disk size '{diskSize}'", nameof(diskSize));
        }
        
        parts = parts.OrderBy(x => x.StartOffset);
        var partsList = MergeOverlappingParts(parts).ToList();
        var unallocatedParts = new List<PartInfo>();

        var offset = 0L;
        var sector = 0L;
        var cylinder = 0L;
        foreach (var part in partsList)
        {
            if (part.StartOffset > offset)
            {
                var unallocatedSize = part.StartOffset - offset;
                unallocatedParts.Add(new PartInfo
                {
                    FileSystem = "Unallocated",
                    PartitionTableType = PartitionTableType.None,
                    PartType = PartType.Unallocated,
                    Size = unallocatedSize,
                    StartOffset = offset,
                    EndOffset = part.StartOffset - 1,
                    StartSector = part.StartSector == 0 ? 0 : sector,
                    EndSector = part.StartSector == 0 ? 0 : part.StartSector - 1,
                    StartCylinder = part.StartCylinder == 0 ? 0 : cylinder,
                    EndCylinder = part.StartCylinder == 0 ? 0 : part.StartCylinder - 1,
                    PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                });
            }

            offset = part.EndOffset + 1;
            sector = useSectors ? part.EndSector + 1 : 0;
            cylinder = useCylinders ? part.EndCylinder + 1 : 0;
        }

        if (offset < diskSize)
        {
            var unallocatedSize = diskSize - offset;
            unallocatedParts.Add(new PartInfo
            {
                FileSystem = "Unallocated",
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
            });
        }

        return partsList.Concat(unallocatedParts).OrderBy(x => x.StartOffset).ToList();
    }

    public static IEnumerable<PartInfo> MergeOverlappingParts(IEnumerable<PartInfo> parts)
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

            if (!ArePartsOverlapping(part, currentPart))
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

    private static bool ArePartsOverlapping(PartInfo part1, PartInfo part2)
    {
        return part1.PartType == PartType.Unallocated &&
               part2.PartType == PartType.Unallocated &&
               AreOffsetsOverlapping(part1.StartOffset, part1.EndOffset, 
                   part2.StartOffset, part2.EndOffset);
    }

    private static bool AreOffsetsOverlapping(long offset1Start, long offset1End, long offset2Start,
        long offset2End)
    {
        return (offset1Start <= offset2End) && (offset2Start <= offset1End);
    }
    
    private static long Next(long value, int size)
    {
        var left = value % size;

        return left == 0 ? value : value - left + size;
    }
}