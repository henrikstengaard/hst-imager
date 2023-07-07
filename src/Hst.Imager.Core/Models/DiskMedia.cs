namespace Hst.Imager.Core.Models
{
    using System.IO;
    using DiscUtils;

    public class DiskMedia : Media
    {
        public VirtualDisk Disk { get; private set; }

        public DiskMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive, VirtualDisk disk, Stream stream = null) 
            : base(path, name, size, type, isPhysicalDrive, stream)
        {
            this.Disk = disk;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    Disk.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public void SetDisk(VirtualDisk disk)
        {
            this.Disk = disk;
            SetStream(disk.Content);
        }
    }
}