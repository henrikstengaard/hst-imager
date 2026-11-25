using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public class GivenFullRepairCommand : RecoveryTestBase
{
    private async Task<(Hst.Core.Result Result, RecoveryReport Report)> FullRepair(
        TestCommandHelper testCommandHelper, string path)
    {
        RecoveryReport report = null;
        var command = new FullRepairCommand(new NullLogger<FullRepairCommand>(), testCommandHelper, [], path,
            LogPath);
        command.RecoveryReportRead += (_, args) => report = args.Report;
        var result = await command.Execute(CancellationToken.None);
        return (result, report);
    }

    [Fact]
    public async Task When_FullRepairingValidDisk_Then_PartitionTableAndFileSystemsAreChecked()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // act - full repair disk
        var (result, report) = await FullRepair(testCommandHelper, mediaPath);

        // assert - partition table and file system are checked and valid
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(RecoveryOperation.FullRepair, report.Operation);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(1, report.PartitionTablesFound);
        Assert.Equal(1, report.PartitionsFound);
        Assert.Equal(1, report.FileSystemsChecked);
        Assert.NotNull(report.DirectoriesFound);
    }

    [Fact]
    public async Task When_FullRepairingDiskWithDamagedRdbAndFileSystem_Then_BothAreRepaired()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt fast file system root block checksum and rigid disk block checksum
        var rootBlockOffset = await FindFastFileSystemRootBlockOffset(testCommandHelper, mediaPath);
        await Corrupt(testCommandHelper, mediaPath, rootBlockOffset + 20);
        await Corrupt(testCommandHelper, mediaPath, 8);

        // act - full repair disk
        var (result, report) = await FullRepair(testCommandHelper, mediaPath);

        // assert - rigid disk block and file system are repaired
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(report.Written);
        Assert.Equal(1, report.FileSystemsChecked);
        Assert.Equal(0, report.IssuesNotFixed);
        Assert.Contains(report.Issues, x => x.Message.StartsWith("DH0: RootBlockChecksum") && x.Fixed);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));

        // assert - log contains full repair, partition table repair and file system repair
        var log = await File.ReadAllTextAsync(LogPath);
        Assert.Contains("Operation: FullRepair", log);
        Assert.Contains("Operation: PartitionTableRepair", log);
        Assert.Contains("Operation: FileSystemRepair", log);

        // assert - scanning partition table and file system after full repair are valid
        var (_, partitionTableReport) = await ScanPartitionTable(testCommandHelper, mediaPath);
        Assert.Equal(0, partitionTableReport.Errors);
        var (_, fileSystemReport) = await ScanFileSystem(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));
        Assert.True(fileSystemReport.IsValid, string.Join(Environment.NewLine, fileSystemReport.Issues));
    }

    [Fact]
    public async Task When_FullRepairingPartitionPath_Then_ErrorIsReturned()
    {
        // arrange - create dos3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);

        // act - full repair partition path
        var (result, _) = await FullRepair(testCommandHelper, Path.Combine(mediaPath, "rdb", "dh0"));

        // assert - error is returned
        Assert.True(result.IsFaulted);
        Assert.Contains("Invalid path", result.Error.Message);
    }
}
