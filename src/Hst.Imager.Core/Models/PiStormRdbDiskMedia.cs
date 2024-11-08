namespace Hst.Imager.Core.Models
{
    using DiscUtils;
    using Hst.Amiga.RigidDiskBlocks;
    using System.IO;

    public class PiStormRdbDiskMedia : DiskMedia
    {
        public readonly RigidDiskBlock RigidDiskBlock;

        public PiStormRdbDiskMedia(string path, string name, long size, MediaType type,
            bool isPhysicalDrive, VirtualDisk disk, bool byteswap, RigidDiskBlock rigidDiskBlock,
            Stream stream = null)
            : base(path, name, size, type, isPhysicalDrive, disk, byteswap, stream)
        {
            RigidDiskBlock = rigidDiskBlock;
        }
    }
}