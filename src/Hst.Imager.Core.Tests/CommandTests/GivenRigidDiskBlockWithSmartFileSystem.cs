using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Extensions;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.SmartFileSystem;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.FsCommands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FileMode = Hst.Amiga.FileSystems.FileMode;
using Size = Hst.Imager.Core.Models.Size;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenRigidDiskBlockWithSmartFileSystem
{
    private static readonly byte[] Sfs0DosType = [0x53, 0x46, 0x53, 0x0];
    private static readonly byte[] Sfs2DosType = [0x53, 0x46, 0x53, 0x2];

    // dummy smart file system in amiga hunk format (68k)
    private static readonly byte[] SmartFileSystemBytes = new byte[] { 0x0, 0x0, 0x03, 0xf3 }
        .Concat(Encoding.ASCII.GetBytes("$VER: SmartFilesystem 1.279 (23.12.2007)")).ToArray();

    // dummy smart file system in elf format (ppc) with a higher version
    private static readonly byte[] SmartFileSystemPpcBytes = new byte[] { 0x7f, 0x45, 0x4c, 0x46 }
        .Concat(Encoding.ASCII.GetBytes("$VER: SmartFilesystem 1.300 (01.01.2010)")).ToArray();

    [Fact]
    public async Task When_FormattingSfs0Partition_Then_PartitionIsFormattedWithSmartFileSystem()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        const string volumeName = "Workbench";

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create rdb disk with unformatted sfs0 partition
        await CreateRdbDisk(testCommandHelper, diskPath, Sfs0DosType);

        // arrange - create rdb part format command
        var rdbPartFormatCommand = new RdbPartFormatCommand(new NullLogger<RdbPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, 1, volumeName, false, string.Empty,
            string.Empty);

        // act - execute rdb part format command
        var result = await rdbPartFormatCommand.Execute(CancellationToken.None);

        // assert - rdb part format command is successful
        Assert.True(result.IsSuccess);

        // assert - partition is formatted with smart file system and volume name
        await using var volume = await MountSmartFileSystemVolume(testCommandHelper, diskPath);
        Assert.Equal(volumeName, volume.Name);
    }

    [Fact]
    public async Task When_FormattingSfs2Partition_Then_UnsupportedFileSystemErrorIsReturned()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create rdb disk with unformatted sfs2 partition
        await CreateRdbDisk(testCommandHelper, diskPath, Sfs2DosType);

        // arrange - create rdb part format command
        var rdbPartFormatCommand = new RdbPartFormatCommand(new NullLogger<RdbPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, 1, "Workbench", false, string.Empty,
            string.Empty);

        // act - execute rdb part format command
        var result = await rdbPartFormatCommand.Execute(CancellationToken.None);

        // assert - rdb part format command failed with unsupported file system
        Assert.True(result.IsFaulted);
        Assert.Contains("Unsupported file system", result.Error.Message);
    }

    [Fact]
    public async Task When_CopyingFileToSfs0Partition_Then_FileIsWrittenAndCanBeListed()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var adfPath = $"{Guid.NewGuid()}.adf";
        var fileData = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("Smart File System ", 1000)));

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create rdb disk with sfs0 formatted partition
        await CreateRdbDisk(testCommandHelper, diskPath, Sfs0DosType, "Workbench");

        // arrange - create adf with file to copy
        using (var adfStream = new MemoryStream())
        {
            await TestHelper.CreateFormattedAdfDisk(adfStream, "Floppy");
            await TestHelper.AddFileToAdf(adfStream, "file1.txt", fileData);
            await testCommandHelper.AddTestMedia(adfPath, data: adfStream.ToArray());
        }

        // arrange - create directory in sfs0 partition
        var fsMkDirCommand = new FsMkDirCommand(testCommandHelper, new List<IPhysicalDrive>(),
            Path.Combine(diskPath, "rdb", "dh0", "dir1"));
        var fsMkDirResult = await fsMkDirCommand.Execute(CancellationToken.None);
        Assert.True(fsMkDirResult.IsSuccess);

        // act - copy file from adf to directory in sfs0 partition
        var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), Path.Combine(adfPath, "file1.txt"),
            Path.Combine(diskPath, "rdb", "dh0", "dir1"), false, false, true);
        var fsCopyResult = await fsCopyCommand.Execute(CancellationToken.None);

        // assert - fs copy command is successful
        Assert.True(fsCopyResult.IsSuccess);

        // act - list entries in directory in sfs0 partition
        var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), Path.Combine(diskPath, "rdb", "dh0", "dir1"), false);
        EntriesInfo entriesInfo = null;
        fsDirCommand.EntriesRead += (_, args) => entriesInfo = args.EntriesInfo;
        var fsDirResult = await fsDirCommand.Execute(CancellationToken.None);

        // assert - fs dir command is successful and lists copied file
        Assert.True(fsDirResult.IsSuccess);
        Assert.NotNull(entriesInfo);
        var entry = Assert.Single(entriesInfo.Entries);
        Assert.Equal("file1.txt", entry.Name);
        Assert.Equal(fileData.Length, entry.Size);

        // assert - copied file in sfs0 partition has same data
        testCommandHelper.ClearActiveMedias();
        await using var volume = await MountSmartFileSystemVolume(testCommandHelper, diskPath);
        await volume.ChangeDirectory("dir1");
        await using var fileStream = await volume.OpenFile("file1.txt", FileMode.Read, false, false);
        using var fileDataStream = new MemoryStream();
        await fileStream.CopyToAsync(fileDataStream);
        Assert.Equal(fileData, fileDataStream.ToArray());
    }

    [Fact]
    public async Task When_FormattingDiskWithRdbSfs0_Then_DiskIsPartitionedAndFormattedWithSmartFileSystem()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var diskSize = 100.MB();
        const string fileSystemPath = "SmartFilesystem";
        var outputDir = $"{Guid.NewGuid()}-dir";

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add smart file system file
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: SmartFileSystemBytes);

        // arrange - add disk
        testCommandHelper.AddTestMedia(diskPath, 0);
        await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize,
            create: true);

        try
        {
            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, FormatType.Rdb, "sfs0",
                fileSystemPath, outputDir, new Size(), new Size(), false, false);

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format is successful
            Assert.True(formatResult.IsSuccess);

            // assert - rdb has smart file system and one sfs0 partition
            testCommandHelper.ClearActiveMedias();
            var diskData = await testCommandHelper.ReadMediaData(diskPath);
            using var diskStream = new MemoryStream(diskData);
            var rigidDiskBlock = await RigidDiskBlockReader.Read(diskStream);
            var fileSystemHeaderBlock = Assert.Single(rigidDiskBlock.FileSystemHeaderBlocks);
            Assert.Equal(Sfs0DosType, fileSystemHeaderBlock.DosType);
            var partitionBlock = Assert.Single(rigidDiskBlock.PartitionBlocks);
            Assert.Equal(Sfs0DosType, partitionBlock.DosType);

            // assert - partition is formatted with smart file system
            await using var volume = await SmartFileSystemVolume.Mount(diskStream, partitionBlock);
            Assert.Equal("Workbench", volume.Name);
        }
        finally
        {
            TestHelper.DeletePaths(outputDir);
        }
    }

    [Fact]
    public async Task When_FormattingDiskWithRdbSfs0WithoutFileSystemPath_Then_ErrorIsReturned()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add disk
        testCommandHelper.AddTestMedia(diskPath, 0);
        await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: 100.MB(),
            create: true);

        // arrange - create format command
        var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, FormatType.Rdb, "sfs0",
            string.Empty, $"{Guid.NewGuid()}-dir", new Size(), new Size(), false, false);

        // act - execute format command
        var formatResult = await formatCommand.Execute(CancellationToken.None);

        // assert - format failed with file system path required
        Assert.True(formatResult.IsFaulted);
        Assert.Contains(FormatCommand.SmartFileSystemLhaUrl, formatResult.Error.Message);
    }

    [Fact]
    public async Task When_FindingSmartFileSystemInMediaWithHunkAndElfFiles_Then_HunkFileIsFound()
    {
        // arrange - paths
        var adfPath = $"{Guid.NewGuid()}.adf";
        const string fileSystemName = "SmartFilesystem";

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create adf with smart file system for amigaos 3 (hunk) and amigaos 4 (elf) with higher version
        using (var adfStream = new MemoryStream())
        {
            await TestHelper.CreateFormattedAdfDisk(adfStream, "SFS");
            await TestHelper.AddFileToAdf(adfStream, "AmigaOS3.x/L/SmartFilesystem", SmartFileSystemBytes);
            await TestHelper.AddFileToAdf(adfStream, "AmigaOS4/Kickstart/SmartFilesystem", SmartFileSystemPpcBytes);
            await testCommandHelper.AddTestMedia(adfPath, data: adfStream.ToArray());
        }

        // act - find smart file system in adf
        var result = await AmigaFileSystemHelper.FindFileSystemInMedia(testCommandHelper, adfPath, fileSystemName);

        // assert - smart file system in hunk format is found
        Assert.True(result.IsSuccess);
        Assert.Equal(fileSystemName, result.Value.Item1);
        Assert.Equal(SmartFileSystemBytes, result.Value.Item2);
    }

    private static async Task CreateRdbDisk(TestCommandHelper testCommandHelper, string path, byte[] dosType,
        string volumeName = null)
    {
        var diskSize = 10.MB();
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());
        rigidDiskBlock.AddFileSystem(dosType, SmartFileSystemBytes)
            .AddPartition(dosType, "DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        if (string.IsNullOrEmpty(volumeName))
        {
            return;
        }

        await SmartFileSystemFormatter.FormatPartition(stream, rigidDiskBlock.PartitionBlocks.First(), volumeName);
    }

    private static async Task<SmartFileSystemVolume> MountSmartFileSystemVolume(
        TestCommandHelper testCommandHelper, string path)
    {
        var diskData = await testCommandHelper.ReadMediaData(path);
        var diskStream = new MemoryStream(diskData);
        var rigidDiskBlock = await RigidDiskBlockReader.Read(diskStream);
        return await SmartFileSystemVolume.Mount(diskStream, rigidDiskBlock.PartitionBlocks.First());
    }
}
