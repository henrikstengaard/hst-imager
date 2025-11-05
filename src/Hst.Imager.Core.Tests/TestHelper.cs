using System;
using System.IO.Compression;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Hst.Core.Extensions;
    using Models;
    using Hst.Amiga.Extensions;
    using Hst.Amiga.FileSystems.Pfs3;
    using System.Collections.Generic;
    using Hst.Amiga.FileSystems.FastFileSystem;
    using Hst.Amiga;
    using System.Text;
    using Hst.Imager.Core.PathComponents;

    public static class TestHelper
    {
        public static readonly byte[] Dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };
        public static readonly byte[] Dos7DosType = { 0x44, 0x4f, 0x53, 0x7 };
        public static readonly byte[] Pfs3DosType = { 0x50, 0x46, 0x53, 0x3 };
        public static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");

        public static readonly byte[] FastFileSystemDos3Bytes = new byte[]{ 0x0, 0x0, 0x03, 0xf3 }.Concat(Encoding.ASCII.GetBytes(
            "$VER: FastFileSystem 0.1 (01/01/22) ")).ToArray();
        public static readonly byte[] FastFileSystemDos7Bytes = new byte[]{ 0x0, 0x0, 0x03, 0xf3 }.Concat(Encoding.ASCII.GetBytes(
            "$VER: FastFileSystem 46.13 (01/01/22)   ")).ToArray();
        public static readonly byte[] Pfs3AioBytes = new byte[]{ 0x0, 0x0, 0x03, 0xf3 }.Concat(Encoding.ASCII.GetBytes(
            "$VER: pfs3aio 0.1 (01/01/22)")).ToArray();

        public static async Task CreateRdbDisk(TestCommandHelper testCommandHelper, string path, 
            long diskSize = 10 * 1024 * 1024)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());

            rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath));
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        }

        public static async Task AddRdbDiskPartition(TestCommandHelper testCommandHelper, string path, 
            long partitionSize = 0, byte[] data = null)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            // read rigid disk block
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            var dataSize = data?.Length ?? 0;
            var size = partitionSize > 0 ? partitionSize : dataSize;
            
            // add partition to rigid disk block
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            rigidDiskBlock = rigidDiskBlock.AddPartition(Pfs3DosType, $"DH{partitionBlocks.Count}", size, bootable: partitionBlocks.Count == 0);

            // write rigid disk block
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
            
            // return if no data
            if (data == null || data.Length == 0)
            {
                return;
            }
            
            var partitionBlock = rigidDiskBlock.PartitionBlocks.Last();

            // calculate cylinders and cylinder size
            var cylinders = partitionBlock.HighCyl - partitionBlock.LowCyl + 1;
            var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;

            // calculate start offset
            var startOffset = (long)partitionBlock.LowCyl * cylinderSize;
            
            stream.Seek(startOffset, SeekOrigin.Begin);
            
            await stream.WriteAsync(data.AsMemory(0, (int)Math.Min(cylinders * cylinderSize, size)));
        }
        
        public static async Task CreatePfs3FormattedDisk(TestCommandHelper testCommandHelper, string path, 
            long diskSize = 10 * 1024 * 1024)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());

            rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
                .AddPartition("DH0", bootable: true);
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

            await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
        }

        public static async Task<Pfs3Volume> MountPfs3Volume(Stream stream)
        {
            stream.Position = 0;
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

            return await Pfs3Volume.Mount(stream, partitionBlock);
        }

        public static async Task CreatePfs3DirectoriesAndFiles(TestCommandHelper testCommandHelper, string path,
    IEnumerable<Models.FileSystems.Entry> entries)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            await using var pfs3Volume = await MountPfs3Volume(stream);

            foreach (var entry in entries)
            {
                if (entry.Type == Models.FileSystems.EntryType.File)
                {
                    await pfs3Volume.CreateFile(entry.Name);
                }
            }
        }

        public static async Task CreateFormattedAdfDisk(Stream stream, string volumeName)
        {
            stream.SetLength(FloppyDiskConstants.DoubleDensity.Size);

            await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
                FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
                FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
                FloppyDiskConstants.BlockSize, FloppyDiskConstants.BlockSize, Dos3DosType, volumeName);
        }

        public static async Task CreateFormattedAdfDisk(TestCommandHelper testCommandHelper, string adfPath, string volumeName)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(adfPath, size: 0, create: true);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            await CreateFormattedAdfDisk(stream, volumeName);
        }

        public static async Task AddFileToAdf(Stream stream, string filePath, byte[] fileData)
        {
            await using var ffsVolume = await FastFileSystemVolume.MountAdf(stream);

            var pathSegments = MediaPath.AmigaOsPath.Split(filePath);

            for(var i = 0; i < pathSegments.Length - 1; i++)
            {
                await ffsVolume.CreateDirectory(pathSegments[i]);
                await ffsVolume.ChangeDirectory(pathSegments[i]);
            }

            await ffsVolume.CreateFile(pathSegments[^1], true);
            using var fileStream = await ffsVolume.OpenFile(pathSegments[^1], Amiga.FileSystems.FileMode.Append);
            await fileStream.WriteBytes(fileData);
        }

        public static void DeletePaths(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    continue;
                }

                if (System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.Delete(path, true);
                }
            }
        }
        
        public static async Task CreateGptDisk(TestCommandHelper testCommandHelper, string path,
            long diskSize)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(stream, Ownership.None);
            GuidPartitionTable.Initialize(disk);
        }

        public static async Task AddGptDiskPartition(TestCommandHelper testCommandHelper, string path,
            long partitionSize = 0, byte[] data = null)
        {
            if (partitionSize == 0 && data == null)
            {
                throw new ArgumentException("Partition size or data must be provided");
            }
                
            var dataSize = data?.Length ?? 0;
                
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media.Stream;
                
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(stream, Ownership.None);
            var guidPartitionTable = new GuidPartitionTable(disk);

            var size = partitionSize > 0 ? partitionSize : dataSize; 
            var sectors = Convert.ToInt64(Math.Ceiling((double)size / 512));
                
            var startSector = guidPartitionTable.Partitions.Count == 0
                ? guidPartitionTable.FirstUsableSector
                : guidPartitionTable.Partitions.Max(x => x.LastSector) + 1;
            var endSector = startSector + sectors - 1;
            if (endSector > guidPartitionTable.LastUsableSector)
            {
                endSector = guidPartitionTable.LastUsableSector;
            }
                
            var partitionIndex = guidPartitionTable.Create(startSector, endSector,
                GuidPartitionTypes.WindowsBasicData, 0, "Empty");

            if (data == null || data.Length == 0)
            {
                return;
            }
                
            var partition = guidPartitionTable.Partitions[partitionIndex];

            await using var partitionStream = partition.Open();

            partitionStream.Position = 0;
            await partitionStream.WriteAsync(data.AsMemory(0, (int)size));
        }
        
        public static async Task CreateMbrDisk(TestCommandHelper testCommandHelper, string path,
            long diskSize)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(stream, Ownership.None);
            BiosPartitionTable.Initialize(disk);
        }

        public static async Task AddMbrDiskPartition(TestCommandHelper testCommandHelper, string path,
            long partitionSize = 0, byte biosType = BiosPartitionTypes.Fat32Lba, byte[] data = null)
        {
            if (partitionSize == 0 && data == null)
            {
                throw new ArgumentException("Partition size or data must be provided");
            }
                
            var dataSize = data?.Length ?? 0;
                
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media.Stream;
                
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(stream, Ownership.None);
            var biosPartitionTable = new BiosPartitionTable(disk);

            var size = partitionSize > 0 ? partitionSize : dataSize; 
            var sectors = Convert.ToInt64(Math.Ceiling((double)size / 512));
                
            var startSector = biosPartitionTable.Partitions.Count == 0
                ? 1
                : biosPartitionTable.Partitions.Max(x => x.LastSector) + 1;
            var endSector = startSector + sectors - 1;

            if (endSector > disk.Geometry.Value.TotalSectorsLong)
            {
                endSector = disk.Geometry.Value.TotalSectorsLong;
            }
                
            var partitionIndex = biosPartitionTable.CreatePrimaryBySector(startSector, endSector,
                biosType, biosPartitionTable.Partitions.Count == 0);

            if (data == null || data.Length == 0)
            {
                return;
            }
                
            var partition = biosPartitionTable.Partitions[partitionIndex];

            await using var partitionStream = partition.Open();

            partitionStream.Position = 0;
            await partitionStream.WriteAsync(data.AsMemory(0, (int)size));
        }
        
        public static async Task<PartInfo> GetMbrPartitionPart(TestCommandHelper testCommandHelper, string path,
            int partitionNumber)
        {
            var mediaResult = await testCommandHelper.GetReadableMedia([], path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            using var media = mediaResult.Value;

            var diskInfo = await testCommandHelper.ReadDiskInfo(media);
            
            if (diskInfo == null || diskInfo.MbrPartitionTablePart == null)
            {
                throw new IOException("No MBR partition table found");
            }
            
            var part = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => 
                x.PartitionNumber == partitionNumber);
            
            if (part == null)
            {
                throw new IOException($"Partition {partitionNumber} not found");
            }

            return part;
        }

        public static async Task<PartInfo> GetGptPartitionPart(TestCommandHelper testCommandHelper, string path,
            int partitionNumber)
        {
            var mediaResult = await testCommandHelper.GetReadableMedia([], path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            using var media = mediaResult.Value;

            var diskInfo = await testCommandHelper.ReadDiskInfo(media);
            
            if (diskInfo == null || diskInfo.GptPartitionTablePart == null)
            {
                throw new IOException("No GPT partition table found");
            }
            
            var part = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => 
                x.PartitionNumber == partitionNumber);
            
            if (part == null)
            {
                throw new IOException($"Partition {partitionNumber} not found");
            }

            return part;
        }

        public static async Task<PartInfo> GetRdbPartitionPart(TestCommandHelper testCommandHelper, string path,
            int partitionNumber)
        {
            var mediaResult = await testCommandHelper.GetReadableMedia([], path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            using var media = mediaResult.Value;

            var diskInfo = await testCommandHelper.ReadDiskInfo(media);
            
            if (diskInfo == null || diskInfo.RdbPartitionTablePart == null)
            {
                throw new IOException("No RDB partition table found");
            }
            
            var part = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x => 
                x.PartitionNumber == partitionNumber);
            
            if (part == null)
            {
                throw new IOException($"Partition {partitionNumber} not found");
            }

            return part;
        }

        public static async Task<byte[]> ReadData(TestCommandHelper testCommandHelper, string path, long offset = 0,
            int length = 0)
        {
            var mediaResult = await testCommandHelper.GetReadableMedia([], path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            using var media = mediaResult.Value;
            
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            var stream = disk.Content;
            stream.Position = offset;
            
            var buffer = new byte[1024 * 1024];
            var readStream = new MemoryStream();

            int readBytes;
            int bytesRead;
            do
            {
                readBytes = length == 0 || readStream.Length + buffer.Length < length
                    ? buffer.Length
                    : length - (int)readStream.Length;
                bytesRead = await stream.ReadAsync(buffer, 0, readBytes);
                await readStream.WriteAsync(buffer, 0, bytesRead);
            } while (readBytes > 0 && bytesRead > 0);
            
            return readStream.ToArray();
        }
        
        public static async Task WriteData(TestCommandHelper testCommandHelper, string path, long offset, byte[] data)
        {
            var mediaResult = await testCommandHelper.GetWritableMedia([], path, size: data.Length);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            using var media = mediaResult.Value;
            
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            var stream = disk.Content;
            
            stream.Position = offset;
            await stream.WriteAsync(data);
        }

        public static async Task<byte[]> CreateZipCompressedImgData(byte[] data)
        {
            using var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var zipArchiveEntry = zipArchive.CreateEntry("disk.img", CompressionLevel.Fastest);
                await using var entryStream = zipArchiveEntry.Open();
                await entryStream.WriteAsync(data);
            }
            
            return zipStream.ToArray();
        }

        public static async Task CreateGptFatFormattedDisk(TestCommandHelper testCommandHelper, string path,
            long diskSize = 10 * 1024 * 1024)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
            var guidPartitionTable = GuidPartitionTable.Initialize(disk);
            var partitionIndex = guidPartitionTable.Create(guidPartitionTable.FirstUsableSector,
                guidPartitionTable.LastUsableSector, GuidPartitionTypes.WindowsBasicData, 0, "Empty");
            FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK");
        }

        public static async Task CreateMbrFatFormattedDisk(TestCommandHelper testCommandHelper, string path,
            long diskSize = 10 * 1024 * 1024)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
            var biosPartitionTable = BiosPartitionTable.Initialize(disk);
            var partitionIndex = biosPartitionTable.CreatePrimaryBySector(1, (disk.Capacity / disk.SectorSize) - 1,
                BiosPartitionTypes.Fat32Lba, true);
            FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK");
        }

        public static async Task CreateRdbPfs3Directory(TestCommandHelper testCommandHelper,
            string diskPath, string[] dirPathComponents)
        {
            var mediaResult = await testCommandHelper.GetWritableMedia([], diskPath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            await using var pfs3Volume = await MountPfs3Volume(stream);

            for (var i = 0; i < dirPathComponents.Length; i++)
            {
                await pfs3Volume.CreateDirectory(dirPathComponents[i]);
            
                if (i >= dirPathComponents.Length - 1)
                {
                    break;
                }

                await pfs3Volume.ChangeDirectory(dirPathComponents[i]);
            }
        }
        
        public static async Task CreateMbrFatDirectory(TestCommandHelper testCommandHelper,
            string diskPath, string[] dirPathComponents)
        {
            var mediaResult = await testCommandHelper.GetWritableMedia([], diskPath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            using var media = mediaResult.Value;
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            var biosPartitionTable = new BiosPartitionTable(disk);
            using var fatFileSystem = new FatFileSystem(biosPartitionTable.Partitions[0].Open());

            var path = string.Join(Path.DirectorySeparatorChar, dirPathComponents);
        
            fatFileSystem.CreateDirectory(path);
        }

        public static void CreateLocalDirectory(string dirPath, string[] dirPathComponents)
        {
            var path = Path.Combine(dirPath, Path.Combine(dirPathComponents));
        
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
        }
        
        public static async Task<byte[]> ReadMediaBytes(ICommandHelper commandHelper, string path, long? size = null)
        {
            var mediaResult = await commandHelper.GetReadableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
            stream.Position = 0;
            var readSize = size ?? media.Size;
            var bytes = new byte[readSize];
            var bytesRead = await stream.ReadAsync(bytes, 0, bytes.Length);

            return readSize != bytesRead
                ? throw new IOException($"Failed to read {readSize} bytes from {path}, instead read {bytesRead} bytes")
                : bytes;
        }
    }
}