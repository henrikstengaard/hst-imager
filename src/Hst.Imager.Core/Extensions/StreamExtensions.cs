using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Extensions;

public static class StreamExtensions
{
    /// <summary>
    /// Fill buffer by continuously read from stream until buffer is filled (stops when read from stream returns 0 bytes)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static int Fill(this Stream stream, byte[] buffer, int offset, int count)
    {
        int bytesRead;
        do
        {
            bytesRead = stream.Read(buffer, offset, count - offset);
            offset += bytesRead;
        } while (bytesRead > 0 && offset < count);

        return offset;
    }
    
    /// <summary>
    /// Fill buffer by continuously read from stream until buffer is filled (stops when read from stream returns 0 bytes)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static async Task<int> FillAsync(this Stream stream, byte[] buffer, int offset, int count)
    {
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, offset, count - offset);
            offset += bytesRead;
        } while (bytesRead > 0 && offset < count);

        return offset;
    }
}