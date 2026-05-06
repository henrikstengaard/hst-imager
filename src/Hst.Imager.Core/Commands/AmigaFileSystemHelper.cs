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
using Hst.Imager.Core.MagicBytes;
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
        /// <returns>Name and data for file system with highest version found.</returns>
        public static async Task<Result<Tuple<string, byte[]>>> FindFileSystemInMedia(ICommandHelper commandHelper, string mediaPath,
            string fileSystemName)
        {
            var mediaResult = await commandHelper.GetReadableFileMedia(mediaPath);

            if (mediaResult.IsFaulted)
            {
                return new Result<Tuple<string, byte[]>>(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            var fileSystemsResult = await GetFileSystemsFromMedia(media, mediaPath, media.Stream,
                fileSystemName);
            if (fileSystemsResult.IsFaulted)
            {
                return new Result<Tuple<string, byte[]>>(fileSystemsResult.Error);
            }

            var fileSystemWithHighestVersion = fileSystemsResult.Value
                .OrderDescending(new FileSystemVersionComparer())
                .FirstOrDefault();

            if (fileSystemWithHighestVersion == null)
            {
                return new Result<Tuple<string, byte[]>>(new Tuple<string, byte[]>(string.Empty, []));
            }

            return new Result<Tuple<string, byte[]>>(fileSystemWithHighestVersion);
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> GetFileSystemsFromMedia(Media media,
            string mediaPath, Stream mediaStream, string fileSystemName)
        {
            // read first 10mb from media
            var firstBytes = await mediaStream.ReadBytes((int)(mediaStream.Length > 10.MB()
                ? 10.MB()
                : mediaStream.Length));

            mediaStream.Position = 0;

            if (!MagicBytesRegister.Instance.TryResolve(firstBytes, out var dataType))
            {
                return new Result<IEnumerable<Tuple<string, byte[]>>>(new List<Tuple<string, byte[]>>());
            }

            switch (dataType)
            {
                case DataType.Hunk:
                    return await GetMediaAsFileSystem(mediaStream, Path.GetFileName(mediaPath));
                case DataType.Adf:
                    return await FindFileSystemsInAdf(media, mediaStream, fileSystemName);
                case DataType.Lha:
                    return await FindFileSystemsInLha(mediaStream, fileSystemName);
                case DataType.Iso9660:
                    return await FindFileSystemsInIso(media, mediaStream, fileSystemName);
                case DataType.Rdb:
                    return await FindFileSystemsInRdb(mediaStream, fileSystemName);
                case DataType.Mbr:
                    return await FindFileSystemsInMbrPiStormRdb(mediaStream, fileSystemName);
            }

            return new Result<IEnumerable<Tuple<string, byte[]>>>(new List<Tuple<string, byte[]>>());
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> GetMediaAsFileSystem(Stream stream,
            string fileSystemName)
        {
            using var fileSystemStream = new MemoryStream();
            await stream.CopyToAsync(fileSystemStream);

            return new Result<IEnumerable<Tuple<string, byte[]>>>(new List<Tuple<string, byte[]>>([
                new Tuple<string, byte[]>(fileSystemName, fileSystemStream.ToArray())
            ]));
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInIso(Media media, Stream stream,
            string fileSystemName)
        {
            var cdReader = new CDReader(stream, true);
            var iso9660Iterator = new Iso9660EntryIterator(stream, string.Empty, cdReader, true);
            var initializeResult = await iso9660Iterator.Initialize();
            if (initializeResult.IsFaulted)
            {
                return new Result<IEnumerable<Tuple<string, byte[]>>>(initializeResult.Error);
            }
            
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

                var fileSystemsResult = await FindFileSystemsInAdf(media, adfStream, fileSystemName);
                if (fileSystemsResult.IsFaulted)
                {
                    return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystemsResult.Error);
                }
                
                fileSystems.AddRange(fileSystemsResult.Value);
            }

            return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystems);
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInLha(Stream stream,
            string fileSystemName)
        {
            var lhaArchive = new LhaArchive(stream);
            var lhaEntryIterator = new LhaArchiveEntryIterator(stream, string.Empty, lhaArchive, true);

            var initializeResult = await lhaEntryIterator.Initialize();
            if (initializeResult.IsFaulted)
            {
                return new Result<IEnumerable<Tuple<string, byte[]>>>(initializeResult.Error);
            }
            
            return await FindFileSystemsInEntryIterator(lhaEntryIterator, fileSystemName);
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInAdf(Media media, Stream stream,
            string fileSystemName)
        {
            var fastFileSystemVolume = await FastFileSystemVolume.MountAdf(stream);

            var amigaVolumeEntryIterator = new AmigaVolumeEntryIterator(media, PartitionTableType.None, 0,
                fastFileSystemVolume, [], true);

            var initializeResult = await amigaVolumeEntryIterator.Initialize();
            if (initializeResult.IsFaulted)
            {
                return new Result<IEnumerable<Tuple<string, byte[]>>>(initializeResult.Error);
            }
            
            return await FindFileSystemsInEntryIterator(amigaVolumeEntryIterator, fileSystemName);
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInEntryIterator(
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

            return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystems);
        }

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInRdb(Stream stream,
            string fileSystemName)
        {
            var rigidDiskBlock = await Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(stream);

            if (rigidDiskBlock == null)
            {
                return new Result<IEnumerable<Tuple<string, byte[]>>>(new List<Tuple<string, byte[]>>());
            }
            
            var fileSystems = rigidDiskBlock.FileSystemHeaderBlocks
                .Where(x => IsDosTypeValidForFileSystem(x.DosType, fileSystemName))
                .Select(x => new Tuple<string, byte[]>(fileSystemName, 
                    x.LoadSegBlocks.SelectMany(loadSegBlock => loadSegBlock.Data).ToArray()))
                .ToList();

            return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystems);
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

        private static async Task<Result<IEnumerable<Tuple<string, byte[]>>>> FindFileSystemsInMbrPiStormRdb(Stream stream,
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
                return new Result<IEnumerable<Tuple<string, byte[]>>>(new List<Tuple<string, byte[]>>());
            }

            if (!biosPartitionTable.DiskGeometry.HasValue)
            {
                throw new InvalidOperationException("Disk geometry is not available in BIOS partition table");
            }
            
            var fileSystems = new List<Tuple<string, byte[]>>();

            foreach (var partitionInfo in biosPartitionTable.Partitions
                         .Where(x => x.BiosType == Constants.BiosPartitionTypes.PiStormRdb))
            {
                var partitionStartOffset = partitionInfo.FirstSector * biosPartitionTable.DiskGeometry.Value.BytesPerSector;
                var partitionSize = (partitionInfo.LastSector - partitionInfo.FirstSector + 1) * biosPartitionTable.DiskGeometry.Value.BytesPerSector;
                
                var partitionStream = new SubStream(stream, partitionStartOffset, partitionSize);
                var fileSystemsResult = await FindFileSystemsInRdb(partitionStream, fileSystemName);
                if (fileSystemsResult.IsFaulted)
                {
                    return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystemsResult.Error);
                }
                
                fileSystems.AddRange(fileSystemsResult.Value);
            }

            return new Result<IEnumerable<Tuple<string, byte[]>>>(fileSystems);
        }
    }
}