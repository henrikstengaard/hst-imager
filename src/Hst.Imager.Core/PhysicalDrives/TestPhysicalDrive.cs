using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class TestPhysicalDrive : GenericPhysicalDrive
    {
        private readonly long size;
        private readonly byte[] data;
        
        public TestPhysicalDrive(string path, string type, string name, long size) : base(path, type, name, size)
        {
            this.size = size;
            data = new byte[size];
        }

        public TestPhysicalDrive(string path, string type, string name, byte[] data) : base(path, type, name, data.Length)
        {
            this.size = data.Length;
            this.data = data;
        }
        
        public override Stream Open(bool useCache, CacheType cacheType, int blockSize)
        {
            return new MemoryStream(data);
        }
    }
}