using System;

namespace Hst.Imager.Core.Compressions.Zip
{
    public class LocalFileHeader : IZipHeader
    {
        public long Offset { get; internal set; }
        public ushort Version { get; internal set; }
        public ushort Flags { get; internal set; }
        public ushort Method { get; internal set; }
        public uint Crc32 { get; internal set; }
        public long CompressedSize { get; internal set; }
        public long UncompressedSize { get; internal set; }
        public string FileName { get; internal set; }
        public byte[] ExtraField { get; internal set; }
        public DateTime FileModificationDate { get; internal set; }
        public bool IsZip64 { get; internal set; }
    }
}