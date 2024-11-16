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
            using (var piStormRdbMedia = new PiStormRdbMedia("\\disk1", Constants.FileSystemNames.PiStormRdb, 1.MB(), Media.MediaType.Raw, false, virtualStream, false, media))
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