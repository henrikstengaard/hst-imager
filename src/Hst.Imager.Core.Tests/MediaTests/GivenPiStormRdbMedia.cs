using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using System.IO;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests
{
    public class GivenPiStormRdbMedia
    {
        [Fact]
        public void When_PiStormRdbMediasHasSamePathAndPartitionNumber_Then_EqualsReturnTrue()
        {
            // arrange - two pistorm rdb medias with same path
            var media = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false);
            var piStormRdbMedia1 = new PiStormRdbMedia("disk.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
            var piStormRdbMedia2 = new PiStormRdbMedia("disk.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
        
            // act - equals
            var equals = piStormRdbMedia1.Equals(piStormRdbMedia2);
        
            // assert - equals is true
            Assert.True(equals);
        }

        [Fact]
        public void When_PiStormRdbMediasHasSamePathAndDifferentPartitionNumber_Then_EqualsReturnFalse()
        {
            // arrange - two pistorm rdb medias with same path and different partition number
            var media = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false);
            var piStormRdbMedia1 = new PiStormRdbMedia("disk.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
            var piStormRdbMedia2 = new PiStormRdbMedia("disk.img", 2, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
        
            // act - equals
            var equals = piStormRdbMedia1.Equals(piStormRdbMedia2);
        
            // assert - equals is false
            Assert.False(equals);
        }

        [Fact]
        public void When_PiStormRdbMediasHasDifferentPathAndSamePartitionNumber_Then_EqualsReturnFalse()
        {
            // arrange - two pistorm rdb medias with different path and same partition number
            var media = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false);
            var piStormRdbMedia1 = new PiStormRdbMedia("disk1.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
            var piStormRdbMedia2 = new PiStormRdbMedia("disk2.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
        
            // act - equals
            var equals = piStormRdbMedia1.Equals(piStormRdbMedia2);
        
            // assert - equals is false
            Assert.False(equals);
        }
        
        [Fact]
        public void When_PiStormRdbMediasHasDifferentPathAndPartitionNumber_Then_EqualsReturnFalse()
        {
            // arrange - two pistorm rdb medias with different path and partition number
            var media = new Media("disk.img", "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false);
            var piStormRdbMedia1 = new PiStormRdbMedia("disk1.img", 1, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
            var piStormRdbMedia2 = new PiStormRdbMedia("disk2.img", 2, "Disk", 100.MB(), Media.MediaType.Raw, 
                false, new MemoryStream(), false, media);
        
            // act - equals
            var equals = piStormRdbMedia1.Equals(piStormRdbMedia2);
        
            // assert - equals is false
            Assert.False(equals);
        }

        [Fact]
        public async Task When_WritingToPiStormRdbMediaStream_Then_DataIsWrittenToBaseStream()
        {
            // arrange - base stream with monitor in sector stream
            var baseStream = new MemoryStream();
            var monitorStream = new MonitorStream(baseStream);
            var sectorStream = new SectorStream(monitorStream);

            // arrange - media with sector stream
            var media = new Media("\\disk1", "\\disk1", 10.MB(), Media.MediaType.Raw, true, sectorStream, false);

            // arrange - create virtual stream at offset 1024 with 1mb max size
            var virtualStream = new VirtualStream(media.Stream, 1024, 1.MB());

            // act - create pistorm rdb media and write data to stream
            using (var piStormRdbMedia = new PiStormRdbMedia("\\disk1", 0, 
                       Constants.FileSystemNames.PiStormRdb, 1.MB(), Media.MediaType.Raw, false,
                       virtualStream, false, media))
            {
                var data = new byte[512];
                Array.Fill<byte>(data, 1);
                await piStormRdbMedia.Stream.WriteAsync(data, 0, data.Length);
            }

            // assert - data is written to base stream at offset 1024
            Assert.Single(monitorStream.Writes);
            monitorStream.Writes[0] = 1024;
        }
    }
}