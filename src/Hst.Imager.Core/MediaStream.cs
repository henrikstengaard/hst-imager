using System.IO;

namespace Hst.Imager.Core;

/// <summary>
/// Media stream is used to control the medias stream length.
/// </summary>
public class MediaStream : Stream
{
    protected readonly Stream Stream;

    public MediaStream(Stream stream, long size)
    {
        this.Stream = stream;
        Length = size;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }
        
        Stream.Flush();
    }

    public override void Flush()
    {
        Stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return Stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new IOException("Media stream doesn't support set length");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Stream.Write(buffer, offset, count);
    }

    public override bool CanRead => Stream.CanRead;
    public override bool CanSeek => Stream.CanSeek;
    public override bool CanWrite => Stream.CanWrite;
    public override long Length { get; }
    public override long Position { get => Stream.Position; set => Stream.Position = value; }
}