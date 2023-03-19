namespace Hst.Imager.Core.Commands
{
    using Hst.Core;

    public class ByteNotEqualError : Error
    {
        public long Offset;
        public byte SourceByte;
        public byte DestinationByte;

        public ByteNotEqualError(long offset, byte sourceByte, byte destinationByte) 
            : base($"ByteNotEqualError at offset {offset} source is 0x{sourceByte:x2} and destination is 0x{destinationByte:x2}")
        {
            Offset = offset;
            SourceByte = sourceByte;
            DestinationByte = destinationByte;
        }
    }
}