using Hst.Core;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using System.IO;
using DiscUtils.Iso9660;
using Hst.Amiga.FileSystems.FastFileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Compression.Lha;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands
{
    public static class AmigaFileSystemHelper
    {
        /// <summary>
        /// Find file system in iso, adf or lha media.
        /// </summary>
        /// <param name="commandHelper">Command helper.</param>
        /// <param name="mediaPath">Path to media with file system to find.</param>
        /// <param name="fileSystemName">Name of file system to find.</param>
        /// <param name="outputPath">Output path to write found file system.</param>
        /// <returns>Name of file system found written in file system path.</returns>
        public static async Task<Result<string>> FindFileSystemInMedia(ICommandHelper commandHelper, string mediaPath,
            string fileSystemName, string outputPath)
        {
            var mediaResult = await commandHelper.GetReadableFileMedia(mediaPath);

            if (mediaResult.IsFaulted)
            {
                return new Result<string>(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            var mediaStream = media is DiskMedia diskMedia
                ? diskMedia.Disk.Content
                : media.Stream;

            var fileSystems = await GetFileSystemsFromMedia(mediaPath, mediaStream, fileSystemName);

            var fileSystemWithHighestVersion = fileSystems.OrderDescending(new FileSystemVersionComparer())
                .FirstOrDefault();

            if (fileSystemWithHighestVersion == null)
            {
                return new Result<string>(string.Empty);
            }

            var fileSystemMediaResult = await commandHelper.GetWritableFileMedia(
                Path.Combine(outputPath, fileSystemWithHighestVersion.Item1), create: true);
            if (fileSystemMediaResult.IsFaulted)
            {
                return new Result<string>(fileSystemMediaResult.Error);
            }

            using var fileSystemMedia = fileSystemMediaResult.Value;
            await fileSystemMedia.Stream.WriteBytes(fileSystemWithHighestVersion.Item2);

            return new Result<string>(fileSystemWithHighestVersion.Item1);
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> GetFileSystemsFromMedia(string mediaPath,
            Stream mediaStream, string fileSystemName)
        {
            // read first 100kb from media
            var firstBytes = await mediaStream.ReadBytes((int)(mediaStream.Length > 100.KB()
                ? 100.KB()
                : mediaStream.Length));

            mediaStream.Position = 0;
            
            // return file, if media has hunk magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.HunkMagicNumber, firstBytes, 0))
            {
                return await GetMediaAsFileSystem(mediaStream, Path.GetFileName(mediaPath));
            }
            
            // read file systems from adf, if media has adf magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.AdfDosMagicNumber, firstBytes, 0))
            {
                return await FindFileSystemsInAdf(mediaStream, fileSystemName);
            }

            // read file systems from lha, if media has lha magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.LhaMagicNumber, firstBytes, 2))
            {
                return await FindFileSystemsInLha(mediaStream, fileSystemName);
            }

            // read file systems from iso and adf files in iso, if media has iso magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, firstBytes, 0x8001) ||
                MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, firstBytes, 0x8801) ||
                MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, firstBytes, 0x9001))
            {
                return await FindFileSystemsInIso(mediaStream, fileSystemName);
            }

            // read file systems from rigid disk block, if media has rdb magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.RdbMagicNumber, firstBytes, 0))
            {
                return await FindFileSystemsInRdb(mediaStream, fileSystemName);
            }

            // read file systems from mbr pistorm rdb partitions, if media has mbr magic number
            if (MagicBytes.HasMagicNumber(MagicBytes.MbrMagicNumber, firstBytes, 0x1fe))
            {
                return await FindFileSystemsInMbrPiStormRdb(mediaStream, fileSystemName);
            }

            return new List<Tuple<string, byte[]>>();
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> GetMediaAsFileSystem(Stream stream,
            string fileSystemName)
        {
            using var fileSystemStream = new MemoryStream();
            await stream.CopyToAsync(fileSystemStream);

            return new List<Tuple<string, byte[]>>([
                new Tuple<string, byte[]>(fileSystemName, fileSystemStream.ToArray())
            ]);
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInIso(Stream stream,
            string fileSystemName)
        {
            var cdReader = new CDReader(stream, true);
            var iso9660Iterator = new Iso9660EntryIterator(stream, string.Empty, cdReader, true);

            var fileSystems = new List<Tuple<string, byte[]>>();

            while (await iso9660Iterator.Next())
            {
                var entry = iso9660Iterator.Current;

                // skip entry, if larger than 1mb or if it doesn't end with ".adf"
                if (entry.Size > 1.MB() || !entry.Name.EndsWith(".adf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var adfStream = await iso9660Iterator.OpenEntry(entry);

                fileSystems.AddRange(await FindFileSystemsInAdf(adfStream, fileSystemName));
            }

            return fileSystems;
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInLha(Stream stream,
            string fileSystemName)
        {
            var lhaArchive = new LhaArchive(stream);
            var lhaEntryIterator = new LhaArchiveEntryIterator(stream, string.Empty, lhaArchive, true);

            return await FindFileSystemsInEntryIterator(lhaEntryIterator, fileSystemName);
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInAdf(Stream stream,
            string fileSystemName)
        {
            var fastFileSystemVolume = await FastFileSystemVolume.MountAdf(stream);

            var amigaVolumeEntryIterator = new AmigaVolumeEntryIterator(stream, string.Empty, fastFileSystemVolume, true);

            return await FindFileSystemsInEntryIterator(amigaVolumeEntryIterator, fileSystemName);
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInEntryIterator(
            IEntryIterator entryIterator, string fileSystemName)
        {
            var fileSystems = new List<Tuple<string, byte[]>>();

            while (await entryIterator.Next())
            {
                var entry = entryIterator.Current;

                var fileName = entry.FullPathComponents[^1];

                if (entry.Type != Models.FileSystems.EntryType.File ||
                    entry.Size >= 1.MB() ||
                    !fileName.Equals(fileSystemName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                await using var entryStream = await entryIterator.OpenEntry(entry);
                using var fileSystemStream = new MemoryStream();
                await entryStream.CopyToAsync(fileSystemStream);

                fileSystems.Add(new Tuple<string, byte[]>(fileName, fileSystemStream.ToArray()));
            }

            return fileSystems;
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInRdb(Stream stream,
            string fileSystemName)
        {
            var rigidDiskBlock = await Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(stream);

            if (rigidDiskBlock == null)
            {
                return new List<Tuple<string, byte[]>>();
            }
            
            return rigidDiskBlock.FileSystemHeaderBlocks
                .Where(x => IsDosTypeValidForFileSystem(x.DosType, fileSystemName))
                .Select(x => new Tuple<string, byte[]>(fileSystemName, 
                    x.LoadSegBlocks.SelectMany(loadSegBlock => loadSegBlock.Data).ToArray()))
                .ToList();
        }

        private static bool IsDosTypeValidForFileSystem(byte[] dosType, string fileSystemName)
        {
            if (dosType.Length < 3)
            {
                return false;
            }
            
            var dosTypeId = Encoding.ASCII.GetString(dosType, 0, 3);
            
            if (dosTypeId.Equals("DOS", StringComparison.OrdinalIgnoreCase) &&
                fileSystemName.Equals("FastFileSystem", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            if ((dosTypeId.Equals("PDS", StringComparison.OrdinalIgnoreCase) ||
                dosTypeId.Equals("PFS", StringComparison.OrdinalIgnoreCase)) &&
                fileSystemName.Equals("pfs3aio", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static async Task<IEnumerable<Tuple<string, byte[]>>> FindFileSystemsInMbrPiStormRdb(Stream stream,
            string fileSystemName)
        {
            BiosPartitionTable biosPartitionTable;
            
            try
            {
                var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
                disk.Content.Position = 0;
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return [];
            }

            var fileSystems = new List<Tuple<string, byte[]>>();

            foreach (var partitionInfo in biosPartitionTable.Partitions
                         .Where(x => x.BiosType == Constants.BiosPartitionTypes.PiStormRdb))
            {
                var partitionStartOffset = partitionInfo.FirstSector * biosPartitionTable.DiskGeometry.BytesPerSector;
                var partitionSize = (partitionInfo.LastSector - partitionInfo.FirstSector + 1) * biosPartitionTable.DiskGeometry.BytesPerSector;
                
                var partitionStream = new SubStream(stream, partitionStartOffset, partitionSize);
                
                fileSystems.AddRange(await FindFileSystemsInRdb(partitionStream, fileSystemName));
            }

            return fileSystems;
        }
    }
}