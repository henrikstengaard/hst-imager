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
        private readonly int blockSize;

        public PhysicalDriveManagerFactory(ILoggerFactory loggerFactory, bool useCache, int blockSize)
        {
            this.loggerFactory = loggerFactory;
            this.useCache = useCache;
            this.blockSize = blockSize;
        }

        public IPhysicalDriveManager Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsPhysicalDriveManager(this.loggerFactory.CreateLogger<WindowsPhysicalDriveManager>(),
                    useCache, blockSize);
            }

            if (OperatingSystem.IsMacOs())
            {
                return new MacOsPhysicalDriveManager(this.loggerFactory.CreateLogger<MacOsPhysicalDriveManager>());
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxPhysicalDriveManager(this.loggerFactory.CreateLogger<LinuxPhysicalDriveManager>());
            }
            
            throw new NotSupportedException("Unsupported operating system");
        }        
    }
}