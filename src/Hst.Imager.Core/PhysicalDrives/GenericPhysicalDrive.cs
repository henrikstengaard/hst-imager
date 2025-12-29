using System;
using System.Threading.Tasks;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class GenericPhysicalDrive : IPhysicalDrive, IAsyncDisposable
    {
        protected readonly bool UseCache;
        protected readonly CacheType CacheType;
        protected readonly int BlockSize;
        
        public string Path { get; }
        public string Type { get; }
        public string Name { get; }
        public long Size { get; protected set; }
        public bool Removable { get; }
        public bool Writable { get; private set; }
        public bool ByteSwap { get; private set; }
        public bool SystemDrive { get; private set; }

        private Stream stream;
        public bool IsDisposed { get; private set; }

        public GenericPhysicalDrive(string path, string type, string name, long size, bool removable = false,
            bool writable = false, bool systemDrive = false, bool useCache = false, CacheType cacheType = CacheType.Memory,
            int blockSize = 1024 * 1024)
        {
            BlockSize = blockSize;
            Path = path;
            Type = type;
            Name = name;
            Size = size;
            Removable = removable;
            Writable = writable;
            SystemDrive = systemDrive;
            UseCache = useCache;
            CacheType = cacheType;
            BlockSize = blockSize;
            stream = null;
        }

        public void SetSystemDrive(bool systemDrive)
        {
            this.SystemDrive = systemDrive;
        }

        public virtual Stream Open()
        {
            if (SystemDrive)
            {
                throw new IOException($"Access to system drive path '{Path}' is not supported!");
            }

            IsDisposed = false;
            stream ??= File.Open(Path, FileMode.Open, FileAccess.ReadWrite);
            
            var baseStream = new MediaStream(stream, Size);
            
            return UseCache
                ? CacheHelper.AddLayeredCache(Path, baseStream, Writable, BlockSize, CacheType)
                : baseStream;
        }

        public void SetWritable(bool writable)
        {
            Writable = writable;
        }

        public void SetByteSwap(bool byteSwap)
        {
            ByteSwap = byteSwap;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                stream?.Close();
                stream?.Dispose();
            }

            IsDisposed = true;
        }

        public void Dispose() => Dispose(true);

        public async ValueTask DisposeAsync()
        {
            if (stream != null) await stream.DisposeAsync();
        }
    }
}