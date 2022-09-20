namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class GenericPhysicalDrive : IPhysicalDrive
    {
        public string Path { get; }
        public string Type { get; }
        public string Model { get; }
        public long Size { get; protected set; }
        public bool Writable { get; private set; }

        public GenericPhysicalDrive(string path, string type, string model, long size, bool writable = false)
        {
            Path = path;
            Type = type;
            Model = model;
            Size = size;
            Writable = writable;
        }

        public virtual Stream Open()
        {
            return File.Open(Path, FileMode.Open, FileAccess.ReadWrite);
        }

        public void SetWritable(bool writable)
        {
            Writable = writable;
        }
    }
}