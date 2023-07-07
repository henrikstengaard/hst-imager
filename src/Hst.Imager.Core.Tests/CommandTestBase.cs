namespace Hst.Imager.Core.Tests
{
    using System.Threading.Tasks;
    using Commands;
    using Models;
    using Xunit;

    public abstract class CommandTestBase
    {
        public readonly int ImageSize = 512 * 512;
        
        protected async Task<byte[]> ReadMediaBytes(ICommandHelper commandHelper, string path, long size)
        {
            var mediaResult = commandHelper.GetReadableFileMedia(path);
            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
            var bytes = new byte[size];
            var bytesRead = await stream.ReadAsync(bytes, 0, bytes.Length);

            Assert.Equal(size, bytesRead);

            return bytes;
        }
    }
}