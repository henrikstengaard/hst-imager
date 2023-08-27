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
    using Microsoft.Extensions.Logging;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class MacOsPhysicalDriveManager : IPhysicalDriveManager
    {
        private readonly ILogger<MacOsPhysicalDriveManager> logger;

        public MacOsPhysicalDriveManager(ILogger<MacOsPhysicalDriveManager> logger)
        {
            this.logger = logger;
        }

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

            var listOutput = await GetDiskUtilExternalDisks(all);

            var disks = DiskUtilReader.ParseList(new MemoryStream(Encoding.UTF8.GetBytes(listOutput))).ToList();

            var physicalDrives = new List<IPhysicalDrive>();

            foreach (var disk in disks)
            {
                var partitionDevices = disk.Partitions.Select(x => x.DeviceIdentifier).ToList();
                var infoOutput = await GetDiskUtilInfoDisk(disk.DeviceIdentifier);

                var info = DiskUtilReader.ParseInfo(new MemoryStream(Encoding.UTF8.GetBytes(infoOutput)));

                if (info.DiskType != DiskUtilInfo.DiskTypeEnum.Physical)
                {
                    continue;
                }

                physicalDrives.Add(new MacOsPhysicalDrive(info.DeviceNode, info.MediaType, info.IoRegistryEntryName,
                    info.Size, IsRemovable(info.BusProtocol), partitionDevices));
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