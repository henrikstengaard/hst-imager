using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class GivenMacOsPhysicalDriveManagerWithUsbStick
{
        private static string ResolveDisk(string diskName)
    {
        return diskName switch
        {
            "/" => $"diskutil-info-boot.plist",
            "disk2" => "diskutil-info-disk2-usb-stick.plist",
            _ => $"diskutil-info-{diskName}.plist",
        };
    }

    // arrange - macos physical drive manager with usb stick
    private readonly TestMacOsPhysicalDriveManager macOsPhysicalDriveManager = new(
        new NullLogger<MacOsPhysicalDriveManager>(),
        all => File.ReadAllText(Path.Combine("TestData", "diskutil",
            all ? "diskutil-all-usb-stick.plist" : "diskutil-external-usb-stick.plist")),
        diskName => File.ReadAllText(Path.Combine("TestData", "diskutil", ResolveDisk(diskName))));
    
    [Fact]
    public async Task WhenGetPhysicalDrivesWithUsbStickThenUsbPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await macOsPhysicalDriveManager.GetPhysicalDrives()).ToList();
            
        // assert - 1 physical drive
        Assert.Single(physicalDrives);
            
        // assert - physical drive is equal
        var physicalDrive = physicalDrives.First();
        Assert.Equal("Generic", physicalDrive.Type);
        Assert.Equal("SanDisk' Cruzer Fit Media", physicalDrive.Name);
        Assert.Equal("/dev/disk2", physicalDrive.Path);
        Assert.Equal(15682240512, physicalDrive.Size);
    }
    
    [Fact]
    public async Task WhenGetAllPhysicalDrivesWithUsbStickThenAllPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await macOsPhysicalDriveManager.GetPhysicalDrives(true)).ToList();
            
        // assert - 2 physical drives
        Assert.Equal(2, physicalDrives.Count);
            
        // assert - physical drive 1 is equal
        var physicalDrive1 = physicalDrives[0];
        Assert.Equal("Generic", physicalDrive1.Type);
        Assert.Equal("INTEL SSDPEKNW512G8 Media", physicalDrive1.Name);
        Assert.Equal("/dev/disk0", physicalDrive1.Path);
        Assert.Equal(512110190592, physicalDrive1.Size);

        // assert - physical drive 2 is equal
        var physicalDrive2 = physicalDrives[1];
        Assert.Equal("Generic", physicalDrive2.Type);
        Assert.Equal("SanDisk' Cruzer Fit Media", physicalDrive2.Name);
        Assert.Equal("/dev/disk2", physicalDrive2.Path);
        Assert.Equal(15682240512, physicalDrive2.Size);
    }
}