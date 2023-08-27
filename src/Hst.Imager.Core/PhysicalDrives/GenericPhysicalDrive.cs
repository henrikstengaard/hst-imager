namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class GenericPhysicalDrive : IPhysicalDrive
    {
        public string Path { get; }
        public string Type { get; }
        public string Name { get; }
        public long Size { get; protected set; }
        public bool Removable { get; }
        public bool Writable { get; private set; }
        public bool ByteSwap { get; private set; }

        public GenericPhysicalDrive(string path, string type, string name, long size, bool removable = false, bool writable = false)
        {
            Path = path;
            Type = type;
            Name = name;
            Size = size;
            Removable = removable;
            Writable = writable;
        }

        public virtual Stream Open()
        {
            return new MediaStream(File.Open(Path, FileMode.Open, Writable ? FileAccess.ReadWrite : FileAccess.Read),
                Size);
        }

        public void SetWritable(bool writable)
        {
            Writable = writable;
        }

        public void SetByteSwap(bool byteSwap)
        {
            ByteSwap = byteSwap;
        }
    }
}