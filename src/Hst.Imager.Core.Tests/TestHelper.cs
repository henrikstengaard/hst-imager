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
    }
}