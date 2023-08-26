namespace Hst.Imager.Core.Tests
{
    using System.IO;
    using System.Linq;
    using Hst.Imager.Core.PhysicalDrives;
    using Xunit;

    public class GivenDiskUtilReader
    {
        [Fact]
        public void WhenParseListOutputFromDiskUtilThenDisksAreReturned()
        {
            var disks = DiskUtilReader.ParseList(File.OpenRead(Path.Combine("TestData", "diskutil", "diskutil-all-usb-stick.plist"))).ToList();
            
            Assert.Equal(3, disks.Count);
            
            var disk1 = disks[0];
            Assert.Equal("disk0", disk1.DeviceIdentifier);
            Assert.Equal(512110190592, disk1.Size);

            var disk2 = disks[1];
            Assert.Equal("disk1", disk2.DeviceIdentifier);
            Assert.Equal(511900434432, disk2.Size);
            
            var disk3 = disks[2];
            Assert.Equal("disk2", disk3.DeviceIdentifier);
            Assert.Equal(15682240512, disk3.Size);
        }
        
        [Fact]
        public void WhenParseInfoOutputFromDiskUtilThenInfoIsReturned()
        {
            var info = DiskUtilReader.ParseInfo(File.OpenRead(Path.Combine("TestData", "diskutil", "diskutil-info-disk0.plist")));
            
            Assert.NotNull(info);
            Assert.Equal("PCI-Express", info.BusProtocol);
            Assert.Equal("INTEL SSDPEKNW512G8 Media", info.IoRegistryEntryName);
            Assert.Equal(512110190592, info.Size);
            Assert.Equal("/dev/disk0", info.DeviceNode);
            Assert.Equal("Generic", info.MediaType);
        }
    }
}