﻿namespace Hst.Imager.Core.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using PhysicalDrives;
    using Xunit;

    public class GivenWmicReader
    {
        [Fact]
        public async Task WhenParseCsvOutputFromWmicThenWmicDiskDrivesAreReturned()
        {
            var wmicDiskDrives = WmicReader.ParseWmicDiskDrives(await File.ReadAllTextAsync(Path.Combine("TestData", "wmic-DiskDrive.csv"))).ToList();

            Assert.NotNull(wmicDiskDrives);
            Assert.NotEmpty(wmicDiskDrives);
            Assert.Equal(2, wmicDiskDrives.Count);

            var wmicDiskDrive1 = wmicDiskDrives[0];
            Assert.Equal("External hard disk media", wmicDiskDrive1.MediaType);
            Assert.Equal("  SCSI Disk Device", wmicDiskDrive1.Model);
            Assert.Equal("\\\\.\\PHYSICALDRIVE3", wmicDiskDrive1.Name);
            Assert.Equal(120031511040, wmicDiskDrive1.Size);
            Assert.Equal("SCSI", wmicDiskDrive1.InterfaceType);
            
            var wmicDiskDrive2 = wmicDiskDrives[1];
            Assert.Equal("Removable Media", wmicDiskDrive2.MediaType);
            Assert.Equal("SanDisk' Cruzer Fit USB Device", wmicDiskDrive2.Model);
            Assert.Equal("\\\\.\\PHYSICALDRIVE2", wmicDiskDrive2.Name);
            Assert.Equal(15677383680, wmicDiskDrive2.Size);
            Assert.Equal("USB", wmicDiskDrive2.InterfaceType);
        }

        [Fact]
        public async Task WhenParseCsvOutputFromWmicWithoutSizeThenWmicDiskDrivesAreReturned()
        {
            var wmicDiskDrives = WmicReader.ParseWmicDiskDrives(await File.ReadAllTextAsync(Path.Combine("TestData", "wmic-diskdrive-list-0-size.csv"))).ToList();

            Assert.NotNull(wmicDiskDrives);
            Assert.Single(wmicDiskDrives);

            var wmicDiskDrive1 = wmicDiskDrives[0];
            Assert.Null(wmicDiskDrive1.Size);
        }

        [Fact]
        public async Task WhenParseCsvOutputFromWmicWith3DiskDrivesWmicDiskDrivesAreReturned()
        {
            var wmicDiskDrives = WmicReader.ParseWmicDiskDrives(await File.ReadAllTextAsync(Path.Combine("TestData", "wmic_diskdrive_list_error.csv"))).ToList();

            Assert.NotNull(wmicDiskDrives);
            Assert.NotEmpty(wmicDiskDrives);
            Assert.Equal(3, wmicDiskDrives.Count);

            var wmicDiskDrive1 = wmicDiskDrives[0];
            Assert.Equal("Fixed hard disk media", wmicDiskDrive1.MediaType);
            Assert.Equal("Samsung SSD 970 EVO 2TB", wmicDiskDrive1.Model);
            Assert.Equal("\\\\.\\PHYSICALDRIVE0", wmicDiskDrive1.Name);
            Assert.Equal(2000396321280, wmicDiskDrive1.Size);
            Assert.Equal("SCSI", wmicDiskDrive1.InterfaceType);
            
            var wmicDiskDrive2 = wmicDiskDrives[1];
            Assert.Equal("Fixed hard disk media", wmicDiskDrive2.MediaType);
            Assert.Equal("WDC PC SN720 SDAPNTW-512G", wmicDiskDrive2.Model);
            Assert.Equal("\\\\.\\PHYSICALDRIVE1", wmicDiskDrive2.Name);
            Assert.Equal(512105932800, wmicDiskDrive2.Size);
            Assert.Equal("SCSI", wmicDiskDrive2.InterfaceType);
            
            var wmicDiskDrive3 = wmicDiskDrives[2];
            Assert.Equal("", wmicDiskDrive3.MediaType);
            Assert.Equal("Generic STORAGE DEVICE USB Device", wmicDiskDrive3.Model);
            Assert.Equal("\\\\.\\PHYSICALDRIVE2", wmicDiskDrive3.Name);
            Assert.Null(wmicDiskDrive3.Size);
            Assert.Equal("USB", wmicDiskDrive3.InterfaceType);
        }
        
        [Fact]
        public async Task WhenParseCsvOutputFromWmicDiskDriveToDiskPartitionThenWmicDiskDriveToDiskPartitionsAreReturned()
        {
            var wmicDiskDriveToDiskPartitions = WmicReader
                .ParseWmicDiskDriveToDiskPartitions(
                    await File.ReadAllTextAsync(Path.Combine("TestData", "wmic-Win32_DiskDriveToDiskPartition.csv"))).ToList();

            Assert.NotEmpty(wmicDiskDriveToDiskPartitions);
            Assert.Equal(4, wmicDiskDriveToDiskPartitions.Count);
            
            var wmicDiskDriveToDiskPartition1 = wmicDiskDriveToDiskPartitions[0];
            Assert.Equal("\\\\.\\PHYSICALDRIVE0", wmicDiskDriveToDiskPartition1.Antecedent);
            Assert.Equal("Disk #0, Partition #0", wmicDiskDriveToDiskPartition1.Dependent);
            
            var wmicDiskDriveToDiskPartition2 = wmicDiskDriveToDiskPartitions[1];
            Assert.Equal("\\\\.\\PHYSICALDRIVE1", wmicDiskDriveToDiskPartition2.Antecedent);
            Assert.Equal("Disk #1, Partition #0", wmicDiskDriveToDiskPartition2.Dependent);
            
            var wmicDiskDriveToDiskPartition3 = wmicDiskDriveToDiskPartitions[2];
            Assert.Equal("\\\\.\\PHYSICALDRIVE1", wmicDiskDriveToDiskPartition3.Antecedent);
            Assert.Equal("Disk #1, Partition #1", wmicDiskDriveToDiskPartition3.Dependent);
            
            var wmicDiskDriveToDiskPartition4 = wmicDiskDriveToDiskPartitions[3];
            Assert.Equal("\\\\.\\PHYSICALDRIVE1", wmicDiskDriveToDiskPartition4.Antecedent);
            Assert.Equal("Disk #1, Partition #2", wmicDiskDriveToDiskPartition4.Dependent);
        }
        
        [Fact]
        public async Task WhenParseCsvOutputFromWmicLogicalDiskToPartitionThenWmicLogicalDiskToPartitionsAreReturned()
        {
            var wmicLogicalDiskToPartitions = WmicReader
                .ParseWmicLogicalDiskToPartitions(
                    await File.ReadAllTextAsync(Path.Combine("TestData", "wmic-Win32_LogicalDiskToPartition.csv"))).ToList();

            Assert.NotEmpty(wmicLogicalDiskToPartitions);
            Assert.Equal(2, wmicLogicalDiskToPartitions.Count);
            
            var wmicLogicalDiskToPartition1 = wmicLogicalDiskToPartitions[0];
            Assert.Equal("Disk #1, Partition #2", wmicLogicalDiskToPartition1.Antecedent);
            Assert.Equal("C:", wmicLogicalDiskToPartition1.Dependent);
            
            var wmicLogicalDiskToPartition2 = wmicLogicalDiskToPartitions[1];
            Assert.Equal("Disk #0, Partition #0", wmicLogicalDiskToPartition2.Antecedent);
            Assert.Equal("D:", wmicLogicalDiskToPartition2.Dependent);
        }
    }
}