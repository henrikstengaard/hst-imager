﻿namespace Hst.Imager.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPhysicalDriveManager
    {
        Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(bool all = false);
    }
}