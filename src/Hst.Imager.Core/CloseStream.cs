using System.IO;

namespace Hst.Imager.Core;

/// <summary>
/// close stream that uses a base stream for read, write seek and a close stream that is closed on dispose
/// </summary>
public class CloseStream : Stream
{
    private readonly Stream baseStream;
    private readonly Stream closeStream;

    public CloseStream(Stream baseStream, Stream closeStream)
    {
        this.baseStream = baseStream;
        this.closeStream = closeStream;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (baseStream != null && baseStream.CanWrite)
                {
                    baseStream.Flush();
                }
                baseStream?.Dispose();
                
                if (closeStream != null && closeStream.CanWrite)
                {
                    closeStream.Flush();
                }
                closeStream?.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
        
    public override void Flush()
    {
        baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return baseStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        baseStream.Write(buffer, offset, count);
    }

    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length => baseStream.Length;
    public override long Position { get => baseStream.Position; set => baseStream.Position = value; }
}