using System.IO;

namespace Hst.Imager.Core.Tests
{
    using System.Threading.Tasks;
    using Commands;
    using Models;

    public abstract class CommandTestBase
    {
        public const int ImageSize = 512 * 512;
        
        protected async Task<byte[]> ReadMediaBytes(ICommandHelper commandHelper, string path, long? size = null)
        {
            var mediaResult = await commandHelper.GetReadableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
            stream.Position = 0;
            var readSize = size ?? media.Size;
            var bytes = new byte[readSize];
            var bytesRead = await stream.ReadAsync(bytes, 0, bytes.Length);

            return readSize != bytesRead
                ? throw new IOException($"Failed to read {readSize} bytes from {path}, instead read {bytesRead} bytes")
                : bytes;
        }
    }
}