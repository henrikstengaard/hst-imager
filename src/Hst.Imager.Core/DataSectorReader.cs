namespace Hst.Imager.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class DataSectorReader
    {
        private readonly Stream stream;
        private readonly int sectorSize;
        private readonly int bufferSize;
        private readonly byte[] buffer;
        private long offset;

        public DataSectorReader(Stream stream, int sectorSize = 512, int bufferSize = 1024 * 1024)
        {
            if (sectorSize % 512 != 0)
            {
                throw new ArgumentException("Sector size must be dividable by 512", nameof(sectorSize));
            }

            if (bufferSize % 512 != 0)
            {
                throw new ArgumentException("Buffer size must be dividable by 512", nameof(bufferSize));
            }

            this.stream = stream;
            this.sectorSize = sectorSize;
            this.bufferSize = bufferSize;
            this.buffer = new byte[bufferSize];
            offset = this.stream.Position;
        }

        public async Task<SectorResult> ReadNext(int? length = null)
        {
            var startOffset = offset;
            var readBytes = length.HasValue && length.Value > 0 && length < bufferSize ? length.Value : bufferSize;
            var bytesRead = await stream.ReadAsync(buffer, 0, readBytes);

            var sectors = new List<Sector>();

            for (var start = 0; start < bytesRead; start += sectorSize)
            {
                var data = new byte[sectorSize];
                Array.Copy(buffer, start, data, 0, sectorSize);
                var sectorStart = offset + start;
                var sectorEnd = sectorStart + sectorSize - 1;

                sectors.Add(new Sector
                {
                    Start = sectorStart,
                    End = sectorEnd,
                    Size = sectorSize,
                    IsZeroFilled = IsZeroFilled(data, 0, sectorSize - 1),
                    Data = data
                });
            }

            offset += bytesRead;
            var endOffset = offset - 1;

            return new SectorResult
            {
                Start = startOffset,
                End = endOffset,
                Data = buffer,
                BytesRead = bytesRead,
                EndOfSectors = bytesRead != bufferSize,
                Sectors = sectors
            };
        }

        private bool IsZeroFilled(byte[] data, int start, int end)
        {
            for (var i = start; i <= end; i++)
            {
                if (data[i] != 0 || data[end - i] != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}