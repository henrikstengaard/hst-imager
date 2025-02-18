using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.FileSystems
{
    public static class FileSystemHelper
    {
        public const string Pfs3AioLhaUrl = "https://aminet.net/disk/misc/pfs3aio.lha";
        public const string Pfs3AioLhaFilename = "pfs3aio.lha";

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

        /// <summary>
        /// Download pfs3aio from aminet.
        /// </summary>
        /// <param name="pfs3AioDirPath">Path to download pfs3aio.lha to.</param>
        /// <returns>Path to downloaded pfs3aio.lha.</returns>
        /// <exception cref="IOException"></exception>
        public static async Task<string> DownloadPfs3AioLha(string pfs3AioDirPath)
        {
            ArgumentNullException.ThrowIfNull(pfs3AioDirPath);

            return await DownloadFile(Pfs3AioLhaUrl, pfs3AioDirPath, Pfs3AioLhaFilename);
        }
    }
}
