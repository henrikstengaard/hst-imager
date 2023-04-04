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
        private readonly bool leaveOpen;
        private const int SectorSize = 512;

        public SectorStream(Stream baseStream, bool leaveOpen = false)
        {
            this.stream = baseStream;
            this.leaveOpen = leaveOpen;
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

            if (count % SectorSize == 0)
            {
                return stream.Read(buffer, offset, count);
            }
            
            Console.WriteLine($"read count {count}");
            var sectorBuffer = new byte[count - (count % SectorSize) + SectorSize];
            var sectorBytesRead = stream.Read(sectorBuffer, offset, sectorBuffer.Length);
            var bytesRead = Math.Min(count, sectorBytesRead);
            Array.Copy(sectorBuffer, 0, buffer, 0, bytesRead);
            return bytesRead;
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
            if (offset != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"Sector stream offset {offset} not supported, only offset 0");
            }

            if (count % SectorSize == 0)
            {
                stream.Write(buffer, 0, count);
                return;
            }
            
            Console.WriteLine($"write count {count} -> {SectorSize}");
            var sectorBuffer = new byte[count - (count % SectorSize) + SectorSize];
            Array.Copy(buffer, 0, sectorBuffer, 0, count);
            stream.Write(sectorBuffer, 0, sectorBuffer.Length);
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