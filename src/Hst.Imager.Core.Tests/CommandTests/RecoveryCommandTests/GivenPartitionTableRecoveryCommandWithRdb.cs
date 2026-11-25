using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public class GivenPartitionTableRecoveryCommandWithRdb : RecoveryTestBase
{
    [Fact]
    public async Task When_ScanningValidRigidDiskBlock_Then_ReportIsValid()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // act - scan partition table
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath);

        // assert - scan succeeded and rigid disk block is valid
        Assert.True(result.IsSuccess);
        Assert.NotNull(report);
        Assert.Equal("RDB", report.TargetType);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(RecoveryOperation.PartitionTableScan, report.Operation);
    }

    [Fact]
    public async Task When_ScanningRigidDiskBlockWithInvalidChecksum_Then_ErrorIsReported()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt rigid disk block checksum
        await Corrupt(testCommandHelper, mediaPath, 8);

        // act - scan partition table with rdb path
        var (result, report) = await ScanPartitionTable(testCommandHelper, Path.Combine(mediaPath, "rdb"));

        // assert - scan succeeded and errors are reported
        Assert.True(result.IsSuccess);
        Assert.NotNull(report);
        Assert.False(report.IsValid);
        Assert.True(report.Errors > 0);
    }

    [Fact]
    public async Task When_RepairingRigidDiskBlockWithInvalidChecksum_Then_RigidDiskBlockIsRepaired()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - corrupt rigid disk block checksum
        await Corrupt(testCommandHelper, mediaPath, 8);

        // act - repair partition table
        var (repairResult, repairReport) = await RepairPartitionTable(testCommandHelper, mediaPath,
            Commands.PartitionTableType.RigidDiskBlock);

        // assert - repair succeeded and changes were written
        Assert.True(repairResult.IsSuccess);
        Assert.NotNull(repairReport);
        Assert.True(repairReport.Written, string.Join(Environment.NewLine, repairReport.Issues));

        // assert - rigid disk block can be read with partition
        var rigidDiskBlock = await ReadRigidDiskBlock(testCommandHelper, mediaPath);
        Assert.NotNull(rigidDiskBlock);
        Assert.Single(rigidDiskBlock.PartitionBlocks);
        Assert.Equal("DH0", rigidDiskBlock.PartitionBlocks.First().DriveName);

        // assert - scanning repaired rigid disk block is valid
        var (scanResult, scanReport) = await ScanPartitionTable(testCommandHelper, mediaPath);
        Assert.True(scanResult.IsSuccess);
        Assert.True(scanReport.IsValid, string.Join(Environment.NewLine, scanReport.Issues));
    }

    [Fact]
    public async Task When_ScanningPartitionTable_Then_LogFileIsWritten()
    {
        // arrange - create pfs3 formatted rdb disk
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var testCommandHelper = new TestCommandHelper();
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // act - scan partition table
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath);

        // assert - log file is written with header and summary
        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(LogPath), report.LogPath);
        Assert.True(File.Exists(LogPath));
        var log = await File.ReadAllTextAsync(LogPath);
        Assert.Contains("Hst Imager recovery log", log);
        Assert.Contains($"Target: {mediaPath}", log);
        Assert.Contains("Summary", log);
        Assert.Contains("Result: Valid", log);
    }
}
