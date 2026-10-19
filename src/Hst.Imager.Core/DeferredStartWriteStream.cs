namespace Hst.Imager.Core
{
    using System;
    using System.IO;

    /// <summary>
    /// Stream deferring writes to the start of the base stream until disposed. Used for physical drives on Windows,
    /// which detects changes to partition tables (MBR, GPT) at the start of a disk and automatically mounts
    /// partitions it can read. Writing the start of the disk last prevents Windows from mounting partitions, while
    /// the rest of the partitions are still being written.
    /// Base stream must support reads and writes with offset 0 only, e.g. Windows physical drive stream.
    /// </summary>
    public class DeferredStartWriteStream : Stream
    {
        public const int DefaultStartSize = 1024 * 1024;

        private readonly Stream baseStream;
        private readonly byte[] startBytes;
        private bool isStartBytesRead;
        private bool isStartBytesUpdated;
        private long position;
        private bool disposed;

        public DeferredStartWriteStream(Stream baseStream, int startSize = DefaultStartSize)
        {
            this.baseStream = baseStream;
            startBytes = new byte[Math.Min(startSize, baseStream.Length)];
            isStartBytesRead = false;
            isStartBytesUpdated = false;
            position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    try
                    {
                        WriteStartBytes();
                    }
                    finally
                    {
                        baseStream.Dispose();
                    }
                }
            }
            finally
            {
                disposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Write deferred start bytes to base stream, if start bytes has been updated.
        /// </summary>
        private void WriteStartBytes()
        {
            if (!isStartBytesUpdated)
            {
                return;
            }

            baseStream.Seek(0, SeekOrigin.Begin);
            baseStream.Write(startBytes, 0, startBytes.Length);
            baseStream.Flush();
            isStartBytesUpdated = false;
        }

        /// <summary>
        /// Read start bytes from base stream, so parts of start not written are preserved when writing start bytes.
        /// </summary>
        private void EnsureStartBytesRead()
        {
            if (isStartBytesRead)
            {
                return;
            }

            baseStream.Seek(0, SeekOrigin.Begin);
            var bytesRead = ReadFromBase(startBytes, 0, startBytes.Length);
            if (bytesRead < startBytes.Length)
            {
                Array.Fill<byte>(startBytes, 0, bytesRead, startBytes.Length - bytesRead);
            }

            isStartBytesRead = true;
        }

        private int ReadFromBase(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            var readBuffer = offset == 0 ? buffer : new byte[count];

            while (bytesRead < count)
            {
                var readBytes = bytesRead == 0 ? readBuffer : new byte[count - bytesRead];
                var length = baseStream.Read(readBytes, 0, count - bytesRead);
                if (length == 0)
                {
                    break;
                }

                if (bytesRead > 0)
                {
                    Array.Copy(readBytes, 0, readBuffer, bytesRead, length);
                }

                bytesRead += length;
            }

            if (offset != 0)
            {
                Array.Copy(readBuffer, 0, buffer, offset, bytesRead);
            }

            return bytesRead;
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;

            if (count > 0 && position < startBytes.Length)
            {
                EnsureStartBytesRead();
                var length = (int)Math.Min(count, startBytes.Length - position);
                Array.Copy(startBytes, position, buffer, offset, length);
                position += length;
                offset += length;
                count -= length;
                bytesRead += length;
            }

            if (count <= 0 || position >= baseStream.Length)
            {
                return bytesRead;
            }

            baseStream.Seek(position, SeekOrigin.Begin);
            var baseBytesRead = ReadFromBase(buffer, offset, count);
            position += baseBytesRead;

            return bytesRead + baseBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => baseStream.Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            return position;
        }

        public override void SetLength(long value) => baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0 && position < startBytes.Length)
            {
                EnsureStartBytesRead();
                var length = (int)Math.Min(count, startBytes.Length - position);
                Array.Copy(buffer, offset, startBytes, position, length);
                isStartBytesUpdated = true;
                position += length;
                offset += length;
                count -= length;
            }

            if (count <= 0)
            {
                return;
            }

            var writeBuffer = buffer;
            if (offset != 0)
            {
                writeBuffer = new byte[count];
                Array.Copy(buffer, offset, writeBuffer, 0, count);
            }

            baseStream.Seek(position, SeekOrigin.Begin);
            baseStream.Write(writeBuffer, 0, count);
            position += count;
        }

        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => baseStream.CanWrite;
        public override long Length => baseStream.Length;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }
    }
}
