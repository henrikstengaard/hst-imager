namespace Hst.Imager.Core.Models
{
    using Hst.Amiga.RigidDiskBlocks;
    using System.IO;

    public class PiStormRdbMedia : Media
    {
        public readonly RigidDiskBlock RigidDiskBlock;

        public PiStormRdbMedia(string path, string name, long size, MediaType type,
            bool isPhysicalDrive, Stream stream, bool byteswap, RigidDiskBlock rigidDiskBlock)
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            RigidDiskBlock = rigidDiskBlock;
        }
    }
}