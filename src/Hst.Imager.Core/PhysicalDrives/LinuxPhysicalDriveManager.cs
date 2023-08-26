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

    public class LinuxPhysicalDriveManager : IPhysicalDriveManager
    {
        private readonly ILogger<LinuxPhysicalDriveManager> logger;

        public LinuxPhysicalDriveManager(ILogger<LinuxPhysicalDriveManager> logger)
        {
            this.logger = logger;
        }
        
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

            var lsBlkJson = await GetLsBlkJson();

            return Parse(lsBlkJson, all);
        }

        protected virtual async Task<string> GetLsBlkJson()
        {
            var output = await "lsblk".RunProcessAsync("-ba -o TYPE,NAME,RM,MODEL,PATH,SIZE,VENDOR,TRAN --json");
            logger.LogDebug(output);
            return output;
        }

        private static IEnumerable<IPhysicalDrive> Parse(string json, bool all = false)
        {
            var lsBlk = LsBlkReader.ParseLsBlk(json);

            if (lsBlk.BlockDevices == null)
            {
                return Enumerable.Empty<IPhysicalDrive>();
            }

            var blockDevices = all
                ? lsBlk.BlockDevices
                : lsBlk.BlockDevices.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Type) &&
                    x.Type.Equals("disk", StringComparison.OrdinalIgnoreCase) &&
                    (x.Removable || (!string.IsNullOrWhiteSpace(x.Tran) &&
                                     x.Tran.Equals("usb", StringComparison.OrdinalIgnoreCase)))).ToList();

            
            var physicalDrives = blockDevices.Select(x =>
                new GenericPhysicalDrive(x.Path, x.Type ?? string.Empty, GetPhysicalDriveName(x),
                    x.Size ?? 0)).ToList();

            return physicalDrives;
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