using System;

namespace Hst.Imager.Core.Compressions.Zip
{
    public class CentralDirectoryFileHeader : IZipHeader
    {
        public long Offset { get; internal set; }
        public byte VersionMadeByZip { get; set; }
        public byte HostOs { get; set; }
        public ushort Version { get; internal set; }
        public ushort Flags { get; internal set; }
        public ushort Method { get; internal set; }
        public uint Crc32 { get; internal set; }
        public uint CompressedSize { get; internal set; }
        public uint UncompressedSize { get; internal set; }
        public string FileName { get; internal set; }
        public byte[] ExtraField { get; internal set; }
        public string FileComment { get; internal set; }
        public ushort DiskNumber { get; internal set; }
        public ushort InternalFileAttributes { get; internal set; }
        public uint ExternalFileAttributes { get; internal set; }
        public uint DataOffset { get; internal set; }
        public DateTime FileModificationDate { get; internal set; }
    }
}