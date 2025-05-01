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

        [Fact]
        public async Task WhenParseLsBlkJsonOutputFromUbuntuThenBlockDevicesAreReturned()
        {
            var lsBlk = LsBlkReader.ParseLsBlk(await File.ReadAllTextAsync(Path.Combine("TestData", "lsblk",
                "lsblk-ubuntu.json")));
            
            Assert.NotNull(lsBlk);
            Assert.NotEmpty(lsBlk.BlockDevices);

            var blockDevices = lsBlk.BlockDevices.Where(x => x.Type == "disk").ToList();
            Assert.Single(blockDevices);

            var diskBlockDevice = blockDevices[0];
            Assert.Equal("disk", diskBlockDevice.Type);
            Assert.False(diskBlockDevice.Removable);
            Assert.Equal("VBOX HARDDISK", diskBlockDevice.Model);
            Assert.Equal("/dev/sda", diskBlockDevice.Path);
            Assert.Equal(274877906944, diskBlockDevice.Size);
            Assert.Equal("ATA     ", diskBlockDevice.Vendor);
            Assert.Equal("sata", diskBlockDevice.Tran);
            
            var children = diskBlockDevice.Children.ToList();
            Assert.Equal(2, children.Count);

            var children1 = children[0];
            Assert.Equal("part", children1.Type);
            Assert.Equal("sda1", children1.Name);
            Assert.False(children1.Removable);
            Assert.Equal("/dev/sda1", children1.Path);
            Assert.Equal(1048576, children1.Size);

            var children2 = children[1];
            Assert.Equal("part", children2.Type);
            Assert.Equal("sda2", children2.Name);
            Assert.False(children2.Removable);
            Assert.Equal("/dev/sda2", children2.Path);
            Assert.Equal(274874761216, children2.Size);
        }
    }
}