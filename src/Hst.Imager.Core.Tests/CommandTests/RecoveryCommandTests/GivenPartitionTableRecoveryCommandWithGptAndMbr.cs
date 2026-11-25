using System;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public class GivenPartitionTableRecoveryCommandWithGptAndMbr : RecoveryTestBase
{
    private const long DiskSize = 10 * 1024 * 1024;

    [Fact]
    public async Task When_ScanningValidGuidPartitionTable_Then_ReportIsValid()
    {
        // arrange - create gpt disk with fat formatted partition
        var mediaPath = $"gpt_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateGptFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);

        // act - scan partition table
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath);

        // assert - guid partition table is valid
        Assert.True(result.IsSuccess);
        Assert.Equal("GPT", report.TargetType);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Contains(report.Stats, x => x.Key == "GPT partitions" && x.Value == "1");
        Assert.Equal(1, report.PartitionTablesFound);
        Assert.Equal(1, report.PartitionsFound);
    }

    [Fact]
    public async Task When_ScanningGuidPartitionTableWithCorruptPrimaryHeader_Then_ErrorIsReported()
    {
        // arrange - create gpt disk with fat formatted partition
        var mediaPath = $"gpt_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateGptFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);

        // arrange - corrupt primary header crc
        await Corrupt(testCommandHelper, mediaPath, 512 + 16);

        // act - scan partition table
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath);

        // assert - primary header error is reported
        Assert.True(result.IsSuccess);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, x => x.Message.Contains("Primary header") && x.Message.Contains("CRC32"));
    }

    [Fact]
    public async Task When_RepairingGuidPartitionTableWithCorruptPrimaryHeader_Then_PrimaryIsRestoredFromBackup()
    {
        // arrange - create gpt disk with fat formatted partition
        var mediaPath = $"gpt_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateGptFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);
        var expectedPrimary = await TestHelper.ReadData(testCommandHelper, mediaPath, 512, 512 * 33);

        // arrange - corrupt primary header and partition entries
        await Corrupt(testCommandHelper, mediaPath, 512, 512 * 2);

        // act - repair partition table
        var (repairResult, repairReport) = await RepairPartitionTable(testCommandHelper, mediaPath);

        // assert - repair succeeded and primary header is restored
        Assert.True(repairResult.IsSuccess);
        Assert.True(repairReport.Written);
        Assert.True(repairReport.IsValid, string.Join(Environment.NewLine, repairReport.Issues));
        Assert.True(repairReport.Fixed > 0);
        Assert.Equal(0, repairReport.IssuesNotFixed);
        var actualPrimary = await TestHelper.ReadData(testCommandHelper, mediaPath, 512, 512 * 33);
        Assert.Equal(expectedPrimary, actualPrimary);

        // assert - scanning repaired guid partition table is valid
        var (_, scanReport) = await ScanPartitionTable(testCommandHelper, mediaPath);
        Assert.True(scanReport.IsValid, string.Join(Environment.NewLine, scanReport.Issues));
    }

    [Fact]
    public async Task When_RepairingGuidPartitionTableWithCorruptBackupHeader_Then_BackupIsRestoredFromPrimary()
    {
        // arrange - create gpt disk with fat formatted partition
        var mediaPath = $"gpt_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateGptFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);
        var expectedBackup = await TestHelper.ReadData(testCommandHelper, mediaPath, DiskSize - 512 * 33, 512 * 33);

        // arrange - corrupt backup header
        await Corrupt(testCommandHelper, mediaPath, DiskSize - 512, 8);

        // act - repair partition table
        var (repairResult, repairReport) = await RepairPartitionTable(testCommandHelper, mediaPath,
            PartitionTableType.GuidPartitionTable);

        // assert - backup header is restored
        Assert.True(repairResult.IsSuccess);
        Assert.True(repairReport.Written);
        var actualBackup = await TestHelper.ReadData(testCommandHelper, mediaPath, DiskSize - 512 * 33, 512 * 33);
        Assert.Equal(expectedBackup, actualBackup);
    }

    [Fact]
    public async Task When_ScanningValidMasterBootRecord_Then_ReportIsValid()
    {
        // arrange - create mbr disk with fat formatted partition
        var mediaPath = $"mbr_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);

        // act - scan partition table
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath);

        // assert - master boot record is valid
        Assert.True(result.IsSuccess);
        Assert.Equal("MBR", report.TargetType);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Contains(report.Stats, x => x.Key == "MBR partitions" && x.Value == "1");
        Assert.Equal(1, report.PartitionTablesFound);
        Assert.Equal(1, report.PartitionsFound);
    }

    [Fact]
    public async Task When_ScanningMasterBootRecordWithOverlappingPartitions_Then_ErrorIsReported()
    {
        // arrange - create mbr disk with fat formatted partition
        var mediaPath = $"mbr_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);

        // arrange - add second partition entry overlapping first partition
        var mbr = await TestHelper.ReadData(testCommandHelper, mediaPath, 0, 512);
        var entry = mbr.Skip(446).Take(16).ToArray();
        entry[0] = 0;
        await TestHelper.WriteData(testCommandHelper, mediaPath, 446 + 16, entry);

        // act - scan partition table with mbr type
        var (result, report) = await ScanPartitionTable(testCommandHelper, mediaPath,
            PartitionTableType.MasterBootRecord);

        // assert - overlap error is reported
        Assert.True(result.IsSuccess);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, x => x.Message.Contains("overlaps"));
    }

    [Fact]
    public async Task When_ScanningMasterBootRecordWithInvalidSignature_Then_ErrorIsReported()
    {
        // arrange - create mbr disk with fat formatted partition
        var mediaPath = $"mbr_{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        await CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, DiskSize);

        // arrange - corrupt signature
        await Corrupt(testCommandHelper, mediaPath, 510, 2);

        // act - scan partition table with mbr path
        var (result, report) = await ScanPartitionTable(testCommandHelper, System.IO.Path.Combine(mediaPath, "mbr"));

        // assert - signature error is reported
        Assert.True(result.IsSuccess);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, x => x.Message.Contains("signature"));
    }
}
