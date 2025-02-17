using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.FileSystems
{
    public static class Pfs3AioFileSystemHelper
    {
        public const string Pfs3AioLhaUrl = "https://aminet.net/disk/misc/pfs3aio.lha";
        public const string Pfs3AioLhaFilename = "pfs3aio.lha";

        /// <summary>
        /// Download pfs3aio from aminet.
        /// </summary>
        /// <param name="pfs3AioDirPath">Path to download pfs3aio.lha to.</param>
        /// <returns>Path to downloaded pfs3aio.lha.</returns>
        /// <exception cref="IOException"></exception>
        public static async Task<string> DownloadPfs3AioLha(string pfs3AioDirPath)
        {
            ArgumentNullException.ThrowIfNull(pfs3AioDirPath);

            if (!Directory.Exists(pfs3AioDirPath))
            {
                Directory.CreateDirectory(pfs3AioDirPath);
            }

            var pfs3AioLhaPath = Path.Combine(pfs3AioDirPath, Pfs3AioLhaFilename);

            if (File.Exists(pfs3AioLhaPath))
            {
                return pfs3AioLhaPath;
            }
            
            using var client = new HttpClient();
            try
            {
                var pfs3AioLhaBytes = await client.GetByteArrayAsync(Pfs3AioLhaUrl);

                await File.WriteAllBytesAsync(pfs3AioLhaPath, pfs3AioLhaBytes);
            }
            catch (Exception ex)
            {
                throw new IOException($"An error occurred while downloading '{Pfs3AioLhaUrl}'", ex);
            }

            return pfs3AioLhaPath;
        }
    }
}
