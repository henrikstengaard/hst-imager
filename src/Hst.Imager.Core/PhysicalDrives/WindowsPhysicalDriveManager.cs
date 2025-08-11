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
        private const bool SupportFloppyDrives = false;

        public WindowsPhysicalDriveManager(ILogger<WindowsPhysicalDriveManager> logger)
        {
            this.logger = logger;
        }

        public Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
        {
            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult(Enumerable.Empty<IPhysicalDrive>());
            }

            var physicalDrives = GetPhysicalDrivesUsingKernel32().ToList();

            if (!all)
            {
                physicalDrives = physicalDrives.Where(x => x.Removable).ToList();
            }

            foreach (var physicalDrive in physicalDrives)
            {
                logger.LogDebug(
                    $"Physical drive: Path '{physicalDrive.Path}', Name '{physicalDrive.Name}', Type = '{physicalDrive.Type}', Size = '{physicalDrive.Size}'");
            }

            return Task.FromResult<IEnumerable<IPhysicalDrive>>(physicalDrives);
        }

        protected virtual IEnumerable<IPhysicalDrive> GetPhysicalDrivesUsingKernel32()
        {
            var systemDriveLetter = Environment.SystemDirectory.Substring(0, 2);

            logger.LogDebug($"System drive '{systemDriveLetter}'");

            var physicalDrives = new List<WindowsPhysicalDrive>();
            
            // iterate drive letters and get their relation to physical drives
            var physicalDriveLettersIndex = new Dictionary<uint, List<string>>();
            var drives = DriveInfo.GetDrives().ToList();
            foreach (var drive in drives)
            {
                if (drive.DriveType == DriveType.CDRom)
                {
                    continue;
                }
                
                try
                {
                    var driveName = drive.Name[..2];
                    var drivePath = $"\\\\.\\{driveName}";
                    logger.LogDebug($"Opening win32 raw disk access to drive letter '{driveName}'");

                    using var win32RawDisk = new Win32RawDisk(drivePath, false, true);
                    if (win32RawDisk.IsInvalid())
                    {
                        continue;
                    }
                    
                    if (SupportFloppyDrives &&
                        (driveName.Equals("a:", StringComparison.OrdinalIgnoreCase) ||
                        driveName.Equals("b:", StringComparison.OrdinalIgnoreCase)))
                    {
                        var storagePropertyQueryResult = win32RawDisk.StoragePropertyQuery();
                        var diskGeometry = win32RawDisk.DiskGeometry();
                        var size = diskGeometry.Cylinders * diskGeometry.TracksPerCylinder * diskGeometry.SectorsPerTrack * diskGeometry.BytesPerSector;                        
                        
                        var physicalDrive = new WindowsPhysicalDrive(drive.Name, "Floppy",
                            storagePropertyQueryResult.BusType, drive.Name, size, true, false, [driveName]);

                        physicalDrives.Add(physicalDrive);
                        logger.LogDebug(
                            $"Physical drive: Path '{physicalDrive.Path}', SystemDrive '{physicalDrive.SystemDrive}', Name '{physicalDrive.Name}', Type = '{physicalDrive.Type}', BusType = '{physicalDrive.BusType}', Size = '{physicalDrive.Size}'");
                        continue;
                    }

                    var diskExtendsResult = win32RawDisk.DiskExtends();
                    logger.LogDebug(
                        $"Disk extends returned disk number {diskExtendsResult.DiskNumber} for drive letter '{driveName}'");

                    if (!physicalDriveLettersIndex.ContainsKey(diskExtendsResult.DiskNumber))
                    {
                        physicalDriveLettersIndex.Add(diskExtendsResult.DiskNumber, new List<string>());
                    }

                    physicalDriveLettersIndex[diskExtendsResult.DiskNumber].Add(driveName);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            // iterate physical drives, get media type from geometry, get name from storage property query and size 
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

                    logger.LogDebug($"Opening win32 raw disk access to physical drive '{physicalDrivePath}'");

                    if (!win32RawDisk.Verify())
                    {
                        logger.LogDebug($"Verify physical drive '{physicalDrivePath}' returned false, skip");
                        continue;
                    }

                    var diskGeometryExResult = win32RawDisk.DiskGeometryEx();
                    var storagePropertyQueryResult = win32RawDisk.StoragePropertyQuery();
                    var name = GetPhysicalDriveName(storagePropertyQueryResult);
                    var size = win32RawDisk.Size();

                    var driveLetters =
                        physicalDriveLettersIndex.TryGetValue(i, out var value) ? value : new List<string>();
                    var systemDrive = driveLetters.Contains(systemDriveLetter);
                    var physicalDrive = new WindowsPhysicalDrive(physicalDrivePath, diskGeometryExResult.MediaType,
                        storagePropertyQueryResult.BusType, name, size,
                        IsRemovable(diskGeometryExResult.MediaType, storagePropertyQueryResult.BusType), 
                        systemDrive, driveLetters);
                    physicalDrives.Add(physicalDrive);
                    logger.LogDebug(
                        $"Physical drive: Path '{physicalDrive.Path}', SystemDrive '{systemDrive}', Name '{physicalDrive.Name}', Type = '{physicalDrive.Type}', BusType = '{physicalDrive.BusType}', Size = '{physicalDrive.Size}'");
                }
                catch (Win32Exception e)
                {
                    // skip physical drive, if win32 not ready error occurs
                    if (e.NativeErrorCode == 21)
                    {
                        logger.LogDebug(
                            $"Skip win32 raw disk access to physical drive '{physicalDrivePath}' returning not ready error");
                        continue;
                    }

                    throw;
                }
                catch (Exception e)
                {
                    throw new IOException(
                        $"Failed to open physical drive '{physicalDrivePath}'. Close Windows Explorer, Antivirus or other applications accessing the physical drive '{physicalDrivePath}' and retry!",
                        e);
                }
            }

            return physicalDrives;
        }

        private static string GetPhysicalDriveName(StoragePropertyQueryResult storagePropertyQueryResult)
        {
            var nameParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(storagePropertyQueryResult.VendorId))
            {
                nameParts.Add(storagePropertyQueryResult.VendorId);
            }

            if (!string.IsNullOrWhiteSpace(storagePropertyQueryResult.ProductId))
            {
                nameParts.Add(storagePropertyQueryResult.ProductId);
            }

            return string.Join(" ", nameParts);
        }

        private static bool IsRemovable(string type, string busType)
        {
            return (type == "RemovableMedia" && busType != "BusTypeSata") ||
                   (type == "FixedMedia" && (busType == "BusTypeUsb" ||
                                             busType == "BusTypeSd" ||
                                             busType == "BusTypeMmc"));
        }

        private async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrivesUsingWmic(bool all = false)
        {
            var wmicDiskDriveListCsv = await GetWmicDiskDriveListCsv();
            var wmicWin32DiskDriveToDiskPartitionsCsv = await GetWmicWin32DiskDriveToDiskPartitionPath();
            var wmicWin32LogicalDiskToPartitionsCsv = await GetWmicWin32LogicalDiskToPartitionPath();

            var wmicDiskDrives = WmicReader.ParseWmicDiskDrives(wmicDiskDriveListCsv).ToList();
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

            return new WindowsPhysicalDrive(wmicDiskDrive.Name, wmicDiskDrive.MediaType, wmicDiskDrive.InterfaceType,
                wmicDiskDrive.Model,
                size, false, false, driveLetters);
        }

        protected virtual async Task<string> GetWmicDiskDriveListCsv()
        {
            var output = await "wmic".RunProcessAsync("diskdrive list /format:csv");
            logger.LogDebug(output);
            return output;
        }

        protected virtual async Task<string> GetWmicWin32DiskDriveToDiskPartitionPath()
        {
            var output = await "wmic".RunProcessAsync("path Win32_DiskDriveToDiskPartition get * /format:csv");
            logger.LogDebug(output);
            return output;
        }

        protected virtual async Task<string> GetWmicWin32LogicalDiskToPartitionPath()
        {
            var output = await "wmic".RunProcessAsync("path Win32_LogicalDiskToPartition get * /format:csv");
            logger.LogDebug(output);
            return output;
        }
    }
}