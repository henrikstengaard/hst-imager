using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenPhysicalDiskMedia
{
    [Fact]
    public void When_PhysicalDriveMediasHasSamePath_Then_EqualsReturnTrue()
    {
        // arrange - two physical drive medias with same path
        var physicalDrive = new TestPhysicalDrive("\\disk1", "Disk", "Disk", 100.MB());
        var media1 = new PhysicalDriveMedia("\\disk1", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, physicalDrive, false);
        var media2 = new PhysicalDriveMedia("\\disk1", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, physicalDrive, false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is true
        Assert.True(equals);
    }
    
    [Fact]
    public void When_PhysicalDiskMediasHasDifferentPath_Then_EqualsReturnFalse()
    {
        // arrange - two physical drive medias with different paths
        var physicalDrive = new TestPhysicalDrive("\\disk1", "Disk", "Disk", 100.MB());
        var media1 = new PhysicalDriveMedia("\\disk1", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, physicalDrive, false);
        var media2 = new PhysicalDriveMedia("\\disk2", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, physicalDrive, false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is false
        Assert.False(equals);
    }
}