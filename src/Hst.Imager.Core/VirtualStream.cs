using System;
using System.IO;

namespace Hst.Imager.Core;

public class VirtualStream : Stream
{
    private readonly Stream stream;
    private readonly long startOffset;
    private readonly long maxSize;
    private bool hasSeekedToCurrentOffset;
    private long currentSize;
    private long currentOffset;

    public VirtualStream(Stream stream, long startOffset, long maxSize = 0, long currentSize = 0)
    {
        if (currentSize > maxSize)
        {
            throw new ArgumentOutOfRangeException(nameof(currentSize), $"Current size {currentSize} is larger than max size {maxSize}");
        }

        this.stream = stream;
        this.startOffset = startOffset;
        this.maxSize = maxSize;
        hasSeekedToCurrentOffset = startOffset == 0;
        this.currentSize = currentSize;
        this.currentOffset = 0;
    }

    public override bool CanRead => stream.CanRead;

    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;

    public override long Length => this.currentSize;

    public override long Position
    {
        get => currentOffset;
        set
        {
            Seek(value, SeekOrigin.Begin);
        }
    }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!hasSeekedToCurrentOffset)
        {
            Seek(currentOffset, SeekOrigin.Begin);
        }

        var readLength = maxSize > 0 && Position + count > maxSize ? (int)(maxSize - Position) : count; 

        var bytesRead = stream.Read(buffer, offset, readLength);

        currentOffset += bytesRead;

        if (currentOffset > currentSize)
        {
            currentSize = currentOffset;
        }
        
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin != SeekOrigin.Begin)
        {
            throw new IOException("Only origin begin is supported");
        }

        var newOffset = stream.Seek(this.startOffset + offset, origin);
        hasSeekedToCurrentOffset = true;
        currentOffset = newOffset - this.startOffset;
        return newOffset;
    }

    public override void SetLength(long value)
    {
        if (value > this.maxSize)
        {
            throw new IOException($"Stream size can not be larger than {this.maxSize}");
        }

        this.currentSize = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!hasSeekedToCurrentOffset)
        {
            Seek(currentOffset, SeekOrigin.Begin);
        }

        var writeLength = maxSize > 0 && Position + count > maxSize ? (int)(maxSize - Position) : count;

        stream.Write(buffer, offset, writeLength);

        currentOffset += writeLength;

        if (currentOffset > currentSize)
        {
            currentSize = currentOffset;
        }
    }
}