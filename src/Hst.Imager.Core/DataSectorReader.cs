namespace Hst.Imager.Core;

using System;
using System.Collections.Generic;

public static class DataSectorReader
{
    /// <summary>
    /// Reads sectors containing data, optionally include zero filled sectors
    /// </summary>
    /// <param name="data"></param>
    /// <param name="sectorSize"></param>
    /// <param name="length"></param>
    /// <param name="includeZeroFilled"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static IEnumerable<Sector> Read(byte[] data, int sectorSize = 512, int? length = null, bool includeZeroFilled = false)
    {
        if (data.Length % 512 != 0)
        {
            throw new ArgumentException("Data length must be dividable by 512", nameof(data));
        }

        if (length.HasValue && length > data.Length)
        {
            throw new ArgumentException($"Length {length} is greater than data length {data.Length}", nameof(length));
        }
        
        for (var start = 0; start < (length ?? data.Length); start += sectorSize)
        {
            var isZeroFilled = IsZeroFilled(data, start, sectorSize);

            if (isZeroFilled && !includeZeroFilled)
            {
                continue;
            }
            
            yield return new Sector
            {
                Start = start,
                End = start + sectorSize - 1,
                Size = sectorSize,
                IsZeroFilled = isZeroFilled
            };
        }
    }

    public static bool IsZeroFilled(byte[] data, int offset, int count)
    {
        var end = count - 1;
        for (var start = 0; start < count && start <= end; start++, end--)
        {
            if (data[offset + start] == 0 && data[offset + end] == 0)
            {
                continue;
            }

            return false;
        }

        return true;
    }
}