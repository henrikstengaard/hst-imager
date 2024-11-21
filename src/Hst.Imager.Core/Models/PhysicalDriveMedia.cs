namespace Hst.Imager.Core.Models
{
    using System.IO;

    public class PhysicalDriveMedia : Media
    {
        private readonly IPhysicalDrive physicalDrive;

        public PhysicalDriveMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive,
            IPhysicalDrive physicalDrive, bool byteswap, Stream stream = null)
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            this.physicalDrive = physicalDrive;
            SetStream(physicalDrive.Open());
        }

        public void OpenStream()
        {
            if (Stream != null)
            {
                return;
            }

            SetStream(physicalDrive.Open());
        }
    }
}