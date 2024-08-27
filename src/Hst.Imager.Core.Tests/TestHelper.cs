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

    public static class TestHelper
    {
        public static readonly byte[] Pfs3DosType = { 0x50, 0x46, 0x53, 0x3 };
        public static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");

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

    }
}