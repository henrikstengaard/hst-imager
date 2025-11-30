using System.IO;
using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenMedia
{
    [Fact]
    public void When_MediasHasSamePath_Then_EqualsReturnTrue()
    {
        // arrange - two media with same path
        var media1 = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, new MemoryStream(), false);
        var media2 = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, new MemoryStream(), false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is true
        Assert.True(equals);
    }
    
    [Fact]
    public void When_MediasHasDifferentPath_Then_EqualsReturnFalse()
    {
        // arrange - two media with different paths
        var media1 = new Media("disk1.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, new MemoryStream(), false);
        var media2 = new Media("disk2.img", "Disk", 100.MB(), Media.MediaType.Raw, 
            false, new MemoryStream(), false);
        
        // act - equals
        var equals = media1.Equals(media2);
        
        // assert - equals is false
        Assert.False(equals);
    }
}