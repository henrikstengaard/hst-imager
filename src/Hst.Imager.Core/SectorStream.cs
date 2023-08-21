namespace Hst.Imager.Core
{
    using System;
    using System.IO;

    /// <summary>
    /// Sector stream that ensures buffers are read and written in sector sizes
    /// </summary>
    public class SectorStream : Stream
    {
        private readonly Stream stream;
        private readonly bool byteSwap;
        private readonly bool leaveOpen;
        private const int SectorSize = 512;
        private readonly byte[] sectorBuffer;

        public SectorStream(Stream baseStream, bool byteSwap = false, bool leaveOpen = false)
        {
            this.stream = baseStream;
            this.byteSwap = byteSwap;
            this.leaveOpen = leaveOpen;
            this.sectorBuffer = new byte[SectorSize];
            this.SectorBufferPosition = 0;
        }

        public int SectorBufferPosition { get; private set; }

        public override void Close()
        {
            Flush();
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (!leaveOpen)
                    {
                        this.stream.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
        
        public override void Flush()
        {
            if (SectorBufferPosition > 0)
            {
                stream.Write(sectorBuffer, 0, SectorSize);
                Array.Fill<byte>(sectorBuffer, 0);
                SectorBufferPosition = 0;
            }
            
            stream.Flush();
        }

        private static void ThrowIfNotDividableBySectorSize(string paramName, long value)
        {
            if (value % SectorSize == 0)
            {
                return;
            }

            throw new ArgumentOutOfRangeException(paramName, 
                $"Sector stream only supports values dividable by {SectorSize} and value is {value}");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Sector stream only supports offset 0");
            }

            if (SectorBufferPosition > 0)
            {
                var sectorBufferCopyLength = Math.Min(count, SectorSize - SectorBufferPosition);
                Array.Copy(this.sectorBuffer, SectorBufferPosition, buffer, 0, sectorBufferCopyLength);
                SectorBufferPosition += sectorBufferCopyLength;
                offset += sectorBufferCopyLength;
            }

            if (offset == count)
            {
                if (byteSwap)
                {
                    ByteSwapBuffer(buffer, count);
                }
                
                return offset;
            }

            var readLength = count - offset;
            var readBuffer = new byte[readLength % SectorSize == 0
                ? readLength
                : readLength - (readLength % SectorSize) + SectorSize];
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

            if (bytesRead < readLength)
            {
                readLength = bytesRead;
            }

            // copy read length to buffer
            Array.Copy(readBuffer, 0, buffer, offset, readLength);
            offset += readLength;

            if (byteSwap)
            {
                ByteSwapBuffer(buffer, readLength);
            }

            var bytesLeftInReadBuffer = readLength % SectorSize;
            if (bytesLeftInReadBuffer == 0)
            {
                return offset;
            }

            // copy left to sector buffer
            Array.Fill<byte>(sectorBuffer, 0);
            Array.Copy(readBuffer, readLength - bytesLeftInReadBuffer, sectorBuffer, 0, SectorSize);
            SectorBufferPosition = bytesLeftInReadBuffer;
            
            return offset;
        }

        private void ByteSwapBuffer(byte[] buffer, int count)
        {
            for (var i = 0; i < count - count % 2; i += 2)
            {
                (buffer[i + 1], buffer[i]) = (buffer[i], buffer[i + 1]);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfNotDividableBySectorSize(nameof(offset), offset);
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (SectorBufferPosition > 0)
            {
                var fillBytes = SectorBufferPosition + count > SectorSize
                    ? SectorSize - SectorBufferPosition
                    : count;
                Array.Copy(buffer, offset, sectorBuffer, SectorBufferPosition, fillBytes);
                SectorBufferPosition += fillBytes;
                offset += fillBytes;

                if (SectorBufferPosition < SectorSize)
                {
                    return;
                }
                
                stream.Write(sectorBuffer, 0, sectorBuffer.Length);
                Array.Fill<byte>(sectorBuffer, 0);
                SectorBufferPosition = 0;
            }

            var remainingBytes = count - offset;
            var sectors = Convert.ToInt32(Math.Floor((double)remainingBytes / SectorSize));
            var sectorsLength = sectors * SectorSize;
            if (sectors > 0)
            {
                stream.Write(buffer, offset, sectorsLength);
            }

            var left = (count - offset - sectorsLength) % SectorSize;
            if (left == 0)
            {
                return;
            }
            
            Array.Copy(buffer, offset + sectorsLength, sectorBuffer, SectorBufferPosition, left);
            SectorBufferPosition += left;
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }
    }
}