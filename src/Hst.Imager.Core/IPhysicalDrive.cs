namespace Hst.Imager.Core
{
    using System.IO;

    public interface IPhysicalDrive
    {
        string Path { get; }
        string Type { get; }
        string Name { get; }
        long Size { get; }
        bool Removable { get; }
        bool Writable { get; }
        bool ByteSwap { get; }

        Stream Open();
        void SetWritable(bool writable);
        void SetByteSwap(bool byteSwap);
    }
}