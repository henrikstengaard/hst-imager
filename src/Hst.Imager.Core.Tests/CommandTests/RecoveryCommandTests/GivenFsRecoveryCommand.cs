using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public class GivenFsRecoveryCommand : RecoveryTestBase
{
    [Fact]
    public async Task When_ScanningValidFastFileSystem_Then_ReportIsValid()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // act - scan file system
        var (result, report) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - file system is valid
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.StartsWith("FFS", report.TargetType);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
    }

    [Fact]
    public async Task When_ScanningFastFileSystemWithInvalidRootBlockChecksum_Then_ErrorIsReported()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt root block checksum
        var rootBlockOffset = await FindFastFileSystemRootBlockOffset(testCommandHelper, mediaPath);
        await Corrupt(testCommandHelper, mediaPath, rootBlockOffset + 20);

        // act - scan file system using partition number
        var (result, report) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "1"));

        // assert - root block checksum error is reported
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, x => x.Message.Contains("RootBlockChecksum"));
    }

    [Fact]
    public async Task When_RepairingFastFileSystemWithInvalidRootBlockChecksum_Then_ErrorIsFixed()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt root block checksum
        var rootBlockOffset = await FindFastFileSystemRootBlockOffset(testCommandHelper, mediaPath);
        await Corrupt(testCommandHelper, mediaPath, rootBlockOffset + 20);

        // act - repair file system
        var (repairResult, repairReport) =
            await RepairFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - repair fixed error
        Assert.True(repairResult.IsSuccess, repairResult.Error?.Message);
        Assert.True(repairReport.Written);
        Assert.True(repairReport.Fixed > 0);
        Assert.Equal(0, repairReport.IssuesNotFixed);
        Assert.True(repairReport.IsValid, string.Join(Environment.NewLine, repairReport.Issues));

        // assert - scanning repaired file system is valid
        var (_, scanReport) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));
        Assert.True(scanReport.IsValid, string.Join(Environment.NewLine, scanReport.Issues));
    }

    [Fact]
    public async Task When_ScanningValidPfs3FileSystem_Then_ReportIsValid()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // act - scan file system
        var (result, report) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - file system is valid
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.StartsWith("PFS3", report.TargetType);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.NotNull(report.DirectoriesFound);
        Assert.Equal(0, report.FilesFound);
        Assert.Null(report.PartitionTablesFound);
    }

    [Fact]
    public async Task When_RepairingValidPfs3FileSystem_Then_NoChangesAreWritten()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // act - repair file system
        var (result, report) = await RepairFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - file system is valid and nothing is written
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.False(report.Written);
    }

    [Fact]
    public async Task When_ScanningPfs3FileSystemWithCorruptRootBlock_Then_ErrorIsReported()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt root block, which is located at block 2 in partition
        var (partitionOffset, _) = await GetFirstPartitionOffset(testCommandHelper, mediaPath);
        await Corrupt(testCommandHelper, mediaPath, partitionOffset + 2 * 512, 512);

        // act - scan file system
        var (result, report) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - root block error is reported
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(report.IsValid);
    }

    [Fact]
    public async Task When_ScanningUnsupportedPath_Then_ErrorIsReturned()
    {
        // arrange - create mbr disk with fat formatted partition
        var mediaPath = $"mbr_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

        // act - scan fat file system
        var (result, _) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "mbr", "1"));

        // assert - error is returned
        Assert.True(result.IsFaulted);
        Assert.Contains("only supported for Amiga file systems", result.Error.Message);
    }

    [Theory]
    [InlineData(UaeMetadata.UaeFsDb)]
    [InlineData(UaeMetadata.UaeMetafile)]
    public async Task When_SalvagingPfs3FileSystem_Then_DirectoriesAndFilesAreSalvagedWithUaeMetadata(
        UaeMetadata uaeMetadata)
    {
        // arrange - create pfs3 formatted rdb disk with directories and files
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var outputPath = AddCleanupPath($"salvage_{Guid.NewGuid()}");
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await CreatePfs3Content(testCommandHelper, mediaPath);

        // act - salvage file system
        var (result, report) = await Execute(new FsSalvageCommand(new NullLogger<FsSalvageCommand>(),
            testCommandHelper, [], Path.Combine(mediaPath, "rdb", "dh0"), outputPath, uaeMetadata, true, LogPath));

        // assert - directories and files are salvaged
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(report.Salvage);
        Assert.Equal(2, report.Salvage.Files);
        Assert.Equal(1, report.Salvage.Directories);
        Assert.True(File.Exists(Path.Combine(outputPath, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(outputPath, "dir1", "file2.txt")));
        Assert.Equal("hello"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(outputPath, "file1.txt")));

        // assert - uae metadata is written
        if (uaeMetadata == UaeMetadata.UaeFsDb)
        {
            Assert.True(File.Exists(Path.Combine(outputPath, "_UAEFSDB.___")));
        }
        else
        {
            Assert.True(File.Exists(Path.Combine(outputPath, "file1.txt.uaem")));
        }

        // assert - log contains salvaged files
        var log = await File.ReadAllTextAsync(LogPath);
        Assert.Contains("file1.txt", log);
        Assert.Contains("dir1/file2.txt", log);
        Assert.Contains("salvaged comment", log);
    }

    [Fact]
    public async Task When_SalvagingFastFileSystemInAdf_Then_DirectoriesAndFilesAreSalvaged()
    {
        // arrange - create dos3 formatted adf with directories and files
        var adfPath = AddCleanupPath($"floppy_{Guid.NewGuid()}.adf");
        var outputPath = AddCleanupPath($"salvage_{Guid.NewGuid()}");
        await CreateDos3FormattedAdf(adfPath);
        await CreateDos3AdfFiles(adfPath);

        // act - salvage adf using local command helper
        using var commandHelper = new Commands.CommandHelper(new NullLogger<Commands.ICommandHelper>(), false);
        var (result, report) = await Execute(new FsSalvageCommand(new NullLogger<FsSalvageCommand>(),
            commandHelper, [], adfPath, outputPath, UaeMetadata.UaeFsDb, true, LogPath));

        // assert - directories and files are salvaged
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("FFS (ADF)", report.TargetType);
        Assert.True(File.Exists(Path.Combine(outputPath, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(outputPath, "dir1", "dir2", "file4.txt")));
    }

    [Fact]
    public async Task When_SalvagingWithoutUaeMetadata_Then_ErrorIsReturned()
    {
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // act - salvage with no uae metadata
        var (result, _) = await Execute(new FsSalvageCommand(new NullLogger<FsSalvageCommand>(),
            testCommandHelper, [], "disk.vhd", "output", UaeMetadata.None, true, LogPath));

        // assert - error is returned
        Assert.True(result.IsFaulted);
        Assert.Contains("UAE metadata", result.Error.Message);
    }

    [Fact]
    public async Task When_RepairingPfs3FileSystemWithInvalidFileType_Then_ErrorIsFixed()
    {
        // arrange - create pfs3 formatted rdb disk with directories and files
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await CreatePfs3Content(testCommandHelper, mediaPath);

        // arrange - set invalid file type in directory entry for file1.txt
        await CorruptPfs3DirEntryType(testCommandHelper, mediaPath, "file1.txt");

        // arrange - scan reports error
        var (_, scanReport) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));
        Assert.False(scanReport.IsValid, await File.ReadAllTextAsync(LogPath));

        // act - repair file system
        var (result, report) = await RepairFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - all errors are fixed
        var log = await File.ReadAllTextAsync(LogPath);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(report.Fixed > 0, log);
        Assert.True(report.IssuesNotFixed == 0, log);
        Assert.Contains("Invalid filetype", log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Verifying repaired file system", log);
        Assert.Contains("0 error(s) left after repair", log);
        Assert.True(report.IsValid, log);

        // assert - scanning repaired file system is valid
        var (_, rescanReport) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));
        Assert.True(rescanReport.IsValid, await File.ReadAllTextAsync(LogPath));
    }

    /// <summary>
    /// Set invalid type in pfs3 directory entry with name. Directory entry has next (1 byte), type (1 byte), anode
    /// (4 bytes), file size (4 bytes), creation day, minute and tick (2 bytes each), protection (1 byte) and name
    /// length (1 byte) followed by name.
    /// </summary>
    private static async Task CorruptPfs3DirEntryType(TestCommandHelper testCommandHelper, string mediaPath,
        string name)
    {
        var (partitionOffset, partitionBlock) = await GetFirstPartitionOffset(testCommandHelper, mediaPath);
        var size = (int)((partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * partitionBlock.Surfaces *
                         partitionBlock.BlocksPerTrack * 512);
        var data = await TestHelper.ReadData(testCommandHelper, mediaPath, partitionOffset, size);
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        var pattern = new[] { (byte)nameBytes.Length }.Concat(nameBytes).ToArray();

        for (var i = 16; i < data.Length - pattern.Length; i++)
        {
            if (!data.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                continue;
            }

            // directory entry starts 17 bytes before name length, type is second byte
            await TestHelper.WriteData(testCommandHelper, mediaPath, partitionOffset + i - 17 + 1, [0x55]);
            return;
        }

        throw new IOException($"Directory entry '{name}' not found");
    }

    private static async Task CreatePfs3Content(TestCommandHelper testCommandHelper, string mediaPath)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(mediaPath);
        using var media = mediaResult.Value;
        await using var volume = await TestHelper.MountPfs3Volume(media.Stream);
        await volume.CreateFile("file1.txt");
        await using (var stream = await volume.OpenFile("file1.txt", Hst.Amiga.FileSystems.FileMode.Write, true))
        {
            await stream.WriteAsync("hello"u8.ToArray(), CancellationToken.None);
        }

        // set comment, which requires uae metadata
        await volume.SetComment("file1.txt", "salvaged comment", CancellationToken.None);

        await volume.CreateDirectory("dir1");
        await volume.ChangeDirectory("dir1");
        await volume.CreateFile("file2.txt");
    }
}
