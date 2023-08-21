using System;
using System.IO;

namespace Hst.Imager.Core.Tests;

public class PhysicalDriveTestStream : Stream
{
    private readonly Stream baseStream;

    public PhysicalDriveTestStream(Stream baseStream)
    {
        this.baseStream = baseStream;
    }

    public override void Flush()
    {
        baseStream.Flush();
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
            
        if (baseStream.Position + count > baseStream.Length)
        {
            throw new IOException($"Read count {count} at position {baseStream.Position} exceeds size {baseStream.Length}");
        }

        return baseStream.Read(buffer, offset, count);
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
            
        if (offset > baseStream.Length)
        {
            throw new IOException($"Seek offset {offset} exceeds size {baseStream.Length}");
        }

        return baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Physical drive doesn't support set length as it's given by media size");
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
            
        if (baseStream.Position + count > baseStream.Length)
        {
            throw new IOException($"Write count {count} at position {baseStream.Position} exceeds size {baseStream.Length}");
        }

        baseStream.Write(buffer, offset, count);
    }

    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length => baseStream.Length;
    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }
}