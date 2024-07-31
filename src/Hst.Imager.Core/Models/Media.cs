namespace Hst.Imager.Core.Models
{
    using System;
    using System.IO;

    public class Media : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public enum MediaType
        {
            Raw,
            Vhd,
            CompressedRaw,
            CompressedVhd,
            Floppy
        }

        public string Path;
        public string Model;
        public long Size;
        public bool IsPhysicalDrive;
        public MediaType Type;
        public bool Byteswap;
        public bool IsWriteable { get; }

        public Stream Stream { get; private set; }

        public Media(string path, string name, long size, MediaType type, bool isPhysicalDrive, Stream stream, bool byteswap)
        {
            Path = path;
            Model = name;
            Size = size;
            Type = type;
            IsPhysicalDrive = isPhysicalDrive; 
            Stream = stream;
            IsWriteable = stream?.CanWrite ?? false;
            Byteswap = byteswap;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                Stream?.Dispose();
                Stream = null;
            }

            IsDisposed = true;
        }

        public void Dispose() => Dispose(true);

        public void SetStream(Stream stream)
        {
            IsDisposed = false;
            this.Stream = stream;
        }
    }
}