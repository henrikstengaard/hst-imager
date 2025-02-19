using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hst.Imager.Core.FileSystems
{
    public static class FileSystemHelper
    {
        public static async Task<string> DownloadFile(string url, string outputPath, string filename)
        {
            ArgumentNullException.ThrowIfNull(outputPath);

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var filePath = Path.Combine(outputPath, filename);

            if (File.Exists(filePath))
            {
                return filePath;
            }

            using var client = new HttpClient();
            try
            {
                var pfs3AioLhaBytes = await client.GetByteArrayAsync(url);

                await File.WriteAllBytesAsync(filePath, pfs3AioLhaBytes);
            }
            catch (Exception ex)
            {
                throw new IOException($"An error occurred while downloading '{url}'", ex);
            }

            return filePath;
        }
    }
}
