namespace Hst.Imager.Core
{
    using System;
    using System.IO;

    /// <summary>
    /// Sector stream that ensures offsets and sizes are dividable by sector size 512
    /// </summary>
    public class SectorStream : Stream
    {
        private readonly Stream stream;
        private readonly bool readFill;
        private readonly bool writeFill;
        private const int SectorSize = 512;

        public SectorStream(Stream baseStream, bool readFill, bool writeFill)
        {
            this.stream = baseStream;
            this.readFill = readFill;
            this.writeFill = writeFill;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        private static void ThrowIfValueIsNotASector(long value)
        {
            if (value % SectorSize == 0)
            {
                return;
            }

            throw new IOException($"Sector stream only supports offset and size dividable by {SectorSize}");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (readFill & count % SectorSize != 0)
            {
                var filledBuffer = new byte[count - (count % SectorSize) + SectorSize];
                var bytesRead = stream.Read(filledBuffer, offset, filledBuffer.Length);
                var length = Math.Min(bytesRead, count);
                Array.Copy(filledBuffer, 0, buffer, 0, length);
                return length;
            }
            ThrowIfValueIsNotASector(count);
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfValueIsNotASector(offset);
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (writeFill & count % SectorSize != 0)
            {
                var filledBuffer = new byte[count - (count % SectorSize) + SectorSize];
                stream.Write(filledBuffer, offset, filledBuffer.Length);
                return;
            }
            ThrowIfValueIsNotASector(count);
            stream.Write(buffer, offset, count);
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;

        public override long Position
        {
            get => stream. Position;
            set => stream. Position = value;
        }
    }
}