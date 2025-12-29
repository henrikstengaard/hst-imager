using System.Collections.Generic;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class TestWindowsPhysicalDriveManager : WindowsPhysicalDriveManager
{
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;

    public TestWindowsPhysicalDriveManager(ILogger<WindowsPhysicalDriveManager> logger,
        IEnumerable<IPhysicalDrive> physicalDrives)
        : base(logger, false, CacheType.Memory)
    {
        this.physicalDrives = physicalDrives;
    }

    protected override IEnumerable<IPhysicalDrive> GetPhysicalDrivesUsingKernel32()
    {
        return physicalDrives;
    }
}