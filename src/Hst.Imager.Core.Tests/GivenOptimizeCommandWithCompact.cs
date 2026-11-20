using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Extensions;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Xunit;
using AmigaFileMode = Hst.Amiga.FileSystems.FileMode;
using Directory = System.IO.Directory;
using File = System.IO.File;
using Size = Hst.Imager.Core.Models.Size;

namespace Hst.Imager.Core.Tests;

public class GivenOptimizeCommandWithCompact
{
    private const long DiskSize = 20 * 1024 * 1024;
    private static readonly byte[] Pfs3DosType = [0x50, 0x46, 0x53, 0x3];
    private static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");

    [Fact]
    public async Task When_CompactingImg_Then_ImageDataIsEqualAndSizeIsUnchanged()
    {
        // arrange - img file with data at start and end, zeros in between
        var imgPath = $"{Guid.NewGuid()}.img";
        var data = new byte[DiskSize];
        new Random(1).NextBytes(data.AsSpan(0, 1024 * 1024));
        new Random(2).NextBytes(data.AsSpan((int)DiskSize - 512, 512));
        await File.WriteAllBytesAsync(imgPath, data);

        try
        {
            // arrange - optimize command with compact
            using var testCommandHelper = new TestCommandHelper();
            var optimizeCommand = new OptimizeCommand(testCommandHelper, imgPath, new Size(0, Unit.Bytes),
                null, compact: true);

            // act - optimize img
            var result = await optimizeCommand.Execute(CancellationToken.None);

            // assert - result is success, data and size is unchanged and no temporary files are left
            Assert.True(result.IsSuccess);
            Assert.Equal(data, await File.ReadAllBytesAsync(imgPath));
            AssertNoTemporaryFiles(imgPath);
        }
        finally
        {
            File.Delete(imgPath);
        }
    }

    [Fact]
    public async Task When_CompactingImgWithSize_Then_ImageIsTruncatedToSize()
    {
        // arrange - img file with data
        var imgPath = $"{Guid.NewGuid()}.img";
        var data = new byte[DiskSize];
        new Random(1).NextBytes(data);
        await File.WriteAllBytesAsync(imgPath, data);

        try
        {
            // arrange - optimize command with compact and size
            using var testCommandHelper = new TestCommandHelper();
            var optimizeCommand = new OptimizeCommand(testCommandHelper, imgPath,
                new Size(1024 * 1024, Unit.Bytes), null, compact: true);

            // act - optimize img
            var result = await optimizeCommand.Execute(CancellationToken.None);

            // assert - result is success and img is truncated to size
            Assert.True(result.IsSuccess);
            Assert.Equal(data.Take(1024 * 1024).ToArray(), await File.ReadAllBytesAsync(imgPath));
        }
        finally
        {
            File.Delete(imgPath);
        }
    }

    [Fact]
    public async Task When_OptimizingVhdWithPfs3AndExaminingFileSystems_Then_FreeSectorsAreRemovedAndFilesAreKept()
    {
        // arrange - vhd with rdb, pfs3 partition, a deleted large file and a kept file
        var vhdPath = $"{Guid.NewGuid()}.vhd";
        var deletedData = CreateData(5 * 1024 * 1024, 1);
        var keptData = CreateData(100 * 1024, 2);

        try
        {
            using (var testCommandHelper = new TestCommandHelper())
            {
                await CreatePfs3Disk(testCommandHelper, vhdPath, deletedData, keptData);
            }

            var sizeBefore = new FileInfo(vhdPath).Length;

            // arrange - optimize command examining file systems
            using (var testCommandHelper = new TestCommandHelper())
            {
                var optimizeCommand = new OptimizeCommand(testCommandHelper, vhdPath, new Size(0, Unit.Bytes),
                    null, examineFileSystems: true);

                // act - optimize vhd
                var result = await optimizeCommand.Execute(CancellationToken.None);

                // assert - result is success
                Assert.True(result.IsSuccess);
            }

            // assert - vhd is smaller as deleted file data is not copied
            var sizeAfter = new FileInfo(vhdPath).Length;
            Assert.True(sizeBefore - sizeAfter >= 4 * 1024 * 1024,
                $"Expected vhd size {sizeBefore} to be reduced by at least 4MB, but size is {sizeAfter}");
            AssertNoTemporaryFiles(vhdPath);

            // assert - kept file is readable from optimized vhd
            using (var testCommandHelper = new TestCommandHelper())
            {
                var mediaResult = await testCommandHelper.GetReadableFileMedia(vhdPath);
                using var media = mediaResult.Value;
                var rigidDiskBlock = await RigidDiskBlockReader.Read(media.Stream);

                // assert - vhd size is optimized to rigid disk block size
                Assert.Equal(rigidDiskBlock.DiskSize, media.Size);

                await using var volume = await Pfs3Volume.Mount(media.Stream, rigidDiskBlock.PartitionBlocks.First());
                await using var stream = await volume.OpenFile("kept.bin", AmigaFileMode.Read, false, false);
                var data = new byte[keptData.Length];
                await stream.ReadExactlyAsync(data);
                Assert.Equal(keptData, data);
            }
        }
        finally
        {
            File.Delete(vhdPath);
        }
    }

    [Fact]
    public async Task When_OptimizingVhd_Then_VhdIsCompactedAndDataIsEqual()
    {
        // arrange - vhd with data
        var vhdPath = $"{Guid.NewGuid()}.vhd";
        var data = CreateData(1024 * 1024, 1);

        try
        {
            using (var testCommandHelper = new TestCommandHelper())
            {
                var mediaResult = await testCommandHelper.GetWritableFileMedia(vhdPath, size: DiskSize, create: true);
                using var media = mediaResult.Value;
                await media.Stream.WriteAsync(data);
            }

            // arrange - optimize command without compact, vhd is always compacted
            using (var testCommandHelper = new TestCommandHelper())
            {
                var optimizeCommand = new OptimizeCommand(testCommandHelper, vhdPath, new Size(0, Unit.Bytes),
                    null);

                // act - optimize vhd
                var result = await optimizeCommand.Execute(CancellationToken.None);

                // assert - result is success
                Assert.True(result.IsSuccess);
            }

            // assert - vhd contains data and has same size
            using (var testCommandHelper = new TestCommandHelper())
            {
                var mediaResult = await testCommandHelper.GetReadableFileMedia(vhdPath);
                using var media = mediaResult.Value;
                Assert.Equal(DiskSize, media.Size);
                var vhdData = new byte[data.Length];
                await media.Stream.ReadExactlyAsync(vhdData);
                Assert.Equal(data, vhdData);
            }
        }
        finally
        {
            File.Delete(vhdPath);
        }
    }

    private static async Task CreatePfs3Disk(TestCommandHelper testCommandHelper, string path, byte[] deletedData,
        byte[] keptData)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: DiskSize, create: true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var rigidDiskBlock = RigidDiskBlock.Create(DiskSize);
        rigidDiskBlock.AddFileSystem(Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");

        await using var volume = await Pfs3Volume.Mount(stream, partitionBlock);
        await using (var fileStream = await volume.OpenFile("deleted.bin", AmigaFileMode.Write, true, false))
        {
            await fileStream.WriteAsync(deletedData);
        }

        await using (var fileStream = await volume.OpenFile("kept.bin", AmigaFileMode.Write, true, false))
        {
            await fileStream.WriteAsync(keptData);
        }

        await volume.Delete("deleted.bin", false);
        await volume.Flush();
    }

    private static byte[] CreateData(int size, int seed)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static void AssertNoTemporaryFiles(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var name = Path.GetFileNameWithoutExtension(path);
        Assert.Empty(Directory.GetFiles(directory, $"{name}.*.optimizing*"));
    }
}
