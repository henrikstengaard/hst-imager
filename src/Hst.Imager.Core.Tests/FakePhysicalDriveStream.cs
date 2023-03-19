namespace Hst.Imager.Core.Tests;

using System;
using System.IO;

/// <summary>
/// fake physical drive stream to simulate behavior of physical drive
/// </summary>
public class FakePhysicalDriveStream : Stream
{
    private readonly Stream stream;

    public FakePhysicalDriveStream(Stream stream)
    {
        this.stream = stream;
    }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (offset != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Only offset 0 is supported");
        }

        if (count % 512 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be dividable by 512");
        }
            
        return stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset % 512 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "offset must be dividable by 512");
        }
        if (origin != SeekOrigin.Begin)
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "Only begin origin is supported");
        }
            
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
            throw new ArgumentOutOfRangeException(nameof(offset), "Only offset 0 is supported");
        }

        if (count % 512 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be dividable by 512");
        }
            
        stream.Write(buffer, offset, count);
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