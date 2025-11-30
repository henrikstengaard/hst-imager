using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingMultipleDirectoriesAndFilesFromAndToSameRdbMedia : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSameRdbMedia_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "copied"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await RdbTestHelper.CreateDirectory(testCommandHelper, mediaPath, 0, ["copied"]);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 3 entries
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            Assert.Equal(3, entries.Count);
            Assert.Equal(["copied", "dir1", "dir2"], entries.Select(x => x.Name).Order());

            // assert - copied directory contains 2 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["copied"])).ToList();
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - copied, dir3 directory contains 0 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["copied", "dir3"])).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameRdbMediaBetweenTwoPartitions_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb", "1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "2"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            // arrange - create rdb disk with 2 partitions
            await TestHelper.CreateRdbDisk(testCommandHelper, mediaPath, 100.MB());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, mediaPath, 45.MB());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, mediaPath, 45.MB());
            await RdbTestHelper.Pfs3FormatRdbPartition(testCommandHelper, mediaPath, 0);
            await RdbTestHelper.Pfs3FormatRdbPartition(testCommandHelper, mediaPath, 1, "Work");

            // arrange - create directories and files in partition 1
            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - partition 2 root directory contains 2 entries
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                1, [])).ToList();
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - partition 2, dir3 directory contains 0 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                1, ["dir3"])).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingFromAndToSameRdbMediaWithCyclicPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1", "*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "copied"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await RdbTestHelper.CreateDirectory(testCommandHelper, mediaPath, 0, ["copied"]);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned cyclic error
            Assert.True(result.IsFaulted);
            Assert.IsType<CyclicPathError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingFromAndToSameRdbMediaWithSelfCopyPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb", "1", "dir1", "file1.txt"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir1", "file1.txt"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned self copy error
            Assert.True(result.IsFaulted);
            Assert.IsType<SelfCopyError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameRdbMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir4"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            // arrange - create pfs3 formatted disk
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameRdbMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1","*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir4", "dir5"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            // arrange - create pfs3 formatted disk
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}

public class GivenFsCopyCommandCopyingMultipleDirectoriesAndFilesFromAndToSamePiStormRdbMedia : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSamePiStormRdbMedia_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "rdb", "1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "rdb", "1", "copied"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create pistorm rdb disk
            await CreatePiStormRdbDisk(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - pistormrdb partition 1, root directory contains 3 entries
            var entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "1", "rdb", "1"))).ToList();
            Assert.Equal(3, entries.Count);
            Assert.Equal(["copied", "dir1", "dir2"], entries.Select(x => x.Name).Order());

            // assert - pistormrdb partition 1, copied directory contains 2 entries
            entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "1", "rdb", "1", "copied"))).ToList();
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - pistormrdb partition 1, copied, dir3 directory contains 0 entries
            entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "1", "rdb", "1", "copied", "dir3"))).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSamePiStormRdbMediaBetweenTwoPartitions_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "rdb", "1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "2", "rdb", "1", "copied"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create pistorm rdb disk
            await CreatePiStormRdbDisk(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - pistormrdb partition 2, root directory contains 3 entries
            var entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "2", "rdb", "1"))).ToList();
            Assert.Equal(3, entries.Count);
            Assert.Equal(["copied", "dir1", "dir2"], entries.Select(x => x.Name).Order());

            // assert - pistormrdb partition 2, copied directory contains 2 entries
            entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "copied"))).ToList();
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - pistormrdb partition 2, copied, dir3 directory contains 0 entries
            entries = (await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "copied", "dir3"))).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    private static async Task CreatePiStormRdbDisk(TestCommandHelper testCommandHelper, string mediaPath)
    {
        // disk sizes
        var mbrDiskSize = 250.MB();
        var rdbDiskSize = 100.MB();

        // add mbr disk media
        testCommandHelper.AddTestMedia(mediaPath, mbrDiskSize);

        // add rdb disk media
        var rdbDiskPath = $"rdb_{Guid.NewGuid()}.vhd";
        testCommandHelper.AddTestMedia(rdbDiskPath, rdbDiskSize);

        // calculate mbr partition start and end sectors
        var mbrPartition1StartSector = 63;
        var mbrPartition1EndSector = (rdbDiskSize / 512);
        var mbrPartition2StartSector = mbrPartition1EndSector + 1;
        var mbrPartition2EndSector = mbrPartition2StartSector + (rdbDiskSize / 512) - 10;
        
        await MbrTestHelper.CreateMbrDisk(testCommandHelper, mediaPath, mbrDiskSize);
        await MbrTestHelper.AddMbrPartition(testCommandHelper, mediaPath,
            mbrPartition1StartSector, mbrPartition1EndSector, Constants.BiosPartitionTypes.PiStormRdb);
        await MbrTestHelper.AddMbrPartition(testCommandHelper, mediaPath,
            mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);
        
        // arrange - create rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, rdbDiskPath, rdbDiskSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, rdbDiskPath, 45.MB());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, rdbDiskPath, 45.MB());
        await RdbTestHelper.Pfs3FormatRdbPartition(testCommandHelper, rdbDiskPath, 0);
        await RdbTestHelper.Pfs3FormatRdbPartition(testCommandHelper, rdbDiskPath, 1, "Work");

        // arrange - create directories and files in partition 1
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, rdbDiskPath);
        await RdbTestHelper.CreateDirectory(testCommandHelper, rdbDiskPath, 0, ["copied"]);
        
        // get readable media for rdb disk
        var rdbMediaResult = await testCommandHelper.GetReadableMedia([], rdbDiskPath);
        if (!rdbMediaResult.IsSuccess)
        {
            throw new Exception(rdbMediaResult.Error.Message);
        }

        // get writable media for mbr disk
        var mbrMediaResult = await testCommandHelper.GetWritableMedia([], mediaPath);
        if (!mbrMediaResult.IsSuccess)
        {
            throw new Exception(mbrMediaResult.Error.Message);
        }

        // copy rdb media to mbr partition 2 creating pistorm rdb hard disk
        using var mbrMedia = mbrMediaResult.Value;
        var mbrStream = mbrMedia is DiskMedia diskMedia
            ? diskMedia.Disk.Content
            : mbrMedia.Stream;

        using var rdbMedia = rdbMediaResult.Value;
        
        var rdbStream = rdbMedia is DiskMedia rdbDiskMedia
            ? rdbDiskMedia.Disk.Content
            : rdbMedia.Stream;

        await CopyStreamData(rdbStream, 0, mbrStream, 512 * mbrPartition1StartSector);
        await CopyStreamData(rdbStream, 0, mbrStream, 512 * mbrPartition2StartSector);
    }
    
    private static async Task CopyStreamData(Stream srcStream, long srcPosition, Stream destStream, long destPosition)
    {
        srcStream.Seek(srcPosition, SeekOrigin.Begin);
        destStream.Seek(destPosition, SeekOrigin.Begin);
        
        var buffer = new byte[4096];

        int bytesRead;
        do
        {
            bytesRead = await srcStream.ReadAsync(buffer, 0, buffer.Length);
            await destStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead != 0);
    }
}