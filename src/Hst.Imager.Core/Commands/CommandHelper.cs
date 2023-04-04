namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DiscUtils;
    using DiscUtils.Partitions;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Models;

    public class CommandHelper : ICommandHelper
    {
        private readonly bool isAdministrator;

        public CommandHelper(bool isAdministrator)
        {
            this.isAdministrator = isAdministrator;
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
        }

        public virtual Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            bool allowPhysicalDrive = true)
        {
            if (!isAdministrator && (Regexs.PhysicalDrivePathRegex.IsMatch(path) ||
                                     Regexs.DevicePathRegex.IsMatch(path)))
            {
                return new Result<Media>(new Error($"Path '{path}' requires administrator privileges"));
            }

            var physicalDrive =
                physicalDrives.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (!allowPhysicalDrive && physicalDrive != null)
            {
                return new Result<Media>(new Error("Physical drive is not allowed"));
            }

            if (physicalDrive != null)
            {
                return new Result<Media>(new Media(path, physicalDrive.Name, physicalDrive.Size, Media.MediaType.Raw,
                    true, physicalDrive.Open()));
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path ?? "null"}' not found", nameof(path)));
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path ?? "null"}' not found", nameof(path)));
            }

            var name = Path.GetFileName(path);
            if (!IsVhd(path))
            {
                var imgStream = File.Open(path, FileMode.Open, FileAccess.Read);
                return new Result<Media>(new Media(path, name, imgStream.Length, Media.MediaType.Raw, false,
                    imgStream));
            }

            var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.Read);
            vhdDisk.Content.Position = 0;
            return new Result<Media>(new VhdMedia(path, name, vhdDisk.Capacity, Media.MediaType.Vhd, false, vhdDisk, new SectorStream(vhdDisk.Content, true)));
        }

        public virtual Stream CreateWriteableStream(string path)
        {
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            long? size = null, bool allowPhysicalDrive = true, bool create = false)
        {
            var physicalDrive =
                physicalDrives.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (!allowPhysicalDrive && physicalDrive != null)
            {
                return new Result<Media>(new Error("Physical drive is not allowed"));
            }

            if (physicalDrive != null)
            {
                physicalDrive.SetWritable(true);
                return new Result<Media>(new Media(path, physicalDrive.Name, physicalDrive.Size, Media.MediaType.Raw,
                    true,
                    physicalDrive.Open()));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(path);
            }

            var destDir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var name = Path.GetFileName(path);

            if (create)
            {
                if (!IsVhd(path))
                {
                    var imgStream = CreateWriteableStream(path);
                    return new Result<Media>(new Media(path, name, imgStream.Length, Media.MediaType.Raw, false,
                        imgStream));
                }

                if (size == null || size.Value == 0)
                {
                    throw new ArgumentNullException(nameof(size), "Size is required for creating VHD image file");
                }

                using var vhdStream = CreateWriteableStream(path);
                using var newVhdDisk = Disk.InitializeDynamic(vhdStream, Ownership.None, GetVhdSize(size.Value));
            }

            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path}' not found", nameof(path)));
            }

            if (!IsVhd(path))
            {
                var imgStream = CreateWriteableStream(path);
                return new Result<Media>(new Media(path, name, imgStream.Length, Media.MediaType.Raw, false,
                    imgStream));
            }

            var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            vhdDisk.Content.Position = 0;
            return new Result<Media>(new VhdMedia(path, name, vhdDisk.Capacity, Media.MediaType.Vhd, false,
                vhdDisk, new SectorStream(vhdDisk.Content, true)));
        }

        public virtual long GetVhdSize(long size)
        {
            // vhd size dividable by 512
            return size % 512 != 0 ? size - (size % 512) : size;
        }

        public bool IsVhd(string path)
        {
            return path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase);
        }

        public virtual async Task<RigidDiskBlock> GetRigidDiskBlock(Stream stream)
        {
            return await RigidDiskBlockReader.Read(stream);
        }

        public virtual async Task<DiskInfo> ReadDiskInfo(Media media, Stream stream)
        {
            var partitionTables = new List<PartitionTableInfo>();

            var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
            
            try
            {
                var biosPartitionTable = new BiosPartitionTable(disk);

                var mbrPartitionNumber = 0;

                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableType.MasterBootRecord,
                    Size = disk.Capacity,
                    Sectors = disk.Capacity / 512,
                    Cylinders = 0,
                    Partitions = biosPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++mbrPartitionNumber,
                        FileSystem = x.TypeAsString,
                        Size = x.SectorCount * disk.BlockSize,
                        StartOffset = x.FirstSector * disk.BlockSize,
                        EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1,
                        StartSector = x.FirstSector,
                        EndSector = x.LastSector,
                        StartCylinder = 0,
                        EndCylinder = 0
                    }).ToList(),
                    Reserved = new PartitionTableReservedInfo
                    {
                        StartOffset = 0,
                        EndOffset = 511,
                        StartSector = 0,
                        EndSector = 1,
                        StartCylinder = 0,
                        EndCylinder = 0,
                        Size = 512
                    },
                    StartOffset = 0,
                    EndOffset = disk.Capacity - 1
                });
            }
            catch (Exception)
            {
                // ignored, if read bios partition table fails
            }

            try
            {
                var guidPartitionTable = new GuidPartitionTable(disk);

                var guidPartitionNumber = 0;

                var guidReservedSize = guidPartitionTable.FirstUsableSector * disk.BlockSize;
                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableType.GuidPartitionTable,
                    Size = disk.Capacity,
                    Sectors = guidPartitionTable.LastUsableSector + 1,
                    Cylinders = 0,
                    Partitions = guidPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++guidPartitionNumber,
                        FileSystem = x.TypeAsString,
                        Size = x.SectorCount * disk.BlockSize,
                        StartOffset = x.FirstSector * disk.BlockSize,
                        EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1,
                        StartSector = x.FirstSector,
                        EndSector = x.LastSector,
                        StartCylinder = 0,
                        EndCylinder = 0
                    }).ToList(),
                    Reserved = new PartitionTableReservedInfo
                    {
                        StartOffset = 0,
                        EndOffset = guidReservedSize - 1,
                        StartSector = 0,
                        EndSector = guidPartitionTable.FirstUsableSector > 0
                            ? guidPartitionTable.FirstUsableSector - 1
                            : 0,
                        StartCylinder = 0,
                        EndCylinder = 0,
                        Size = guidReservedSize
                    },
                    StartOffset = 0,
                    EndOffset = disk.Capacity,
                    StartSector = guidPartitionTable.FirstUsableSector,
                    EndSector = guidPartitionTable.LastUsableSector,
                    StartCylinder = 0,
                    EndCylinder = 0
                });
            }
            catch (Exception)
            {
                // ignored, if read guid partition table fails
            }

            RigidDiskBlock rigidDiskBlock = null;
            try
            {
                rigidDiskBlock = await GetRigidDiskBlock(stream);
                if (rigidDiskBlock != null)
                {
                    var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
                    var rdbPartitionNumber = 0;

                    var rdbStartCyl = 0;
                    var rdbEndCyl = Next(rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize,
                        (int)cylinderSize) - 1;

                    var rdbStartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize;
                    var rdbEndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1;
                    partitionTables.Add(new PartitionTableInfo
                    {
                        Type = PartitionTableType.RigidDiskBlock,
                        Size = rigidDiskBlock.DiskSize,
                        Sectors = rigidDiskBlock.Sectors,
                        Cylinders = rigidDiskBlock.Cylinders,
                        Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
                        {
                            PartitionNumber = ++rdbPartitionNumber,
                            FileSystem = x.DosTypeFormatted,
                            Size = x.PartitionSize,
                            StartOffset = (long)x.LowCyl * cylinderSize,
                            EndOffset = ((long)x.HighCyl + 1) * cylinderSize - 1,
                            StartSector = (long)x.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                            EndSector = (long)x.HighCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                            StartCylinder = x.LowCyl,
                            EndCylinder = x.HighCyl,
                        }).ToList(),
                        Reserved = new PartitionTableReservedInfo
                        {
                            StartOffset = rdbStartOffset,
                            EndOffset = rdbEndOffset,
                            StartSector = rigidDiskBlock.RdbBlockLo,
                            EndSector = rigidDiskBlock.RdbBlockHi,
                            StartCylinder = rdbStartCyl,
                            EndCylinder = rdbEndCyl,
                            Size = rdbEndOffset - rdbStartOffset + 1
                        },
                        StartOffset = rdbStartOffset,
                        EndOffset = rdbStartOffset + rigidDiskBlock.DiskSize - 1
                    });
                }
            }
            catch (Exception)
            {
                // ignored, if read rigid disk block fails
            }

            var diskInfo = new DiskInfo
            {
                Path = media.Path,
                Name = media.Model,
                Size = media.Size,
                PartitionTables = partitionTables,
                StartOffset = 0,
                EndOffset = media.Size > 0 ? media.Size - 1 : 0,
                RigidDiskBlock = rigidDiskBlock,
            };

            diskInfo.GptPartitionTablePart = CreateGptParts(diskInfo);
            diskInfo.MbrPartitionTablePart = CreateMbrParts(diskInfo);
            diskInfo.RdbPartitionTablePart = CreateRdbParts(diskInfo);
            diskInfo.DiskParts = CreateDiskParts(diskInfo);

            return diskInfo;
        }

        private static IEnumerable<PartInfo> CreateDiskParts(DiskInfo diskInfo)
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

            return CreateUnallocatedParts(diskInfo.Size, 0, 0, allocatedParts);
        }

        private static PartitionTablePart CreateGptParts(DiskInfo diskInfo)
        {
            var gptPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.GuidPartitionTable);

            if (gptPartitionTable == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    FileSystem = "Reserved",
                    PartitionTableType = PartitionTableType.MasterBootRecord,
                    PartType = PartType.PartitionTable,
                    Size = gptPartitionTable.Reserved.Size,
                    StartOffset = gptPartitionTable.Reserved.StartOffset,
                    EndOffset = gptPartitionTable.Reserved.EndOffset,
                    StartSector = gptPartitionTable.Reserved.StartSector,
                    EndSector = gptPartitionTable.Reserved.EndSector,
                    StartCylinder = gptPartitionTable.Reserved.StartCylinder,
                    EndCylinder = gptPartitionTable.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * gptPartitionTable.Reserved.Size)
                }
            }.Concat(gptPartitionTable.Partitions.Select(x => new PartInfo
            {
                FileSystem = x.FileSystem,
                Size = x.Size,
                PartitionTableType = gptPartitionTable.Type,
                PartType = PartType.Partition,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size)
            }));

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = gptPartitionTable.Type,
                Size = diskInfo.Size,
                Sectors = 0,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(gptPartitionTable.Size, gptPartitionTable.Sectors, 0, parts)
            };
        }

        private static PartitionTablePart CreateMbrParts(DiskInfo diskInfo)
        {
            var mbrPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.MasterBootRecord);

            if (mbrPartitionTable == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    FileSystem = "Reserved",
                    PartitionTableType = PartitionTableType.MasterBootRecord,
                    PartType = PartType.PartitionTable,
                    Size = mbrPartitionTable.Reserved.Size,
                    StartOffset = mbrPartitionTable.Reserved.StartOffset,
                    EndOffset = mbrPartitionTable.Reserved.EndOffset,
                    StartSector = mbrPartitionTable.Reserved.StartSector,
                    EndSector = mbrPartitionTable.Reserved.EndSector,
                    StartCylinder = mbrPartitionTable.Reserved.StartCylinder,
                    EndCylinder = mbrPartitionTable.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * mbrPartitionTable.Reserved.Size)
                }
            }.Concat(mbrPartitionTable.Partitions.Select(x => new PartInfo
            {
                FileSystem = x.FileSystem,
                Size = x.Size,
                PartitionTableType = mbrPartitionTable.Type,
                PartType = PartType.Partition,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size)
            }));

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = mbrPartitionTable.Type,
                Size = diskInfo.Size,
                Sectors = mbrPartitionTable.Sectors,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(mbrPartitionTable.Size, mbrPartitionTable.Sectors, 0, parts)
            };
        }

        private static PartitionTablePart CreateRdbParts(DiskInfo diskInfo)
        {
            var parts = new List<PartInfo>();
            if (diskInfo.RigidDiskBlock == null)
            {
                return null;
            }

            var cylinderSize = diskInfo.RigidDiskBlock.Heads * diskInfo.RigidDiskBlock.Sectors *
                               diskInfo.RigidDiskBlock.BlockSize;

            var rdbStartCyl = 0;
            var rdbEndCyl = (Next(diskInfo.RigidDiskBlock.RdbBlockHi * diskInfo.RigidDiskBlock.BlockSize,
                (int)cylinderSize) / cylinderSize) - 1;
            var rdbSize = (diskInfo.RigidDiskBlock.RdbBlockHi - diskInfo.RigidDiskBlock.RdbBlockLo + 1) *
                          diskInfo.RigidDiskBlock.BlockSize;

            parts.Add(new PartInfo
            {
                FileSystem = "Reserved",
                PartitionTableType = PartitionTableType.RigidDiskBlock,
                PartType = PartType.PartitionTable,
                Size = rdbSize,
                StartOffset = diskInfo.RigidDiskBlock.RdbBlockLo * diskInfo.RigidDiskBlock.BlockSize,
                EndOffset = ((diskInfo.RigidDiskBlock.RdbBlockHi + 1) * diskInfo.RigidDiskBlock.BlockSize) - 1,
                StartSector = diskInfo.RigidDiskBlock.RdbBlockLo,
                EndSector = diskInfo.RigidDiskBlock.RdbBlockHi,
                StartCylinder = rdbStartCyl,
                EndCylinder = rdbEndCyl,
                PercentSize = Math.Round(((double)100 / diskInfo.RigidDiskBlock.DiskSize) * rdbSize)
            });

            var partitionNumber = 0;
            foreach (var partitionBlock in diskInfo.RigidDiskBlock.PartitionBlocks.OrderBy(x => x.LowCyl).ToList())
            {
                parts.Add(new PartInfo
                {
                    FileSystem = partitionBlock.DosTypeFormatted,
                    PartitionTableType = PartitionTableType.RigidDiskBlock,
                    PartType = PartType.Partition,
                    PartitionNumber = ++partitionNumber,
                    Size = partitionBlock.PartitionSize,
                    StartOffset = (long)partitionBlock.LowCyl * cylinderSize,
                    EndOffset = ((long)partitionBlock.HighCyl + 1) * cylinderSize - 1,
                    StartSector = (long)partitionBlock.LowCyl * diskInfo.RigidDiskBlock.Heads *
                                  diskInfo.RigidDiskBlock.Sectors,
                    EndSector = (((long)partitionBlock.HighCyl + 1) * diskInfo.RigidDiskBlock.Heads *
                                 diskInfo.RigidDiskBlock.Sectors) - 1,
                    StartCylinder = partitionBlock.LowCyl,
                    EndCylinder = partitionBlock.HighCyl,
                    PercentSize = Math.Round(((double)100 / diskInfo.RigidDiskBlock.DiskSize) *
                                             partitionBlock.PartitionSize)
                });
            }

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = PartitionTableType.RigidDiskBlock,
                Size = diskInfo.RigidDiskBlock.DiskSize,
                Sectors = diskInfo.RigidDiskBlock.Sectors,
                Cylinders = diskInfo.RigidDiskBlock.Cylinders,
                Parts = CreateUnallocatedParts(diskInfo.RigidDiskBlock.DiskSize, diskInfo.RigidDiskBlock.Sectors, 0,
                    parts)
            };
        }

        private static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
            IEnumerable<PartInfo> parts)
        {
            if (diskSize <= 0)
            {
                throw new ArgumentException($"Invalid disk size '{diskSize}'", nameof(diskSize));
            }

            var partsList = parts.ToList();
            var unallocatedParts = new List<PartInfo>();

            var offset = 0L;
            var sector = 0L;
            var cylinder = 0L;
            foreach (var part in partsList.OrderBy(x => x.StartOffset).ToList())
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
                        StartSector = part.StartOffset == 0 ? 0 : sector,
                        EndSector = part.StartOffset == 0 ? 0 : part.StartSector - 1,
                        StartCylinder = part.StartCylinder == 0 ? 0 : cylinder,
                        EndCylinder = part.StartCylinder == 0 ? 0 : part.StartCylinder - 1,
                        PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                    });
                }

                offset = part.EndOffset == 0 ? 0 : part.EndOffset + 1;
                sector = part.EndSector == 0 ? 0 : part.EndSector + 1;
                cylinder = part.EndCylinder == 0 ? 0 : part.EndCylinder + 1;
            }

            if (offset < diskSize)
            {
                var unallocatedSize = diskSize - offset + 1;
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

        private static long Next(long value, int size)
        {
            var left = value % size;

            return left == 0 ? value : value - left + size;
        }
    }
}