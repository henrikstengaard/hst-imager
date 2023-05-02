namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Apis;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using Models;

    public class WindowsPhysicalDriveManager : IPhysicalDriveManager
    {
        private readonly ILogger<WindowsPhysicalDriveManager> logger;

        public WindowsPhysicalDriveManager(ILogger<WindowsPhysicalDriveManager> logger)
        {
            this.logger = logger;
        }

        public Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new NotSupportedException("Windows physical drive manager is not running on Windows environment");
            }

            return Task.FromResult(GetPhysicalDrivesUsingKernel32(all));
        }

        private IEnumerable<IPhysicalDrive> GetPhysicalDrivesUsingKernel32(bool all = false)
        {
            // iterate drive letters and get their relation to physical drives
            var physicalDriveLettersIndex = new Dictionary<uint, List<string>>();
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                var driveName = drive.Name[..2];
                var drivePath = $"\\\\.\\{driveName}";
                using var win32RawDisk = new Win32RawDisk(drivePath, false, true);
                if (win32RawDisk.IsInvalid())
                {
                    continue;
                }

                var diskExtendsResult = win32RawDisk.DiskExtends();
                if (!physicalDriveLettersIndex.ContainsKey(diskExtendsResult.DiskNumber))
                {
                    physicalDriveLettersIndex.Add(diskExtendsResult.DiskNumber, new List<string>());
                }

                physicalDriveLettersIndex[diskExtendsResult.DiskNumber].Add(driveName);
            }

            // iterate physical drives, get media type from geometry, get name from storage property query and size 
            var physicalDrives = new List<IPhysicalDrive>();
            for (uint i = 0; i < PhysicalDrive.MAX_NUMBER_OF_DRIVES; i++)
            {
                var physicalDrivePath = $"\\\\.\\PhysicalDrive{i}";
                
                try
                {
                    using var win32RawDisk = new Win32RawDisk(physicalDrivePath, false, true);
                    if (win32RawDisk.IsInvalid())
                    {
                        continue;
                    }

                    var diskGeometryExResult = win32RawDisk.DiskGeometryEx();
                    var storagePropertyQueryResult = win32RawDisk.StoragePropertyQuery();
                    var name = string.Join(" ",
                        new[] { storagePropertyQueryResult.VendorId, storagePropertyQueryResult.ProductId }.Where(x =>
                            !string.IsNullOrWhiteSpace(x)));
                    var size = win32RawDisk.Size();

                    var driveLetters =
                        physicalDriveLettersIndex.TryGetValue(i, out var value) ? value : new List<string>();
                    physicalDrives.Add(new WindowsPhysicalDrive(physicalDrivePath, diskGeometryExResult.MediaType, name,
                        size, driveLetters));
                }
                catch (Win32Exception e)
                {
                    // ignore physical drive, if win32 not ready error occurs
                    if (e.NativeErrorCode == 21)
                    {
                        continue;
                    }

                    throw;
                }
                catch (Exception e)
                {
                    throw new IOException($"Failed to open physical drive '{physicalDrivePath}'. Close Windows Explorer, Antivirus or other applications reading or writing the physical drive and retry!", e);
                }
            }

            if (!all)
            {
                physicalDrives = physicalDrives.Where(x => x.Type != "FixedMedia").ToList();
            }

            foreach (var physicalDrive in physicalDrives)
            {
                logger.LogDebug($"Physical drive: Path '{physicalDrive.Path}', Name '{physicalDrive.Name}', Type = '{physicalDrive.Type}', Size = '{physicalDrive.Size}'");
            }
            
            return physicalDrives;
        }

        private async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrivesUsingWmic(bool all = false)
        {
            var wmicDiskDriveListCsv = await GetWmicDiskDriveListCsv();
            var wmicWin32DiskDriveToDiskPartitionsCsv = await GetWmicWin32DiskDriveToDiskPartitionPath();
            var wmicWin32LogicalDiskToPartitionsCsv = await GetWmicWin32LogicalDiskToPartitionPath();

            var wmicDiskDrives = WmicReader.ParseWmicCsv<WmicDiskDrive>(wmicDiskDriveListCsv).ToList();
            var wmicDiskDriveToDiskPartitions =
                WmicReader.ParseWmicDiskDriveToDiskPartitions(wmicWin32DiskDriveToDiskPartitionsCsv).ToList();
            var wmicLogicalDiskToPartitions =
                WmicReader.ParseWmicLogicalDiskToPartitions(wmicWin32LogicalDiskToPartitionsCsv).ToList();

            if (!all)
            {
                wmicDiskDrives = wmicDiskDrives.Where(x =>
                        x.MediaType.Equals("Removable Media", StringComparison.OrdinalIgnoreCase) ||
                        x.MediaType.Equals("External hard disk media", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            
            return wmicDiskDrives.Select(x =>
                CreatePhysicalDrive(x, wmicDiskDriveToDiskPartitions, wmicLogicalDiskToPartitions));
        }

        private IPhysicalDrive CreatePhysicalDrive(WmicDiskDrive wmicDiskDrive,
            IEnumerable<WmicDiskDriveToDiskPartition> wmicDiskDriveToDiskPartitions,
            IEnumerable<WmicLogicalDiskToPartition> wmicLogicalDiskToPartitions)
        {
            var driveLetters = wmicDiskDriveToDiskPartitions.Where(x => x.Antecedent == wmicDiskDrive.Name)
                .Join(wmicLogicalDiskToPartitions, disk => disk.Dependent, logical => logical.Antecedent,
                    (_, logical) => logical.Dependent).ToList();

            long size;
            using (var win32RawDisk = new Win32RawDisk(wmicDiskDrive.Name))
            {
                size = win32RawDisk.Size();
            }

            return new WindowsPhysicalDrive(wmicDiskDrive.Name, wmicDiskDrive.MediaType, wmicDiskDrive.Model,
                size, driveLetters);
        }

        private async Task<string> GetWmicDiskDriveListCsv()
        {
            var output = await "wmic".RunProcessAsync("diskdrive list /format:csv");
            logger.LogDebug(output);
            return output;
        }

        private async Task<string> GetWmicWin32DiskDriveToDiskPartitionPath()
        {
            var output = await "wmic".RunProcessAsync("path Win32_DiskDriveToDiskPartition get * /format:csv");
            logger.LogDebug(output);
            return output;
        }

        private async Task<string> GetWmicWin32LogicalDiskToPartitionPath()
        {
            var output = await "wmic".RunProcessAsync("path Win32_LogicalDiskToPartition get * /format:csv");
            logger.LogDebug(output);
            return output;
        }
    }
}