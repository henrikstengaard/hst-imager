using System;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core
{
    using System.IO;

    public interface IPhysicalDrive : IDisposable
    {
        string Path { get; }
        string Type { get; }
        string Name { get; }
        long Size { get; }
        bool Removable { get; }
        bool Writable { get; }
        bool ByteSwap { get; }
        bool SystemDrive { get; }

        Stream Open(bool useCache, CacheType cacheType, int blockSize);
        void SetWritable(bool writable);
        void SetByteSwap(bool byteSwap);
        void SetSystemDrive(bool systemDrive);
    }
}