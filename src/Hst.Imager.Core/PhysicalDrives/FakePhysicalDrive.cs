namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class FakePhysicalDrive : GenericPhysicalDrive
    {
        private readonly long size;
        private readonly byte[] data;
        
        public FakePhysicalDrive(string path, string type, string name, long size) : base(path, type, name, size)
        {
            this.size = size;
            data = new byte[size];
        }

        public FakePhysicalDrive(string path, string type, string name, byte[] data) : base(path, type, name, data.Length)
        {
            this.size = data.Length;
            this.data = data;
        }
        
        public override Stream Open()
        {
            return new MemoryStream(data);
        }
    }
}