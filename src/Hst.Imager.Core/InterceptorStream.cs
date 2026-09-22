using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hst.Imager.Core;

/// <summary>
/// interceptor stream override behavior of base stream for read, write, close, length
/// </summary>
public class InterceptorStream : Stream
{
    private readonly Stream baseStream;
    private readonly long? length;
    private readonly Func<byte[], int, int, int> readHandler;
    private readonly Func<byte[], int, int, Task<int>> readAsyncHandler;
    private readonly Action<byte[], int, int> writeHandler;
    private readonly Func<long, SeekOrigin, long> seekHandler;
    private readonly Action<long> setLengthHandler;
    private readonly Action closeHandler;

    public InterceptorStream(Stream baseStream, long? length = null,
        Func<byte[], int, int, int> readHandler = null,
        Func<byte[], int, int, Task<int>> readAsyncHandler = null,
        Action<byte[], int, int> writeHandler = null,
        Func<long, SeekOrigin, long> seekHandler = null,
        Action<long> setLengthHandler = null,
        Action closeHandler = null)
    {
        this.baseStream = baseStream;
        this.setLengthHandler = setLengthHandler;
        this.seekHandler = seekHandler;
        this.length = length;
        this.readHandler = readHandler;
        this.readAsyncHandler = readAsyncHandler;
        this.writeHandler = writeHandler;
        this.closeHandler = closeHandler;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (!disposing)
            {
                return;
            }

            baseStream?.Dispose();

            closeHandler?.Invoke();
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
        return readHandler?.Invoke(buffer, offset, count) ?? baseStream.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (readAsyncHandler != null)
        {
            return readAsyncHandler.Invoke(buffer, offset, count);
        }

        return readHandler != null
            ? Task.FromResult(readHandler.Invoke(buffer, offset, count))
            : baseStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (seekHandler == null)
        {
            return baseStream.Seek(offset, origin);
        }

        return seekHandler.Invoke(offset, origin);
    }

    public override void SetLength(long value)
    {
        if (setLengthHandler == null)
        {
            baseStream.SetLength(value);
            return;
        }
        
        setLengthHandler.Invoke(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (writeHandler == null)
        {
            baseStream.Write(buffer, offset, count);
            return;
        }
        writeHandler.Invoke(buffer, offset, count);
    }

    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length => length ?? baseStream.Length;

    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }
}