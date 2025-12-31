using Hst.Imager.Core.Models;

namespace Hst.Imager.Core
{
    using System;
    using Microsoft.Extensions.Logging;
    using PhysicalDrives;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class PhysicalDriveManagerFactory(ILoggerFactory loggerFactory)
    {
        public IPhysicalDriveManager Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsPhysicalDriveManager(loggerFactory.CreateLogger<WindowsPhysicalDriveManager>());
            }

            if (OperatingSystem.IsMacOs())
            {
                return new MacOsPhysicalDriveManager(loggerFactory.CreateLogger<MacOsPhysicalDriveManager>());
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxPhysicalDriveManager(loggerFactory.CreateLogger<LinuxPhysicalDriveManager>());
            }
            
            throw new NotSupportedException("Unsupported operating system");
        }        
    }
}