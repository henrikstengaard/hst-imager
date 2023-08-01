using System.IO;

namespace Hst.Imager.Core;

public class MediaStream : Stream
{
    private readonly Stream stream;

    public MediaStream(Stream stream, long size)
    {
        this.stream = stream;
        Length = size;
    }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new IOException("Media stream doesn't support set length");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        stream.Write(buffer, offset, count);
    }

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => stream.CanWrite;
    public override long Length { get; }
    public override long Position { get => stream.Position; set => stream.Position = value; }
}