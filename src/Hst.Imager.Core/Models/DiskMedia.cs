using System;

namespace Hst.Imager.Core.Models
{
    using System.IO;
    using DiscUtils;

    public class DiskMedia : Media
    {
        private readonly VirtualDisk disk;

        public DiskMedia(string path, string name, MediaType type, bool isPhysicalDrive,
            VirtualDisk disk, bool byteswap, Stream stream = null) 
            : base(path, name, type, isPhysicalDrive, stream, byteswap)
        {
            ArgumentNullException.ThrowIfNull(disk);
            this.disk = disk;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                disk.Dispose();
            }
        }

        public VirtualDisk GetVirtualDisk() => disk;
    }
}