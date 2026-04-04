namespace Hst.Imager.Core.Models
{
    using System.IO;

    public class PhysicalDriveMedia : Media
    {
        private readonly IPhysicalDrive physicalDrive;
        private readonly bool useCache;
        private readonly CacheType cacheType;
        private readonly int blockSize;

        public PhysicalDriveMedia(string path, string name, MediaType type,
            IPhysicalDrive physicalDrive, bool byteswap, Stream stream = null, bool useCache = false,
            CacheType cacheType = CacheType.Disk, int blockSize = 1024 * 1024)
            : base(path, name, type, true, stream, byteswap)
        {
            this.physicalDrive = physicalDrive;
            this.useCache = useCache;
            this.cacheType = cacheType;
            this.blockSize = blockSize;
            SetStream(physicalDrive.Open(useCache, cacheType, blockSize));
        }

        public void OpenStream()
        {
            if (Stream != null)
            {
                return;
            }

            SetStream(physicalDrive.Open(useCache, cacheType, blockSize));
        }
    }
}