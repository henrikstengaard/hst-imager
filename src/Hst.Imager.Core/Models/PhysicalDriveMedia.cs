namespace Hst.Imager.Core.Models
{
    using System.IO;

    public class PhysicalDriveMedia : Media
    {
        private readonly IPhysicalDrive physicalDrive;
        private readonly bool useCache;
        private readonly CacheType cacheType;
        private readonly int blockSize;

        public PhysicalDriveMedia(string path, string name, long size, MediaType type, bool isPhysicalDrive,
            IPhysicalDrive physicalDrive, bool byteswap, Stream stream = null, bool useCache = false,
            CacheType cacheType = CacheType.Memory, int blockSize = 1024 * 1024)
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
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