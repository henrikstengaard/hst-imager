namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using DiscUtils;
    using DiscUtils.Partitions;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Models;

    public class CommandHelper : ICommandHelper
    {
        private readonly bool isAdministrator;

        private static readonly Regex PhysicalDrivePathRegex =
            new("^(\\\\\\\\\\.\\\\PHYSICALDRIVE|/dev)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public CommandHelper(bool isAdministrator)
        {
            this.isAdministrator = isAdministrator;
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
        }

        public virtual Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            bool allowPhysicalDrive = true)
        {
            if (!isAdministrator && PhysicalDrivePathRegex.IsMatch(path))
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
                return new Result<Media>(new Media(path, physicalDrive.Model, physicalDrive.Size, Media.MediaType.Raw,
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

            var model = Path.GetFileName(path);
            if (!IsVhd(path))
            {
                return new Result<Media>(new Media(path, model, new FileInfo(path).Length, Media.MediaType.Raw, false,
                    File.Open(path, FileMode.Open, FileAccess.Read)));
            }

            var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.Read);
            vhdDisk.Content.Position = 0;
            return new Result<Media>(new VhdMedia(path, model, vhdDisk.Capacity, Media.MediaType.Vhd, false, vhdDisk));
        }

        public virtual Stream CreateWriteableStream(string path)
        {
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            long? size = null, bool allowPhysicalDrive = true)
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
                return new Result<Media>(new Media(path, physicalDrive.Model, physicalDrive.Size, Media.MediaType.Raw,
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

            var model = Path.GetFileName(path);

            if (!IsVhd(path))
            {
                return new Result<Media>(new Media(path, model, 0, Media.MediaType.Raw, false,
                    CreateWriteableStream(path)));
            }

            if (File.Exists(path))
            {
                var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
                vhdDisk.Content.Position = 0;
                return new Result<Media>(new VhdMedia(path, model, vhdDisk.Capacity, Media.MediaType.Vhd, false,
                    vhdDisk));
            }

            if (size == null || size.Value == 0)
            {
                throw new ArgumentNullException(nameof(size), "Size required for vhd");
            }

            var stream = CreateWriteableStream(path);
            var newVhdDisk = Disk.InitializeDynamic(stream, Ownership.None, GetVhdSize(size.Value));
            return new Result<Media>(new VhdMedia(path, model, newVhdDisk.Capacity, Media.MediaType.Vhd, false,
                newVhdDisk, stream));
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
            Disk disk = null;
            BiosPartitionTable biosPartitionTable = null;
            try
            {
                disk = new Disk(stream, Ownership.None);
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                // ignored, if read master boot record fails
            }

            if (disk != null)
            {
                await disk.Content.DisposeAsync();
                disk.Dispose();
            }
            
            RigidDiskBlock rigidDiskBlock = null;
            try
            {
                rigidDiskBlock = await GetRigidDiskBlock(stream);
            }
            catch (Exception)
            {
                // ignored, if read rigid disk block fails
            }

            var partitionTables = new List<PartitionTableInfo>();

            if (biosPartitionTable != null)
            {
                var mbrPartitionNumber = 0;
                
                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableInfo.PartitionTableType.MasterBootRecord,
                    Size = disk.Capacity,
                    Partitions = biosPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++mbrPartitionNumber,
                        Type = x.TypeAsString,
                        Size = x.SectorCount * disk.BlockSize,
                        StartOffset = x.FirstSector * disk.BlockSize,
                        EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1
                    }).ToList(),
                    StartOffset = 0,
                    EndOffset = 511
                });
            }

            if (rigidDiskBlock != null)
            {
                var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
                var rdbPartitionNumber = 0;
                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableInfo.PartitionTableType.RigidDiskBlock,
                    Size = rigidDiskBlock.DiskSize,
                    Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++rdbPartitionNumber,
                        Type = x.DosTypeFormatted,
                        Size = x.PartitionSize,
                        StartOffset = (long)x.LowCyl * cylinderSize,
                        EndOffset = ((long)x.HighCyl + 1) * cylinderSize - 1
                    }).ToList(),
                    StartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize,
                    EndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1
                });
            }
            
            return new DiskInfo
            {
                Path = media.Path,
                Name = media.Model,
                Size = media.Size,
                PartitionTables = partitionTables,
                StartOffset = 0,
                EndOffset = media.Size
            };
        }
    }
}