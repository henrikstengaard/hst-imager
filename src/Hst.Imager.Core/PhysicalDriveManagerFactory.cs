namespace Hst.Imager.Core
{
    using System;
    using Microsoft.Extensions.Logging;
    using PhysicalDrives;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class PhysicalDriveManagerFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly bool useCache;

        public PhysicalDriveManagerFactory(ILoggerFactory loggerFactory, bool useCache)
        {
            this.loggerFactory = loggerFactory;
            this.useCache = useCache;
        }

        public IPhysicalDriveManager Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsPhysicalDriveManager(this.loggerFactory.CreateLogger<WindowsPhysicalDriveManager>(),
                    useCache);
            }

            if (OperatingSystem.IsMacOs())
            {
                return new MacOsPhysicalDriveManager(this.loggerFactory.CreateLogger<MacOsPhysicalDriveManager>(),
                    useCache);
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxPhysicalDriveManager(this.loggerFactory.CreateLogger<LinuxPhysicalDriveManager>(),
                    useCache);
            }
            
            throw new NotSupportedException("Unsupported operating system");
        }        
    }
}