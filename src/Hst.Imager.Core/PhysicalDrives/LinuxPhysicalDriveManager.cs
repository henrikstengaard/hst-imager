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

        public async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new NotSupportedException("Linux physical drive manager is not running on Linux environment");
            }

            var lsBlkJson = await GetLsBlkJson();

            return Parse(lsBlkJson, all);
        }

        private async Task<string> GetLsBlkJson()
        {
            var output = await "lsblk".RunProcessAsync("-ba -o TYPE,NAME,RM,MODEL,PATH,SIZE,VENDOR --json");
            logger.LogDebug(output);
            return output;
        }

        public static IEnumerable<IPhysicalDrive> Parse(string json, bool all = false)
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
                    x.Removable).ToList();

            var physicalDrives = blockDevices.Select(x =>
                new GenericPhysicalDrive(x.Path, x.Type ?? string.Empty, string.Concat(x.Vendor, " ", x.Model), x.Size ?? 0)).ToList();

            return physicalDrives;
        }
    }
}