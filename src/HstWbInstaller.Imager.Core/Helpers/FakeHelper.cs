namespace HstWbInstaller.Imager.Core.Helpers
{
    using Commands;
    using Hst.Amiga.RigidDiskBlocks;

    public static class FakeHelper
    {
        public static byte[] DosType = new byte[] { 80, 68, 84, 3 };
        public const string Path = "fake";
        
        public static MediaInfo CreateFakeMediaInfo()
        {
            return new MediaInfo
            {
                Path = Path,
                DiskSize = 1024L * 1024 * 1000 * 16,
                Model = "SanDisk 16GB",
                IsPhysicalDrive = true,
            };
        }

        public static RigidDiskBlock Create()
        {
            return new RigidDiskBlock
            {
                BlockSize = 512,
                CylBlocks = 1008,
                Cylinders = 7362,
                DiskProduct = "HstWB 4GB",
                DiskRevision = "0.4",
                DiskSize = 3799498752L,
                DiskVendor = "UAE",
                Heads = 16,
                HiCylinder = 7361,
                PartitionBlocks = new[]
                {
                    new PartitionBlock
                    {
                        BlocksPerTrack = 63,
                        BootPriority = 0,
                        DosType = DosType,
                        DriveName = "DH0",
                        FileSystemBlockSize = 512,
                        HighCyl = 610,
                        LowCyl = 2,
                        Mask = 2147483646U,
                        MaxTransfer = 130560,
                        NumBuffer = 80,
                        PartitionSize = 314302464L,
                        PreAlloc = 0,
                        Reserved = 2,
                        Sectors = 1,
                        Surfaces = 16
                    },
                    new PartitionBlock
                    {
                        BlocksPerTrack = 63,
                        BootPriority = 0,
                        DosType = DosType,
                        DriveName = "DH1",
                        FileSystemBlockSize = 512,
                        HighCyl = 7356,
                        LowCyl = 611,
                        Mask = 2147483646U,
                        MaxTransfer = 130560,
                        NumBuffer = 80,
                        PartitionSize = 3481583616L,
                        PreAlloc = 0,
                        Reserved = 2,
                        Sectors = 1,
                        Surfaces = 16
                    }
                },
                FileSystemHeaderBlocks = new[]
                {
                    new FileSystemHeaderBlock
                    {
                        DosType = DosType,
                        Version = 1245186
                    }
                }
            };

        }
    }
}