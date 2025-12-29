using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class LinuxPhysicalDriveManager(
        ILogger<LinuxPhysicalDriveManager> logger,
        bool useCache,
        CacheType cacheType)
        : IPhysicalDriveManager
    {
        protected virtual void VerifyLinuxOperatingSystem()
        {
            if (OperatingSystem.IsLinux())
            {
                return;
            }

            throw new NotSupportedException("Linux physical drive manager is not running on Linux environment");
        }

        public async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
        {
            VerifyLinuxOperatingSystem();

            var bootPath = await GetBootPath();
            
            var lsBlkJson = await GetLsBlkJson();

            var physicalDrives = Parse(lsBlkJson, useCache, cacheType)
                .Where(x => x.Type.Equals("disk", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var physicalDrive in physicalDrives)
            {
                if (!string.IsNullOrWhiteSpace(bootPath) &&
                    !bootPath.StartsWith(physicalDrive.Path, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                
                physicalDrive.SetSystemDrive(true);
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

            return physicalDrives.ToList();
        }

        protected virtual async Task<string> GetBootPath()
        {
            var output = (await "findmnt".RunProcessAsync("--output SOURCE -n /")).Trim();
            logger.LogDebug(output);
            return output;
        }

        protected virtual async Task<string> GetLsBlkJson()
        {
            var output = await "lsblk".RunProcessAsync("-ba -o TYPE,NAME,RM,MODEL,PATH,SIZE,VENDOR,TRAN --json");
            logger.LogDebug(output);
            return output;
        }

        private static IEnumerable<IPhysicalDrive> Parse(string json, bool useCache, CacheType cacheType)
        {
            var lsBlk = LsBlkReader.ParseLsBlk(json);

            if (lsBlk.BlockDevices == null)
            {
                return [];
            }

            var physicalDrives = lsBlk.BlockDevices.Select(x =>
                new GenericPhysicalDrive(x.Path, x.Type ?? string.Empty, GetPhysicalDriveName(x),
                    x.Size ?? 0, IsRemovable(x), useCache: useCache, cacheType: cacheType)).ToList();

            return physicalDrives;
        }

        private static bool IsRemovable(BlockDevice blockDevice)
        {
            return !string.IsNullOrWhiteSpace(blockDevice.Type) &&
                   blockDevice.Type.Equals("disk", StringComparison.OrdinalIgnoreCase) &&
                   (blockDevice.Removable || (!string.IsNullOrWhiteSpace(blockDevice.Tran) &&
                                              blockDevice.Tran.Equals("usb", StringComparison.OrdinalIgnoreCase)));
        }

        private static string GetPhysicalDriveName(BlockDevice blockDevice)
        {
            var nameParts = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(blockDevice.Vendor))
            {
                nameParts.Add(blockDevice.Vendor);
            }
            if (!string.IsNullOrWhiteSpace(blockDevice.Model))
            {
                nameParts.Add(blockDevice.Model);
            }

            return string.Join(" ", nameParts);
        }
    }
}