using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Hst.Imager.Core;

public class ActivityMonitorStream : Stream
{
    private readonly Stream stream;
    private readonly IList<IStreamActivity> activities;
    public IReadOnlyCollection<IStreamActivity> Activities => new ReadOnlyCollection<IStreamActivity>(activities);

    public ActivityMonitorStream(Stream stream)
    {
        this.stream = stream;
        this.activities = new List<IStreamActivity>();
    }

    public override void Flush()
    {
        activities.Add(new FlushActivity(DateTime.Now));
        stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        activities.Add(new ReadActivity(DateTime.Now, stream.Position, offset, count));
        return stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        activities.Add(new SeekActivity(DateTime.Now, stream.Position, offset, origin));
        return stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        activities.Add(new WriteActivity(DateTime.Now, stream.Position, offset, count));
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