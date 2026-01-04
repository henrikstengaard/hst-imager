using System.Collections.Generic;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class TestWindowsPhysicalDriveManager(
    ILogger<WindowsPhysicalDriveManager> logger,
    IEnumerable<IPhysicalDrive> physicalDrives)
    : WindowsPhysicalDriveManager(logger)
{
    protected override IEnumerable<IPhysicalDrive> GetPhysicalDrivesUsingKernel32()
    {
        return physicalDrives;
    }
}