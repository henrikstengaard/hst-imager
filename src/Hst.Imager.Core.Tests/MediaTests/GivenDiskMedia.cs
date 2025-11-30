using System.IO;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenDiskMedia
{
    [Fact]
    public void When_DiskMediasHasSamePath_Then_EqualsReturnTrue()
    {
        // arrange - two disk medias with same path
        var disk = new DiscUtils.Raw.Disk(new MemoryStream(), Ownership.None);
        var media1 = new DiskMedia("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, disk, false);
        var media2 = new DiskMedia("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, disk, false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is true
        Assert.True(equals);
    }
    
    [Fact]
    public void When_DiskMediasHasDifferentPath_Then_EqualsReturnFalse()
    {
        // arrange - two disk medias with different paths
        var disk1 = new DiscUtils.Raw.Disk(new MemoryStream(), Ownership.None);
        var disk2 = new DiscUtils.Raw.Disk(new MemoryStream(), Ownership.None);
        var media1 = new DiskMedia("disk1.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, disk1, false);
        var media2 = new DiskMedia("disk2.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, disk2, false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is false
        Assert.False(equals);
    }
}