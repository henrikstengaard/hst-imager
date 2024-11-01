namespace Hst.Imager.Core.Compressions.Zip
{
    public class EndOfCentralDirectoryFileHeader : IZipHeader
    {
        public long Offset { get; internal set; }
        public ushort DiskNumber { get; internal set; }
        public ushort DiskCentralStart { get; internal set; }
        public ushort NumberOfCentralsStored { get; internal set; }
        public ushort TotalNumberOfCentralDirectories { get; internal set; }
        public uint SizeOfCentralDirectory { get; internal set; }
        public uint OffsetCentralDirectoryStart { get; internal set; }
        public string Comment { get; internal set; }
    }
}