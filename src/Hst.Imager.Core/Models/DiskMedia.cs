namespace Hst.Imager.Core.Models
{
    using System.IO;
    using DiscUtils;

    public class DiskMedia : Media
    {
        public VirtualDisk Disk { get; private set; }

        public DiskMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive,
            VirtualDisk disk, bool byteswap, Stream stream = null) 
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            this.Disk = disk;
        }

        public override long Size => this.Disk?.Capacity ?? Stream?.Length ?? 0;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Disk.Dispose();
            }
        }

        public void SetDisk(VirtualDisk disk)
        {
            this.Disk = disk;
            SetStream(disk.Content);
        }
    }
}