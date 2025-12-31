using System;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class TestLinuxPhysicalDriveManager(
    ILogger<LinuxPhysicalDriveManager> logger,
    string bootPath,
    Func<string> lsBlkHandler)
    : LinuxPhysicalDriveManager(logger)
{
    protected override Task<string> GetBootPath()
    {
        return Task.FromResult(bootPath);
    }

    protected override void VerifyLinuxOperatingSystem()
    {
        // no verification to allow in unit test context
    }

    protected override Task<string> GetLsBlkJson()
    {
        return Task.FromResult(lsBlkHandler.Invoke());
    }
}