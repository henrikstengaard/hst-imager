using DiscUtils.Fat;
using DiscUtils.Ntfs;

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
    using Helpers;
    using Hst.Core;
    using Models;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class CommandHelper : ICommandHelper
    {
        private readonly bool isAdministrator;
        
        /// <summary>
        /// Active medias contains opened medias and is used to get reuse medias without opening same media twice
        /// </summary>
        private readonly IList<Media> activeMedias;

        public CommandHelper(bool isAdministrator)
        {
            this.isAdministrator = isAdministrator;
            this.activeMedias = new List<Media>();
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
        }
        
        /// <summary>
        /// Clear active medias to avoid source and destination being reused between commands
        /// </summary>
        public void ClearActiveMedias()
        {
            foreach (var activeMedia in this.activeMedias)
            {
                activeMedia.Dispose();
            }
            this.activeMedias.Clear();
        }

        private Media GetActiveMedia(string path)
        {
            var media = this.activeMedias.FirstOrDefault(x => x.Path == path);
            if (media == null)
            {
                return null;
            }

            if (media is DiskMedia diskMedia && diskMedia.Type == Media.MediaType.Vhd)
            {
                if (diskMedia.IsDisposed)
                {
                    var vhdDisk = VirtualDisk.OpenDisk(path, media.IsWriteable ? FileAccess.ReadWrite : FileAccess.Read);
                    vhdDisk.Content.Position = 0;
                    diskMedia.SetDisk(vhdDisk);
                }
                return diskMedia;
            }

            if (media.Stream == null)
            {
                media.SetStream(File.Open(path, FileMode.Open,
                    media.IsWriteable ? FileAccess.ReadWrite : FileAccess.Read));
            }

            return media;
        }
        
        public virtual Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            return GetPhysicalDriveMedia(physicalDrives, path).Then(() => GetReadableFileMedia(path));
        }

        public virtual Stream CreateWriteableStream(string path, bool create)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            path = PathHelper.GetFullPath(path);
            if (create && File.Exists(path))
            {
                File.Delete(path);
            }

            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Result<Media> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            bool writeable = false)
        {
            var physicalDrivePath = GetPhysicalDrivePath(path);
            if (string.IsNullOrEmpty(physicalDrivePath))
            {
                return new Result<Media>((Media)null);
            }

            var media = GetActiveMedia(physicalDrivePath);
            if (media != null)
            {
                return !isAdministrator 
                    ? new Result<Media>(new Error($"Path '{path}' requires administrator privileges"))
                    : new Result<Media>(media);
            }

            var physicalDrive =
                physicalDrives.FirstOrDefault(x =>
                    x.Path.Equals(physicalDrivePath, StringComparison.OrdinalIgnoreCase));

            if (physicalDrive == null)
            {
                return new Result<Media>(new Error($"Physical drive '{path}' not found"));
            }

            if (!isAdministrator)
            {
                return new Result<Media>(new Error($"Path '{path}' requires administrator privileges"));
            }

            physicalDrive.SetWritable(writeable);
            var physicalDriveMedia = new Media(physicalDrivePath, physicalDrive.Name, physicalDrive.Size,
                Media.MediaType.Raw,
                true, physicalDrive.Open());
            this.activeMedias.Add(physicalDriveMedia);
            return new Result<Media>(physicalDriveMedia);
        }

        private static string GetPhysicalDrivePath(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                var diskPathMatch = Regexs.DiskPathRegex.Match(path);
                if (diskPathMatch.Success)
                {
                    return $"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}";
                }

                return Regexs.PhysicalDrivePathRegex.IsMatch(path) ? path : null;
            }

            if (OperatingSystem.IsMacOs() || OperatingSystem.IsLinux())
            {
                return Regexs.DevicePathRegex.IsMatch(path) ? path : null;
            }

            return null;
        }

        public virtual Result<Media> GetReadableFileMedia(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new Result<Media>(new Error("Path not defined"));
            }

            path = PathHelper.GetFullPath(path);
            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path ?? "null"}' not found", nameof(path)));
            }

            var media = GetActiveMedia(path);
            if (media != null)
            {
                return new Result<Media>(media);
            }

            var name = Path.GetFileName(path);
            if (!IsVhd(path))
            {
                var fileStream = File.Open(path, FileMode.Open, FileAccess.Read);
                var fileMedia = new Media(path, name, fileStream.Length, Media.MediaType.Raw, false, fileStream);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.Read);
            vhdDisk.Content.Position = 0;
            var vhdMedia = new DiskMedia(path, name, vhdDisk.Capacity, Media.MediaType.Vhd, false, vhdDisk,
                new SectorStream(vhdDisk.Content, true));
            this.activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        public virtual Result<Media> GetWritableFileMedia(string path, long? size = null, bool create = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(path);
            }

            path = PathHelper.GetFullPath(path);
            
            var media = GetActiveMedia(path);
            if (media != null)
            {
                return new Result<Media>(media);
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
                    var fileStream = CreateWriteableStream(path, true);
                    var fileMedia = new Media(path, name, fileStream.Length, Media.MediaType.Raw, false,
                        fileStream);
                    this.activeMedias.Add(fileMedia);
                    return new Result<Media>(fileMedia);
                }

                if (size == null || size.Value == 0)
                {
                    throw new ArgumentNullException(nameof(size), "Size is required for creating VHD image file");
                }

                using var vhdStream = CreateWriteableStream(path, true);
                using var newVhdDisk = Disk.InitializeDynamic(vhdStream, Ownership.None, GetVhdSize(size.Value));
            }

            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path}' not found", nameof(path)));
            }

            if (!IsVhd(path))
            {
                var fileStream = CreateWriteableStream(path, false);
                var fileMedia = new Media(path, name, fileStream.Length, Media.MediaType.Raw, false,
                    fileStream);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            disk.Content.Position = 0;
            var vhdMedia = new DiskMedia(path, name, disk.Capacity, Media.MediaType.Vhd, false,
                disk, new SectorStream(disk.Content, true));
            this.activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        public virtual Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            long? size = null, bool create = false)
        {
            return GetPhysicalDriveMedia(physicalDrives, path, true)
                .Then(() => GetWritableFileMedia(path, size, create));
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
        
        public virtual async Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None)
        {
            var partitionTables = new List<PartitionTableInfo>();
            
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            try
            {
                var biosPartitionTable = new BiosPartitionTable(disk);

                var mbrPartitionNumber = 0;

                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableType.MasterBootRecord,
                    Size = disk.Geometry.Capacity,
                    Sectors = disk.Geometry.TotalSectorsLong,
                    Cylinders = 0,
                    Partitions = biosPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++mbrPartitionNumber,
                        FileSystem = x.TypeAsString,
                        BiosType = x.BiosType,
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
                        EndSector = 0,
                        StartCylinder = 0,
                        EndCylinder = 0,
                        Size = 512
                    },
                    StartOffset = 0,
                    EndOffset = disk.Geometry.Capacity - 1,
                    StartSector = 0,
                    EndSector = disk.Geometry.TotalSectorsLong - 1
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
                        BiosType = x.BiosType,
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
                    EndOffset = disk.Capacity - 1,
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
                rigidDiskBlock = await GetRigidDiskBlock(media.Stream);
                if (rigidDiskBlock != null)
                {
                    var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
                    var rdbPartitionNumber = 0;

                    var rdbStartCyl = 0;
                    var rdbEndCyl =
                        Convert.ToInt32(Math.Ceiling((double)rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize /
                                                     cylinderSize)) - 1;

                    var rdbStartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize;
                    var rdbEndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1;
                    partitionTables.Add(new PartitionTableInfo
                    {
                        Type = PartitionTableType.RigidDiskBlock,
                        Size = rigidDiskBlock.DiskSize,
                        Sectors = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
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
                        EndOffset = rdbStartOffset + rigidDiskBlock.DiskSize - 1,
                        StartCylinder = 0,
                        EndCylinder = rigidDiskBlock.HiCylinder,
                        StartSector = rigidDiskBlock.RdbBlockLo,
                        EndSector = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
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

            diskInfo.GptPartitionTablePart = CreateGptParts(diskInfo, partitionTableTypeContext);
            diskInfo.MbrPartitionTablePart = CreateMbrParts(diskInfo, partitionTableTypeContext);
            diskInfo.RdbPartitionTablePart = CreateRdbParts(diskInfo, partitionTableTypeContext);
            diskInfo.DiskParts = CreateDiskParts(diskInfo, partitionTableTypeContext);

            return diskInfo;
        }

        private static IEnumerable<PartInfo> CreateDiskParts(DiskInfo diskInfo,
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

        private static PartitionTablePart CreateGptParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
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
                    PartitionTableType = PartitionTableType.GuidPartitionTable,
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
                BiosType = x.BiosType,
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
                Size = gptPartitionTable.Size,
                Sectors = gptPartitionTable.Sectors,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(gptPartitionTable.Size, gptPartitionTable.Sectors, 0, 
                    partitionTableTypeContext, parts, true, false)
            };
        }

        private static PartitionTablePart CreateMbrParts(DiskInfo diskInfo, PartitionTableType partitionTableTypeContext)
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
                BiosType = x.BiosType,
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
                Size = mbrPartitionTable.Size,
                Sectors = mbrPartitionTable.Sectors,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(mbrPartitionTable.Size, mbrPartitionTable.Sectors, 0,
                    partitionTableTypeContext, parts, true, false)
            };
        }

        private static PartitionTablePart CreateRdbParts(DiskInfo diskInfo, PartitionTableType partitionTableTypeContext)
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
                Parts = CreateUnallocatedParts(diskInfo.RigidDiskBlock.DiskSize,
                    diskInfo.RigidDiskBlock.DiskSize / diskInfo.RigidDiskBlock.BlockSize, 0,
                    partitionTableTypeContext, parts, true, true)
            };
        }

        private static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
            PartitionTableType partitionTableTypeContext, IEnumerable<PartInfo> parts, bool useSectors, bool useCylinders)
        {
            if (diskSize <= 0)
            {
                throw new ArgumentException($"Invalid disk size '{diskSize}'", nameof(diskSize));
            }

            if (partitionTableTypeContext == PartitionTableType.GuidPartitionTable)
            {
                parts = parts.Where(x => x.BiosType != BiosPartitionTypes.GptProtective);
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

        private static long Next(long value, int size)
        {
            var left = value % size;

            return left == 0 ? value : value - left + size;
        }
        
        public virtual Result<MediaResult> ResolveMedia(string path)
        {
            var diskPathMatch = Regexs.DiskPathRegex.Match(path);
            var physicalDrivePath = diskPathMatch.Success
                ? string.Concat($"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}", path.Substring(diskPathMatch.Groups[1].Value.Length + diskPathMatch.Groups[2].Value.Length))
                : path;

            var directorySeparatorChar = Path.DirectorySeparatorChar.ToString();

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '\\' || path[i] == '/')
                {
                    directorySeparatorChar = path[i].ToString();
                    break;
                }
            }

            // physical drive
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(physicalDrivePath);
            if (physicalDrivePathMatch.Success)
            {
                var physicalDriveMediaPath = physicalDrivePathMatch.Value;
                var firstSeparatorIndex = physicalDrivePath.IndexOf(directorySeparatorChar, physicalDriveMediaPath.Length, StringComparison.Ordinal);

                return new Result<MediaResult>(new MediaResult
                {
                    FullPath = physicalDrivePath,
                    MediaPath = physicalDriveMediaPath,
                    FileSystemPath = firstSeparatorIndex >= 0
                        ? physicalDrivePath.Substring(firstSeparatorIndex + 1, physicalDrivePath.Length - (firstSeparatorIndex + 1))
                        : string.Empty,
                    DirectorySeparatorChar = directorySeparatorChar
                });
            }
            
            path = PathHelper.GetFullPath(path);
            
            // media file
            var next = 0;
            do
            {
                next = path.IndexOf(directorySeparatorChar, next + 1, StringComparison.OrdinalIgnoreCase);
                var mediaPath = path.Substring(0, next == -1 ? path.Length : next);

                if (File.Exists(mediaPath))
                {
                    return new Result<MediaResult>(new MediaResult
                    {
                        FullPath = path,
                        MediaPath = mediaPath,
                        FileSystemPath = mediaPath.Length + 1 < path.Length
                            ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                            : string.Empty,
                        DirectorySeparatorChar = directorySeparatorChar
                    });
                }

                if (!Directory.Exists(mediaPath))
                {
                    break;
                }
            } while (next != -1);

            return new Result<MediaResult>(new PathNotFoundError($"Media not '{path}' found", path));
        }
    }
}