using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Optimization;

public static class StreamReadHelper
{
    /// <summary>
    /// Read bytes at offset. Bytes beyond end of stream are returned as zeros.
    /// </summary>
    public static async Task<byte[]> ReadBytesAt(Stream stream, long offset, int count)
    {
        var buffer = new byte[count];
        await ReadAt(stream, offset, buffer, 0, count);
        return buffer;
    }

    /// <summary>
    /// Read bytes at offset into buffer until count is read or end of stream is reached.
    /// </summary>
    public static async Task<int> ReadAt(Stream stream, long offset, byte[] buffer, int bufferOffset, int count)
    {
        stream.Seek(offset, SeekOrigin.Begin);

        var totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = await stream.ReadAsync(buffer, bufferOffset + totalRead, count - totalRead);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }
}
