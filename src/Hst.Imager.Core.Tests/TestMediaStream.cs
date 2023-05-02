namespace Hst.Imager.Core.Tests;

using System.IO;

public class TestMediaStream : Stream
{
    private readonly TestMedia testMedia;
    private readonly MemoryStream stream;

    public TestMediaStream(TestMedia testMedia)
    {
        this.testMedia = testMedia;
        this.stream = new MemoryStream();
        if (this.testMedia.Data != null && this.testMedia.Data.Length > 0)
        {
            this.stream.Write(this.testMedia.Data, 0, this.testMedia.Data.Length);
        }
    }

    protected override void Dispose(bool disposing)
    {
        this.testMedia.SetData(this.stream.ToArray()); 
        base.Dispose(disposing);
    }

    public override void Flush()
    {
        this.stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return this.stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return this.stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        this.stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.stream.Write(buffer, offset, count);
    }

    public override bool CanRead => this.stream.CanRead;
    public override bool CanSeek => this.stream.CanSeek;
    public override bool CanWrite => this.stream.CanWrite;
    public override long Length => this.stream.Length;
    public override long Position { get => this.stream.Position; set => this.stream.Position = value; }
}