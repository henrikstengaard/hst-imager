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
        private readonly int bufferSize;
        private const int SectorSize = 512;
        private readonly byte[] sectorBytes;
        private int sectorBytesRead;
        
        /// <summary>
        /// offset in the stream
        /// </summary>
        private long streamOffset;
        
        /// <summary>
        /// offset for current sector
        /// </summary>
        private long sectorOffset;
        
        /// <summary>
        /// indicates if the sector has been read
        /// </summary>
        private bool isSectorBytesRead;

        /// <summary>
        /// indicates if the sector has been updated
        /// </summary>
        private bool isSectorBytesUpdated;

        private bool hasSeeked;

        public SectorStream(Stream baseStream, int bufferSize = 1024 * 1024, bool byteSwap = false, bool leaveOpen = false)
        {
            this.stream = baseStream;
            this.bufferSize = bufferSize;
            this.byteSwap = byteSwap;
            this.leaveOpen = leaveOpen;
            this.sectorBytes = new byte[bufferSize];
            this.sectorBytesRead = 0;
            this.SectorBufferPosition = 0;
            this.streamOffset = 0;
            this.sectorOffset = 0;
            this.isSectorBytesRead = false;
            isSectorBytesUpdated = false;
            hasSeeked = false;
        }

        public int SectorBufferPosition { get; private set; }

        public override void Close()
        {
            if (stream != null && stream.CanWrite)
            {
                Flush();
            }

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
            if (isSectorBytesUpdated)
            {
                WriteSectorBytes();
            }
            
            stream.Flush();
        }

        private void ThrowIfNotDividableBySectorSize(string paramName, long value)
        {
            if (value % bufferSize == 0)
            {
                return;
            }

            throw new ArgumentOutOfRangeException(paramName, 
                $"Sector stream only supports values dividable by {bufferSize} and value is {value}");
        }
        
        private void ClearSectorBytes()
        {
            Array.Fill<byte>(sectorBytes, 0);
            sectorBytesRead = 0;
            isSectorBytesRead = false;
            isSectorBytesUpdated = false;
        }
        
        private static void ByteSwapBuffer(byte[] buffer, int count)
        {
            for (var i = 0; i < count - count % 2; i += 2)
            {
                (buffer[i + 1], buffer[i]) = (buffer[i], buffer[i + 1]);
            }
        }
        
        private void ReadSectorBytes(int count)
        {
            var bytesToRead = count;
            
            if (bytesToRead % SectorSize != 0)
            {
                bytesToRead += SectorSize - (bytesToRead % SectorSize);
            }

            if (!hasSeeked)
            {
                stream.Seek(sectorOffset, SeekOrigin.Begin);
            }
            
            var sectorBytesToRead = Math.Min(bytesToRead, bufferSize);
            sectorBytesRead = stream.Read(sectorBytes, 0, sectorBytesToRead);
            
            if (byteSwap)
            {
                ByteSwapBuffer(sectorBytes, sectorBytesRead);
            }

            isSectorBytesRead = true;
            isSectorBytesUpdated = false;
            hasSeeked = false;
        }

        /// <summary>
        /// Write sector bytes that was updated up to nearest sector size
        /// </summary>
        private void WriteSectorBytes()
        {
            var bytesToWrite = SectorBufferPosition;

            if (SectorBufferPosition % SectorSize != 0)
            {
                bytesToWrite += SectorSize - (bytesToWrite % SectorSize);
            }
            
            var sectorBytesToWrite = Math.Min(bytesToWrite, bufferSize);

            if (SectorBufferPosition < sectorBytesToWrite)
            {
                Array.Fill<byte>(sectorBytes, 0, SectorBufferPosition, sectorBytesToWrite - SectorBufferPosition);
            }
            
            if (byteSwap)
            {
                ByteSwapBuffer(sectorBytes, sectorBytesToWrite);
            }

            if (!hasSeeked)
            {
                stream.Seek(sectorOffset, SeekOrigin.Begin);
            }

            stream.Write(sectorBytes, 0, sectorBytesToWrite);
            
            isSectorBytesUpdated = false;
            hasSeeked = false;
        }

        private void MoveToNextSectorOffset()
        {
            SectorBufferPosition = 0;
            isSectorBytesRead = false;

            sectorOffset = streamOffset - (streamOffset % SectorSize);
            hasSeeked = false;
            isSectorBytesUpdated = false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;

            while (bytesRead < count && streamOffset < stream.Length)
            {
                if (!isSectorBytesRead)
                {
                    ReadSectorBytes(count);
                }
                
                // stop reading, if sector bytes read is zero
                if (sectorBytesRead == 0)
                {
                    break;
                }

                var sectorBufferCopyLength = Math.Min(count - bytesRead, sectorBytesRead - SectorBufferPosition);
                Array.Copy(this.sectorBytes, SectorBufferPosition, buffer, offset, sectorBufferCopyLength);
                SectorBufferPosition += sectorBufferCopyLength;
                offset += sectorBufferCopyLength;
                bytesRead += sectorBufferCopyLength;
                this.streamOffset += sectorBufferCopyLength;

                if (SectorBufferPosition >= sectorBytesRead)
                {
                    MoveToNextSectorOffset();
                }
            }

            return bytesRead;
        }

        /// <summary>
        /// Sets the position within the current stream the specified value. Internally sector stream will seek to nearest sector start offset.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.End)
            {
                throw new NotSupportedException("Sector stream doesn't support seeking to end origin");
            }

            if (isSectorBytesUpdated)
            {
                WriteSectorBytes();
            }

            streamOffset = origin == SeekOrigin.Begin ? offset : streamOffset + offset;
            var newSectorBufferOffset = streamOffset % SectorSize;
            var newSectorOffset = streamOffset - newSectorBufferOffset;

            SectorBufferPosition = Convert.ToInt32(newSectorBufferOffset);

            if (isSectorBytesRead && sectorOffset == newSectorOffset)
            {
                return streamOffset;
            }
            
            ClearSectorBytes();
            sectorOffset = newSectorOffset;
            isSectorBytesRead = false;
            isSectorBytesUpdated = false;
            hasSeeked = true;
            
            sectorOffset = stream.Seek(sectorOffset, origin);

            return streamOffset;
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var bytesWritten = 0;

            while (bytesWritten < count)
            {
                var sectorBufferCopyLength = Math.Min(count - bytesWritten, bufferSize - SectorBufferPosition);
                Array.Copy(buffer, offset, this.sectorBytes, SectorBufferPosition, sectorBufferCopyLength);
                SectorBufferPosition += sectorBufferCopyLength;
                offset += sectorBufferCopyLength;
                bytesWritten += sectorBufferCopyLength;
                isSectorBytesUpdated = true;

                streamOffset += sectorBufferCopyLength;
                
                if (SectorBufferPosition == bufferSize)
                {
                    WriteSectorBytes();
                    MoveToNextSectorOffset();
                }
            }
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;

        public override long Position
        {
            get => streamOffset;
            set => Seek(value, SeekOrigin.Begin);
        }
    }
}