namespace Hst.Imager.Core.Models
{
    using System.IO;
    using DiscUtils;

    public class VhdMedia : Media
    {
        private readonly VirtualDisk disk;

        public VhdMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive, VirtualDisk disk, Stream stream = null) 
            : base(path, name, size, type, isPhysicalDrive, stream)
        {
            this.disk = disk;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    disk.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}