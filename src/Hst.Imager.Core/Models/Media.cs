namespace Hst.Imager.Core.Models
{
    using System;
    using System.IO;

    public class Media : IDisposable, IEquatable<Media>
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
        public virtual long Size => Stream.Length;
        public bool IsPhysicalDrive;
        public MediaType Type;
        public bool Byteswap;
        public bool IsWriteable { get; }

        public Stream Stream { get; private set; }

        public Media(string path, string name, MediaType type, bool isPhysicalDrive, Stream stream, bool byteswap)
        {
            Path = path;
            Model = name;
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
                Stream?.Close();
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

        public bool Equals(Media other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Path == other.Path;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Media)obj);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Path);
        }
    }
}