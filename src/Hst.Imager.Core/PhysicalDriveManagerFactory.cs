using Hst.Imager.Core.Models;

namespace Hst.Imager.Core
{
    using System;
    using Microsoft.Extensions.Logging;
    using PhysicalDrives;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class PhysicalDriveManagerFactory(ILoggerFactory loggerFactory, bool useCache, CacheType cacheType)
    {
        public IPhysicalDriveManager Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsPhysicalDriveManager(loggerFactory.CreateLogger<WindowsPhysicalDriveManager>(),
                    useCache, cacheType);
            }

            if (OperatingSystem.IsMacOs())
            {
                return new MacOsPhysicalDriveManager(loggerFactory.CreateLogger<MacOsPhysicalDriveManager>(),
                    useCache, cacheType);
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxPhysicalDriveManager(loggerFactory.CreateLogger<LinuxPhysicalDriveManager>(),
                    useCache, cacheType);
            }
            
            throw new NotSupportedException("Unsupported operating system");
        }        
    }
}