using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core;

namespace Hst.Imager.Core.Helpers
{
    public static class MediaHelper
    {
        public static Media GetMediaWithPiStormRdbSupport(ICommandHelper commandHelper, Media media, string path)
        {
            var pathResult = commandHelper.ResolveMedia(path);
            if (pathResult.IsFaulted)
            {
                return media;
            }
            
            var piStormRdbMediaResult = GetPiStormRdbMedia(
                media, pathResult.Value.FileSystemPath, pathResult.Value.DirectorySeparatorChar);

            return piStormRdbMediaResult.HasPiStormRdb ? piStormRdbMediaResult.Media : media;
        }

        /// <summary>
        /// Get PiStorm Rigid Disk Block media from media.
        /// </summary>
        /// <param name="media">Media used to get pistorm rdb from.</param>
        /// <param name="fileSystemPath">File system path used to get pistorm rdb from.</param>
        /// <param name="directorySeparatorChar"></param>
        /// <returns>PiStormRdb media result with PiStormRdb media, if master boot record partition type is 0x76. Otherwise media is returned.</returns>
        public static PiStormRdbMediaResult GetPiStormRdbMedia(Media media, string fileSystemPath,
            string directorySeparatorChar)
        {
            if (media is PiStormRdbMedia)
            {
                return new PiStormRdbMediaResult
                {
                    HasPiStormRdb = true,
                    Media = media,
                    FileSystemPath = fileSystemPath
                };
            }

            var parts = (fileSystemPath ?? string.Empty).Split(new[] { directorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || !parts[0].Equals("mbr", StringComparison.OrdinalIgnoreCase))
            {
                return new PiStormRdbMediaResult
                {
                    HasPiStormRdb = false,
                    Media = media,
                    FileSystemPath = fileSystemPath
                };
            }

            if (!int.TryParse(parts[1], out var partitionNumber))
            {
                return new PiStormRdbMediaResult
                {
                    HasPiStormRdb = false,
                    Media = media,
                    FileSystemPath = fileSystemPath
                };
            }

            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return new PiStormRdbMediaResult
                {
                    HasPiStormRdb = false,
                    Media = media,
                    FileSystemPath = fileSystemPath
                };
            }

            var partitionInfo = biosPartitionTable.Partitions[partitionNumber - 1];

            if (partitionInfo.BiosType != Constants.BiosPartitionTypes.PiStormRdb)
            {
                return new PiStormRdbMediaResult
                {
                    HasPiStormRdb = false,
                    Media = media,
                    FileSystemPath = fileSystemPath
                };
            }

            var partitionOffset = partitionInfo.FirstSector * disk.SectorSize;
            var partitionSize = partitionInfo.SectorCount * disk.SectorSize;

            var partitionStream = new VirtualStream(disk.Content, partitionOffset, partitionSize, partitionSize);

            var piStormRdbMedia = CreatePiStormRdbMedia(media, partitionNumber, partitionSize, partitionStream);

            return new PiStormRdbMediaResult
            {
                HasPiStormRdb = true,
                Media = piStormRdbMedia,
                FileSystemPath = string.Join(directorySeparatorChar, parts.Skip(2))
            };
        }

        private static Media CreatePiStormRdbMedia(Media media, int mbrPartitionNumber, long size, Stream stream)
        {
            var type = media.Type == Media.MediaType.CompressedRaw || media.Type == Media.MediaType.CompressedVhd
                ? Media.MediaType.CompressedRaw
                : Media.MediaType.Raw;

            return new PiStormRdbMedia(
                media.Path, 
                mbrPartitionNumber,
                string.Concat("Partition #", mbrPartitionNumber, ", ", Constants.FileSystemNames.PiStormRdb),
                size,
                type,
                false,
                stream, 
                false,
                media);
        }

        public static async Task<RigidDiskBlock> ReadRigidDiskBlockFromMedia(Media media)
        {
            var stream = GetStreamFromMedia(media);

            stream.Position = 0;
            
            return await RigidDiskBlockReader.Read(stream);
        }

        public static async Task WriteRigidDiskBlockToMedia(Media media, RigidDiskBlock rigidDiskBlock)
        {
            var stream = GetStreamFromMedia(media);

            stream.Position = 0;

            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        }

        public static VirtualDisk GetDiskFromMedia(Media media)
        {
            if (media is PiStormRdbMedia piStormRdbMedia)
            {
                return new DiscUtils.Raw.Disk(piStormRdbMedia.Stream, Ownership.None);
            }

            if (media is DiskMedia diskMedia)
            {
                return diskMedia.Disk;
            }

            return new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
        }

        public static Stream GetStreamFromMedia(Media media)
        {
            if (media is PiStormRdbMedia piStormRdbMedia)
            {
                return piStormRdbMedia.Stream;
            }

            if (media is DiskMedia diskMedia)
            {
                return diskMedia.Stream;
            }

            return media.Stream;
        }
        
        public static async Task<Result<Tuple<long, long>>> GetStartOffsetAndSize(ICommandHelper commandHelper, Media media, string path)
        {
            // return start offset 0 an media size if media is compressed raw or compressed vhd
            if (media.Type == Media.MediaType.CompressedRaw || media.Type == Media.MediaType.CompressedVhd)
            {
                return new Result<Tuple<long, long>>(new Tuple<long, long>(0, media.Size));
            }
            
            // read disk info
            var diskInfo = await commandHelper.ReadDiskInfo(media);            
            
            var pathComponents = string.IsNullOrEmpty(path)
                ? []
                : path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries).ToArray();

            if (pathComponents.Length == 0)
            {
                return new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.Size));
            }
            
            switch (pathComponents[0])
            {
                case "gpt":
                    if (diskInfo.GptPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Guid Partition Table not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.GptPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.GptPartitionTablePart);
                case "mbr":
                    if (diskInfo.MbrPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Master Boot Record not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.MbrPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.MbrPartitionTablePart);
                case "rdb":
                    if (diskInfo.RdbPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Rigid Disk Block not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.RdbPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.RdbPartitionTablePart);
                default:
                    return new Result<Tuple<long, long>>(new Error($"Unsupported path '{path}'"));
            }
        }
        
        private static Result<Tuple<long, long>> GetPartitionStartOffsetAndSize(string path, string[] pathComponents,
            PartitionTablePart partitionTablePart)
        {
            if (pathComponents.Length == 0)
            {
                return new Result<Tuple<long, long>>(new Error($"Partition number not found in path '{path}'"));
            }
            
            if (pathComponents.Length > 1 ||
                !int.TryParse(pathComponents[0], out var partitionNumber))
            {
                return new Result<Tuple<long, long>>(new Error($"Invalid partition number in path '{path}'"));
            }

            var partition = partitionTablePart.Parts.FirstOrDefault(x => x.PartitionNumber == partitionNumber);
            
            return partition == null 
                ? new Result<Tuple<long, long>>(new Error($"Partition number {partitionNumber} not found"))
                : new Result<Tuple<long, long>>(new Tuple<long, long>(partition.StartOffset, partition.Size));
        }
    }

    public class PiStormRdbMediaResult
    {
        public bool HasPiStormRdb { get; set; }
        public Media Media { get; set; }
        public string FileSystemPath { get; set; }
    }
}
