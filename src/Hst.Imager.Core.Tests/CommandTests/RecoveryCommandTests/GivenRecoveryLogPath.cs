using System;
using System.IO;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public class GivenRecoveryLogPath
{
    private static readonly DateTime Timestamp = new(2026, 11, 24, 13, 14, 15);

    [Fact]
    public void When_GettingDefaultForImageFile_Then_LogPathIsNextToImageFile()
    {
        // arrange - image file path
        var imagePath = Path.Combine(Path.GetTempPath(), "images", "disk.hdf");

        // act - get default log path
        var logPath = RecoveryLogPath.GetDefault(imagePath, false, RecoveryOperation.FileSystemScan, Timestamp);

        // assert - log path is next to image file
        Assert.Equal(Path.Combine(Path.GetTempPath(), "images", "disk.hdf.fs-scan-20261124-131415.log"), logPath);
    }

    [Fact]
    public void When_GettingDefaultForPhysicalDrive_Then_LogPathIsInHomeDirectory()
    {
        // arrange - physical drive path
        const string physicalDrivePath = @"\\.\PhysicalDrive2";

        // act - get default log path
        var logPath = RecoveryLogPath.GetDefault(physicalDrivePath, true, RecoveryOperation.PartitionTableRepair,
            Timestamp);

        // assert - log path is in home directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(homeDir, Path.GetDirectoryName(logPath));
        Assert.EndsWith("partitiontable-repair-20261124-131415.log", logPath);
        Assert.Contains("physicaldrive2", Path.GetFileName(logPath));
    }
}
