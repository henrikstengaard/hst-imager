namespace Hst.Imager.Core.Compressions.Zip
{
    public class Zip64EndOfCentralDirectoryLocator : IZipHeader
    {
        public long Offset { get; internal set; }
        public uint DiskStartZip64CentralDirectory { get; internal set; }
        public ulong StartOfZip64EndOfCentralDirector { get; internal set; }
        public uint TotalNumberOfDisks { get; internal set; }
    }
}