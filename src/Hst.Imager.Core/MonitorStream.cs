namespace Hst.Imager.Core;

using System.Collections.Generic;
using System.IO;

public class MonitorStream : Stream
{
    private readonly Stream stream;
    public readonly IList<long> Seeks;
    public readonly IList<long> Reads;
    public readonly IList<long> Writes;

    public MonitorStream(Stream stream)
    {
        this.stream = stream;
        this.Seeks = new List<long>();
        this.Reads = new List<long>();
        this.Writes = new List<long>();
    }

    public override void Flush()
    {
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Reads.Add(stream.Position);
        return stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Seeks.Add(offset);
        return stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Writes.Add(stream.Position);
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