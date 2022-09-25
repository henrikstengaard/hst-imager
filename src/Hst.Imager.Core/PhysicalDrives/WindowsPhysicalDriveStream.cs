namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Apis;

    public class WindowsPhysicalDriveStream : Stream
    {
        private readonly long size;
        private readonly IEnumerable<string> driveLetters;

        private readonly Win32RawDisk win32RawDisk;
        private long position;

        public WindowsPhysicalDriveStream(string path, long size, IEnumerable<string> driveLetters, bool writable)
        {
            this.size = size;
            this.driveLetters = driveLetters;
            this.CanWrite = writable;
            this.win32RawDisk = new Win32RawDisk(path, writable);
            this.position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                win32RawDisk.Dispose();

                foreach (var driveLetter in driveLetters)
                {
                    using var driveLetterWin32RawDisk = new Win32RawDisk(driveLetter, this.CanWrite);
                    driveLetterWin32RawDisk.UnlockDevice();
                }
            }

            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Seek(position, SeekOrigin.Begin);
            
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
            Seek(position, SeekOrigin.Begin);
            
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
        public override bool CanWrite { get; }

        public override long Length => size;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        } 
    }
}