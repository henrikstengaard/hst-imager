using System;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class TestMacOsPhysicalDriveManager : MacOsPhysicalDriveManager
{
    private readonly Func<bool, string> listHandler;
    private readonly Func<string, string> infoHandler;

    public TestMacOsPhysicalDriveManager(ILogger<MacOsPhysicalDriveManager> logger,
        Func<bool, string> listHandler, Func<string, string> infoHandler) : base(logger, false)
    {
        this.listHandler = listHandler;
        this.infoHandler = infoHandler;
    }

    protected override void VerifyMacOs()
    {
        // no verification to allow in unit test context
    }

    protected override Task<string> GetDiskUtilExternalDisks(bool all)
    {
        return Task.FromResult(listHandler.Invoke(all));
    }

    protected override Task<string> GetDiskUtilInfoDisk(string disk)
    {
        return Task.FromResult(infoHandler.Invoke(disk));
    }
}