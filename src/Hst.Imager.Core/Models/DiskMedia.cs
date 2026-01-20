using System;

namespace Hst.Imager.Core.Models
{
    using System.IO;
    using DiscUtils;

    public class DiskMedia : Media
    {
        private readonly VirtualDisk disk;

        public DiskMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive,
            VirtualDisk disk, bool byteswap, Stream stream = null) 
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            ArgumentNullException.ThrowIfNull(disk);
            this.disk = disk;
        }

        public DiskMedia(Media media, VirtualDisk disk, Stream stream) 
            : base(media.Path, media.Model, disk.Capacity, media.Type, media.IsPhysicalDrive, stream, media.Byteswap)
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