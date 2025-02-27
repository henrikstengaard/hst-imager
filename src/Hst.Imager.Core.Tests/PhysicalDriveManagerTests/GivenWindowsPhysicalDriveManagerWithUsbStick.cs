using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using OperatingSystem = Hst.Core.OperatingSystem;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class GivenWindowsPhysicalDriveManagerWithUsbStick
{
    // arrange - windows physical drive manager with usb stick
    private readonly TestWindowsPhysicalDriveManager windowsPhysicalDriveManager = new(
        new NullLogger<TestWindowsPhysicalDriveManager>(),
        new List<IPhysicalDrive>
        {
            new WindowsPhysicalDrive("\\disk0", "FixedMedia", "17", "Micron_2450_MTFDKBA1T0TFK", 1024209543168, false, new []{ "C" }),
            new WindowsPhysicalDrive("\\disk1", "RemovableMedia", "BusTypeUsb", "SanDisk' Cruzer Fit", 15682240512, true, new []{ "D" }),
        }
    );
    
    [Fact]
    public async Task WhenGetPhysicalDrivesWithUsbStickThenUsbPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await windowsPhysicalDriveManager.GetPhysicalDrives()).ToList();
            
        // end test, if not windows operating system
        if (!OperatingSystem.IsWindows())
        {
            // assert - no physical drives
            Assert.Empty(physicalDrives);
            
            return;
        }    

        // assert - 1 physical drive
        Assert.Single(physicalDrives);
            
        // assert - physical drive is equal
        var physicalDrive = physicalDrives.First();
        Assert.Equal("RemovableMedia", physicalDrive.Type);
        Assert.Equal("SanDisk' Cruzer Fit", physicalDrive.Name);
        Assert.Equal("\\disk1", physicalDrive.Path);
        Assert.Equal(15682240512, physicalDrive.Size);
    }
    
    [Fact]
    public async Task WhenGetAllPhysicalDrivesWithUsbStickThenAllPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await windowsPhysicalDriveManager.GetPhysicalDrives(true)).ToList();
            
        // end test, if not windows operating system
        if (!OperatingSystem.IsWindows())
        {
            // assert - no physical drives
            Assert.Empty(physicalDrives);
            
            return;
        }    

        // assert - 2 physical drives
        Assert.Equal(2, physicalDrives.Count);
            
        // assert - physical drive 1 is equal
        var physicalDrive1 = physicalDrives[0];
        Assert.Equal("FixedMedia", physicalDrive1.Type);
        Assert.Equal("Micron_2450_MTFDKBA1T0TFK", physicalDrive1.Name);
        Assert.Equal("\\disk0", physicalDrive1.Path);
        Assert.Equal(1024209543168, physicalDrive1.Size);

        // assert - physical drive 2 is equal
        var physicalDrive2 = physicalDrives[1];
        Assert.Equal("RemovableMedia", physicalDrive2.Type);
        Assert.Equal("SanDisk' Cruzer Fit", physicalDrive2.Name);
        Assert.Equal("\\disk1", physicalDrive2.Path);
        Assert.Equal(15682240512, physicalDrive2.Size);
    }
}