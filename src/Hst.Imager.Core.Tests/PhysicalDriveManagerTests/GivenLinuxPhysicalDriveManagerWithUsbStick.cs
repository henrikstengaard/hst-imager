using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class GivenLinuxPhysicalDriveManagerWithUsbStick
{
    // arrange - linux physical drive manager with usb stick
    private readonly TestLinuxPhysicalDriveManager linuxPhysicalDriveManager = new(
        new NullLogger<TestLinuxPhysicalDriveManager>(),
        "/dev/mmcblk0",
        () => File.ReadAllText(Path.Combine("TestData", "lsblk", "lsblk-raspberry-pi-usb-stick.json"))
    );

    [Fact]
    public async Task WhenGetPhysicalDrivesWithUsbStickThenUsbPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await linuxPhysicalDriveManager.GetPhysicalDrives()).ToList();

        // assert - 1 physical drive
        Assert.Single(physicalDrives);

        // assert - physical drive is equal
        var physicalDrive = physicalDrives.First();
        Assert.Equal("disk", physicalDrive.Type);
        Assert.Equal("SanDisk' Cruzer_Fit", physicalDrive.Name);
        Assert.Equal("/dev/sda", physicalDrive.Path);
        Assert.Equal(15682240512, physicalDrive.Size);
        Assert.False(physicalDrive.SystemDrive);
    }

    [Fact]
    public async Task WhenGetAllPhysicalDrivesWithUsbStickThenAllPhysicalDrivesAreReturned()
    {
        // act - get physical drives
        var physicalDrives = (await linuxPhysicalDriveManager.GetPhysicalDrives(true)).ToList();

        // assert - 2 physical drives
        Assert.Equal(2, physicalDrives.Count);

        // assert - physical drive is equal
        var physicalDrive1 = physicalDrives[0];
        Assert.Equal("disk", physicalDrive1.Type);
        Assert.Equal("SanDisk' Cruzer_Fit", physicalDrive1.Name);
        Assert.Equal("/dev/sda", physicalDrive1.Path);
        Assert.Equal(15682240512, physicalDrive1.Size);
        Assert.False(physicalDrive1.SystemDrive);

        // assert - physical drive 2 is equal
        var physicalDrive2 = physicalDrives[1];
        Assert.Equal("disk", physicalDrive2.Type);
        Assert.Equal(string.Empty, physicalDrive2.Name);
        Assert.Equal("/dev/mmcblk0", physicalDrive2.Path);
        Assert.Equal(30989615104, physicalDrive2.Size);
        Assert.True(physicalDrive2.SystemDrive);
    }
}