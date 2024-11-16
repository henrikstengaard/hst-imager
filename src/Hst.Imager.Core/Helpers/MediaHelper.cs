using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using SharpCompress.Compressors.Xz;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        /// <returns>PiStormRdb media result with PiStormRdb media, if master boot record partition type is 0x76. Otherwise media is returned.</returns>
        public static PiStormRdbMediaResult GetPiStormRdbMedia(Media media, string fileSystemPath,
            string directorySeparatorChar)
        {
            if (media.Type != Media.MediaType.Raw && media.Type != Media.MediaType.Vhd)
            {
                throw new ArgumentException("Only raw and vhd media is supported for pistormrdb", nameof(media));
            }

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

            var piStormRdbMedia = CreatePiStormRdbMedia(media, partitionSize, partitionStream);

            return new PiStormRdbMediaResult
            {
                HasPiStormRdb = true,
                Media = piStormRdbMedia,
                FileSystemPath = string.Join(directorySeparatorChar, parts.Skip(2))
            };
        }

        private static Media CreatePiStormRdbMedia(Media media, long size, Stream stream)
        {
            var type = media.Type == Media.MediaType.CompressedRaw || media.Type == Media.MediaType.CompressedVhd
                ? Media.MediaType.CompressedRaw
                : Media.MediaType.Raw;

            return new PiStormRdbMedia(media.Path, Constants.FileSystemNames.PiStormRdb, size, type,
                false, stream, false, media);
        }

        public static async Task<RigidDiskBlock> ReadRigidDiskBlockFromMedia(Media media)
        {
            if (media is DiskMedia diskMedia)
            {
                diskMedia.Disk.Content.Position = 0;
                return await RigidDiskBlockReader.Read(diskMedia.Disk.Content);
            }

            media.Stream.Position = 0;
            return await RigidDiskBlockReader.Read(media.Stream);
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
                return diskMedia.Disk.Content;
            }

            return media.Stream;
        }
    }

    public class PiStormRdbMediaResult
    {
        public bool HasPiStormRdb { get; set; }
        public Media Media { get; set; }
        public string FileSystemPath { get; set; }
    }
}
