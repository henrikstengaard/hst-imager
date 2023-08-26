using System;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class TestLinuxPhysicalDriveManager : LinuxPhysicalDriveManager
{
    private readonly Func<string> lsBlkHandler;

    public TestLinuxPhysicalDriveManager(ILogger<LinuxPhysicalDriveManager> logger,
        Func<string> lsBlkHandler) : base(logger)
    {
        this.lsBlkHandler = lsBlkHandler;
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