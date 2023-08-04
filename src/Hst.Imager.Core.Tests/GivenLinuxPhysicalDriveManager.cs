using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.PhysicalDrives;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenLinuxPhysicalDriveManager
{
    [Fact]
    public async Task WhenParsePhysicalDrivesThenOnlyRemovablePhysicalDrivesAreReturned()
    {
        // arrange - lsblk json
        var json = await File.ReadAllTextAsync(Path.Combine("TestData", "raspberry-pi-lsblk.json"));

        // act - parse json
        var physicalDrives = LinuxPhysicalDriveManager.Parse(json).ToList();
            
        // assert - 1 physical drive
        Assert.Single(physicalDrives);
            
        // assert - physical drive is equal
        var physicalDrive = physicalDrives.First();
        Assert.Equal("SanDisk' Cruzer_Fit", physicalDrive.Name);
        Assert.Equal("/dev/sda", physicalDrive.Path);
        Assert.Equal(15682240512, physicalDrive.Size);
        Assert.Equal("disk", physicalDrive.Type);
    }
}