namespace Hst.Imager.Core.Models
{
    using DiscUtils;
    using Hst.Amiga.RigidDiskBlocks;
    using System.IO;

    /// <summary>
    /// PiStormRdb media, keeps the rigid disk block read from mbr partition and disk, if opened from a disk media.
    /// </summary>
    public class PiStormRdbMedia : Media
    {
        public readonly RigidDiskBlock RigidDiskBlock;
        private readonly VirtualDisk disk;

        public PiStormRdbMedia(string path, string name, long size, MediaType type,
            bool isPhysicalDrive, Stream stream, bool byteswap, RigidDiskBlock rigidDiskBlock, VirtualDisk disk = null)
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            RigidDiskBlock = rigidDiskBlock;
            this.disk = disk;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && disk != null)
            {
                disk.Dispose();
            }
        }
    }
}