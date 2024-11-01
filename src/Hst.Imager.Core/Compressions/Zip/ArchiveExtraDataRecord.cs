namespace Hst.Imager.Core.Compressions.Zip
{
    public class ArchiveExtraDataRecord : IZipHeader
    {
        public long Offset { get; internal set; }
        public ushort Version { get; internal set; }
        public int ExtraFieldLength { get; internal set; }
        public byte[] ExtraField { get; internal set; }
    }
}