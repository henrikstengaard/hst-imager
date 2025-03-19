using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hst.Imager.Core.PhysicalDrives;

public class StaticPhysicalDriveManager(IEnumerable<IPhysicalDrive> physicalDrives) : IPhysicalDriveManager
{
    public Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false)
    {
        return Task.FromResult(physicalDrives);
    }
}