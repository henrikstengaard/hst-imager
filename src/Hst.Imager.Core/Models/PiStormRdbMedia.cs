namespace Hst.Imager.Core.Models
{
    using System;
    using System.IO;

    /// <summary>
    /// PiStorm RDB media represents a Master Boot Record partition containing a Rigid Disk Block
    /// </summary>
    public class PiStormRdbMedia(
        string path,
        int partitionNumber,
        string name,
        long size,
        Media.MediaType type,
        bool isPhysicalDrive,
        Stream stream,
        bool byteswap,
        Media baseMedia)
        : Media(path, name, size, type, isPhysicalDrive, stream, byteswap), IEquatable<PiStormRdbMedia>
    {
        private readonly Media baseMedia = baseMedia;
        private readonly int partitionNumber = partitionNumber;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                baseMedia.Dispose();
            }
        }

        public bool Equals(PiStormRdbMedia other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(baseMedia, other.baseMedia) && partitionNumber == other.partitionNumber;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PiStormRdbMedia)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), baseMedia, partitionNumber);
        }
    }
}