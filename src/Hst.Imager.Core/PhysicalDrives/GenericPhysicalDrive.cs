using System;
using System.Threading.Tasks;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System.IO;

    public class GenericPhysicalDrive : IPhysicalDrive, IAsyncDisposable
    {
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
            bool writable = false, bool systemDrive = false)
        {
            Path = path;
            Type = type;
            Name = name;
            Size = size;
            Removable = removable;
            Writable = writable;
            SystemDrive = systemDrive;
            stream = null;
        }

        public void SetSystemDrive(bool systemDrive)
        {
            this.SystemDrive = systemDrive;
        }

        public virtual Stream Open(bool useCache, CacheType cacheType, int blockSize)
        {
            if (SystemDrive)
            {
                throw new IOException($"Access to system drive path '{Path}' is not supported!");
            }

            IsDisposed = false;
            
            var retry = 0;
            var waitTimeInMilliseconds = 200;
            do
            {
                try
                {
                    stream ??= File.Open(Path, FileMode.Open, FileAccess.ReadWrite);
                    break;
                }
                catch(IOException)
                {
                    Task.Delay(waitTimeInMilliseconds).GetAwaiter().GetResult();
                    waitTimeInMilliseconds *= 2;
                    retry++;
                }
            } while(stream == null || retry < 10);
            
            if (stream == null)
            {
                throw new IOException($"Failed to open physical drive path '{Path}' after {retry} retries and wait time of {waitTimeInMilliseconds} milliseconds");
            }

            var baseStream = new MediaStream(stream, Size);
            
            return useCache
                ? CacheHelper.AddLayeredCache(Path, baseStream, Writable, blockSize, cacheType)
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
                stream = null;
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