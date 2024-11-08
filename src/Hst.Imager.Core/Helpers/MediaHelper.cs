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

namespace Hst.Imager.Core.Helpers
{
    public static class MediaHelper
    {
        /// <summary>
        /// Get PiStorm Rigid Disk Block media from media.
        /// </summary>
        /// <param name="media">Media used to get pistorm rdb from.</param>
        /// <param name="fileSystemPath">File system path used to get pistorm rdb from.</param>
        /// <returns>PiStormRdb media, if master boot record partition type is 0x76. Otherwise media is returned.</returns>
        public static async Task<Tuple<Media, string>> GetPiStormRdbMedia(Media media, string fileSystemPath,
            string directorySeparatorChar)
        {
            if (media.Type != Media.MediaType.Raw && media.Type != Media.MediaType.Vhd)
            {
                throw new ArgumentException("Only raw and vhd media is supported", nameof(media));
            }

            if (media is PiStormRdbDiskMedia || media is PiStormRdbMedia)
            {
                return new Tuple<Media, string>(media, fileSystemPath);
            }

            var parts = (fileSystemPath ?? string.Empty).Split(new[] { directorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || !parts[0].Equals("mbr", StringComparison.OrdinalIgnoreCase))
            {
                return new Tuple<Media, string>(media, fileSystemPath);
            }

            if (!int.TryParse(parts[1], out var partitionNumber))
            {
                return new Tuple<Media, string>(media, fileSystemPath);
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
                return new Tuple<Media, string>(media, fileSystemPath);
            }

            var partitionInfo = biosPartitionTable.Partitions[partitionNumber - 1];

            if (partitionInfo.BiosType != Constants.BiosPartitionTypes.PiStormRdb)
            {
                return new Tuple<Media, string>(media, fileSystemPath);
            }

            var partitionOffset = partitionInfo.FirstSector * disk.SectorSize;
            var partitionSize = partitionInfo.SectorCount * disk.SectorSize;

            var partitionStream = new VirtualStream(disk.Content, partitionOffset, partitionSize, partitionSize);

            var rigidDiskBlock = await RigidDiskBlockReader.Read(partitionStream);

            var piStormRdbMedia = CreatePiStormRdbMedia(media, partitionSize, partitionStream, rigidDiskBlock);

            return new Tuple<Media, string>(piStormRdbMedia, string.Join(directorySeparatorChar, parts.Skip(2)));
        }

        private static Media CreatePiStormRdbMedia(Media media, long size, Stream stream, RigidDiskBlock rigidDiskBlock)
        {
            if (media is DiskMedia diskMedia)
            {
                return new PiStormRdbDiskMedia(media.Path, Constants.FileSystemNames.PiStormRdb, size, media.Type,
                false, diskMedia.Disk, false, rigidDiskBlock, stream);
            }

            return new PiStormRdbMedia(media.Path, Constants.FileSystemNames.PiStormRdb, size, media.Type,
                false, stream, false, rigidDiskBlock);
        }
    }
}
