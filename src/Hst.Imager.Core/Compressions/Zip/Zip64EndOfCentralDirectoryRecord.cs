namespace Hst.Imager.Core.Compressions.Zip
{
    public class Zip64EndOfCentralDirectoryRecord : IZipHeader
    {
        public long Offset { get; internal set; }
        public ushort VersionMadeBy { get; internal set; }
        public ushort VersionNeededToExtract { get; internal set; }
        public uint DiskNumber { get; internal set; }
        public uint DiskCentralDirectoryStart { get; internal set; }
        public ulong NumberOfCentralDirectoriesStored { get; internal set; }
        public ulong TotalNumberOfCentralDirectories { get; internal set; }
        public ulong SizeOfCentralDirectory { get; internal set; }
        public ulong OffsetCentralDirectoryStart { get; internal set; }
        public string Comment { get; internal set; }
    }
}