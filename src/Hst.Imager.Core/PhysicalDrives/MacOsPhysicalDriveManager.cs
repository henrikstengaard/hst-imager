using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;
    using Commands;
    using Microsoft.Extensions.Logging;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class MacOsPhysicalDriveManager(ILogger<MacOsPhysicalDriveManager> logger)
        : IPhysicalDriveManager
    {
        protected virtual void VerifyMacOs()
        {
            if (OperatingSystem.IsMacOs())
            {
                return;
            }

            throw new NotSupportedException("MacOS physical drive manager is not running on macOS environment");
        }

        public async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
        {
            VerifyMacOs();

            var bootDiskInfoOutput = await GetDiskUtilInfoDisk("/");

            var bootDiskInfo = DiskUtilReader.ParseInfo(new MemoryStream(Encoding.UTF8.GetBytes(bootDiskInfoOutput)));

            var bootDiskMatch = Regexs.MacOsDiskPathRegex.Match(bootDiskInfo.ParentWholeDisk);

            if (!bootDiskMatch.Success)
            {
                throw new IOException($"Invalid boot disk parent whole disk '{bootDiskInfo.ParentWholeDisk}'");
            }

            var bootDisk = bootDiskMatch.Groups[1].Value;

            logger.LogDebug($"Boot disk '{bootDiskInfo.ParentWholeDisk}'");

            var listOutput = await GetDiskUtilExternalDisks(all);

            var disks = DiskUtilReader.ParseList(new MemoryStream(Encoding.UTF8.GetBytes(listOutput))).ToList();

            var physicalDrives = new List<MacOsPhysicalDrive>();
            var physicalDriveIndex = new Dictionary<string, MacOsPhysicalDrive>();

            var apfsParentBootDisks = new List<string>(10);

            foreach (var disk in disks)
            {
                var partitionDevices = disk.Partitions.Select(x => x.DeviceIdentifier).ToList();
                var infoOutput = await GetDiskUtilInfoDisk(disk.DeviceIdentifier);

                var info = DiskUtilReader.ParseInfo(new MemoryStream(Encoding.UTF8.GetBytes(infoOutput)));

                var apfsPhysicalDeviceIdentifier = disk.ApfsPhysicalStores?.DeviceIdentifier ?? string.Empty;

                var apfsDiskMatch = Regexs.MacOsDiskPathRegex.Match(apfsPhysicalDeviceIdentifier);

                var isSystemDrive = disk.DeviceIdentifier.Equals(bootDisk);

                var apfsDisk = apfsDiskMatch.Success ? apfsDiskMatch.Groups[1].Value : string.Empty;

                if (isSystemDrive &&
                    !string.IsNullOrWhiteSpace(apfsDisk))
                {
                    apfsParentBootDisks.Add(apfsDisk);
                }

                if (info.DiskType != DiskUtilInfo.DiskTypeEnum.Physical)
                {
                    continue;
                }

                var physicalDrive = new MacOsPhysicalDrive(info.DeviceNode, info.MediaType, info.IoRegistryEntryName,
                    info.Size, IsRemovable(info.BusProtocol), isSystemDrive, partitionDevices);
                physicalDrives.Add(physicalDrive);
                physicalDriveIndex[disk.DeviceIdentifier] = physicalDrive;
            }

            foreach (var apfsParentBootDisk in apfsParentBootDisks)
            {
                if (!physicalDriveIndex.ContainsKey(apfsParentBootDisk))
                {
                    continue;
                }

                physicalDriveIndex[apfsParentBootDisk].SetSystemDrive(true);
            }

            if (!all)
            {
                physicalDrives = physicalDrives.Where(x => x.Removable).ToList();
            }

            foreach (var physicalDrive in physicalDrives)
            {
                logger.LogDebug(
                    $"Physical drive: Path '{physicalDrive.Path}', Name '{physicalDrive.Name}', Type = '{physicalDrive.Type}', Size = '{physicalDrive.Size}'");
            }

            return physicalDrives;
        }

        private static bool IsRemovable(string busProtocol)
        {
            return !string.IsNullOrWhiteSpace(busProtocol) &&
                   busProtocol.Equals("usb", StringComparison.OrdinalIgnoreCase);
        }

        protected virtual async Task<string> GetDiskUtilExternalDisks(bool all)
        {
            var output = await "diskutil".RunProcessAsync($"list -plist{(all ? "" : " external")}");
            logger.LogDebug(output);
            return output;
        }

        protected virtual async Task<string> GetDiskUtilInfoDisk(string disk)
        {
            var output = await "diskutil".RunProcessAsync($"info -plist {disk}");
            logger.LogDebug(output);
            return output;
        }
    }
}