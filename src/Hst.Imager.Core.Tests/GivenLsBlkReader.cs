namespace Hst.Imager.Core.Tests
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using PhysicalDrives;
    using Xunit;

    public class GivenLsBlkReader
    {
        [Fact]
        public async Task WhenParseLsBlkJsonOutputFromRaspberryPiWithUsbStickThenBlockDevicesAreReturned()
        {
            var lsBlk = LsBlkReader.ParseLsBlk(
                await File.ReadAllTextAsync(Path.Combine("TestData", "lsblk", "lsblk-raspberry-pi-usb-stick.json")));

            Assert.NotNull(lsBlk);
            Assert.NotEmpty(lsBlk.BlockDevices);

            var blockDevices = lsBlk.BlockDevices.ToList();
            Assert.Equal(2, blockDevices.Count);

            var blockDevice1 = blockDevices[0];
            Assert.Equal("disk", blockDevice1.Type);
            Assert.True(blockDevice1.Removable);
            Assert.Equal("Cruzer_Fit", blockDevice1.Model);
            Assert.Equal("/dev/sda", blockDevice1.Path);
            Assert.Equal(15682240512, blockDevice1.Size);
            Assert.Equal("SanDisk'", blockDevice1.Vendor);
            Assert.Equal("usb", blockDevice1.Tran);

            var blockDevice2 = blockDevices[1];
            Assert.Equal("disk", blockDevice2.Type);
            Assert.False(blockDevice2.Removable);
            Assert.Null(blockDevice2.Model);
            Assert.Equal("/dev/mmcblk0", blockDevice2.Path);
            Assert.Equal(30989615104, blockDevice2.Size);
            Assert.Null(blockDevice2.Vendor);
            Assert.Null(blockDevice2.Tran);
        }

        [Fact]
        public async Task WhenParseLsBlkJsonOutputFromRaspberryPiWithUsbIdeAdapterThenBlockDevicesAreReturned()
        {
            var lsBlk = LsBlkReader.ParseLsBlk(await File.ReadAllTextAsync(Path.Combine("TestData", "lsblk",
                "lsblk-raspberry-pi-usb-ide-adapter.json")));

            Assert.NotNull(lsBlk);
            Assert.NotEmpty(lsBlk.BlockDevices);

            var blockDevices = lsBlk.BlockDevices.ToList();
            Assert.Equal(2, blockDevices.Count);

            var blockDevice1 = blockDevices[0];
            Assert.Equal("disk", blockDevice1.Type);
            Assert.False(blockDevice1.Removable);
            Assert.Equal("SSD_PM830_mSATA", blockDevice1.Model);
            Assert.Equal("/dev/sda", blockDevice1.Path);
            Assert.Equal(128035676160, blockDevice1.Size);
            Assert.Equal("SAMSUNG ", blockDevice1.Vendor);
            Assert.Equal("usb", blockDevice1.Tran);

            var blockDevice2 = blockDevices[1];
            Assert.Equal("disk", blockDevice2.Type);
            Assert.False(blockDevice1.Removable);
            Assert.Null(blockDevice2.Model);
            Assert.Equal("/dev/mmcblk0", blockDevice2.Path);
            Assert.Equal(30989615104, blockDevice2.Size);
            Assert.Null(blockDevice2.Vendor);
            Assert.Null(blockDevice2.Tran);
        }
    }
}