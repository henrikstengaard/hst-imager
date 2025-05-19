using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hst.Imager.Core.FileSystems
{
    public static class FileSystemHelper
    {
        private static readonly byte[] Pfs3DosType = [0x50, 0x46, 0x53, 0x3];
        private static readonly byte[] Pds3DosType = [0x50, 0x44, 0x53, 0x3];

        public static bool IsPfs3DosType(byte[] dosType)
        {
            if (dosType.Length != 4)
            {
                throw new ArgumentException("DosType must be 4 bytes", nameof(dosType));
            }
            
            return dosType.SequenceEqual(Pds3DosType) || dosType.SequenceEqual(Pfs3DosType);
        }
        
        /// <summary>
        /// Pfs3 max partition size of 101.6 GB
        /// </summary>
        public const long Pfs3MaxPartitionSize = Amiga.FileSystems.Pfs3.Constants.MAXDISKSIZE1K * 512;

        /// <summary>
        /// Pfs3 experimental max partition size of 2TB
        /// </summary>
        public const long Pfs3MaxExperimentalPartitionSize = 1099511627776L * 2;
        
        public static bool IsPfs3PartitionSizeExperimental(long partitionSize) => partitionSize > Pfs3MaxPartitionSize;

        public static IEnumerable<long> CalculateRdbPartitionSizes(long size, long maxPartitionSize)
        {
            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0");
            }

            // calculate min partition size 20% of max partition size
            var minPartitionSize = Convert.ToInt64(maxPartitionSize * 0.2d);

            // calculate partition count
            var partitionCount = Convert.ToInt32(Math.Ceiling((double)size / maxPartitionSize));

            // calculate last partition size
            var lastPartitionSize = size % maxPartitionSize;
            
            var adjustLastPartitions = partitionCount >= 2 && lastPartitionSize >= 0 && lastPartitionSize < minPartitionSize;

            for (var partition = 0; partition < partitionCount; partition++)
            {
                switch (adjustLastPartitions)
                {
                    case true when partition == partitionCount - 2:
                        yield return maxPartitionSize - minPartitionSize;
                        continue;
                    case true when partition == partitionCount - 1:
                        yield return lastPartitionSize + minPartitionSize;
                        continue;
                    default:
                        yield return partition == partitionCount - 1 ? lastPartitionSize : maxPartitionSize;
                        break;
                }
            }
        }
        
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
