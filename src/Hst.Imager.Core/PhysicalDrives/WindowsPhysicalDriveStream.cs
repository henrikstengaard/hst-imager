namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Apis;

    public class WindowsPhysicalDriveStream(
        string path,
        long size,
        bool writable,
        IEnumerable<Win32RawDisk> dismountedDrives)
        : Stream
    {
        private readonly Win32RawDisk win32RawDisk = new(path, writable);
        private long position = 0;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                win32RawDisk.Dispose();

                foreach (var dismountedDrive in dismountedDrives)
                {
                    dismountedDrive.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new ArgumentException("'Only offset 0 is allowed", nameof(offset));
            }

            var bytesRead = Convert.ToInt32(win32RawDisk.Read(buffer, count));
            position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            position = win32RawDisk.Seek(offset, origin);
            return position;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new ArgumentException("Only offset 0 is allowed", nameof(offset));
            }

            var bytesWritten = win32RawDisk.Write(buffer, count);
            position += bytesWritten;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) =>
            throw new NotSupportedException("Physical device doesn't support set length");

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite { get; } = writable;

        public override long Length => size;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        } 
    }
}