using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Copies used sectors from a source stream to a sparse destination stream (sparse file or dynamic vhd).
/// Byte ranges marked free in allocation map and zero filled data are not written to destination,
/// so they remain unallocated in the destination.
/// </summary>
public class ImageCompactor
{
    /// <summary>
    /// Size of buffer used for reading source.
    /// </summary>
    public const int BufferSize = 1024 * 1024;

    /// <summary>
    /// Size of chunks checked for being zero filled. Matches NTFS sparse allocation unit of 64KB
    /// (16 clusters of 4KB) and divides vhd block size of 2MB.
    /// </summary>
    public const int ZeroCheckSize = 64 * 1024;

    private const int ProgressIntervalMilliseconds = 1000;

    public event EventHandler<DataProcessedEventArgs> DataProcessed;

    /// <summary>
    /// Copy used sectors from source to destination.
    /// </summary>
    /// <param name="source">Source stream to read from.</param>
    /// <param name="destination">Destination stream to write to. Destination must be zero filled.</param>
    /// <param name="size">Size to copy in bytes.</param>
    /// <param name="allocationMap">Allocation map with free byte ranges, which are not copied.</param>
    /// <param name="byteSwap">Byte swap data written to destination.</param>
    /// <param name="token">Cancellation token.</param>
    public async Task<Result<ImageCompactorStatistics>> Compact(Stream source, Stream destination, long size,
        AllocationMap allocationMap, bool byteSwap, CancellationToken token)
    {
        var buffer = new byte[BufferSize];
        var stopwatch = Stopwatch.StartNew();
        var lastProgress = TimeSpan.Zero;

        long bytesWritten = 0;
        long bytesFree = 0;
        long bytesZeroFilled = 0;

        OnDataProcessed(size, 0, stopwatch.Elapsed);

        for (long chunkOffset = 0; chunkOffset < size; chunkOffset += BufferSize)
        {
            if (token.IsCancellationRequested)
            {
                return new Result<ImageCompactorStatistics>(new Error("Cancelled"));
            }

            var chunkLength = (int)Math.Min(BufferSize, size - chunkOffset);
            long chunkBytesUsed = 0;

            foreach (var usedRange in allocationMap.GetUsedRanges(chunkOffset, chunkLength))
            {
                chunkBytesUsed += usedRange.Length;
                var bufferOffset = (int)(usedRange.Offset - chunkOffset);
                var length = (int)usedRange.Length;

                var bytesRead = await StreamReadHelper.ReadAt(source, usedRange.Offset, buffer, bufferOffset, length);
                if (bytesRead < length)
                {
                    Array.Clear(buffer, bufferOffset + bytesRead, length - bytesRead);
                }

                bytesWritten += await WriteNonZeroData(destination, buffer, chunkOffset, bufferOffset, length,
                    byteSwap, token);
            }

            bytesFree += chunkLength - chunkBytesUsed;

            var bytesProcessed = chunkOffset + chunkLength;
            bytesZeroFilled = bytesProcessed - bytesFree - bytesWritten;

            if ((stopwatch.Elapsed - lastProgress).TotalMilliseconds >= ProgressIntervalMilliseconds ||
                bytesProcessed >= size)
            {
                lastProgress = stopwatch.Elapsed;
                OnDataProcessed(size, bytesProcessed, stopwatch.Elapsed);
            }
        }

        await destination.FlushAsync(token);

        return new Result<ImageCompactorStatistics>(
            new ImageCompactorStatistics(size, bytesWritten, bytesFree, bytesZeroFilled));
    }

    /// <summary>
    /// Write data in buffer to destination skipping zero check sized chunks, which are zero filled.
    /// Chunks are aligned to absolute offsets, so skipped chunks match sparse allocation units.
    /// </summary>
    /// <returns>Number of bytes written.</returns>
    private static async Task<long> WriteNonZeroData(Stream destination, byte[] buffer, long chunkOffset,
        int bufferOffset, int length, bool byteSwap, CancellationToken token)
    {
        long bytesWritten = 0;
        var end = bufferOffset + length;
        var position = bufferOffset;
        var writeStart = -1;

        while (position < end)
        {
            // next zero check boundary aligned to absolute offset
            var absolutePosition = chunkOffset + position;
            var nextBoundary = (absolutePosition / ZeroCheckSize + 1) * ZeroCheckSize;
            var segmentEnd = (int)Math.Min(end, nextBoundary - chunkOffset);
            var isZeroFilled = !buffer.AsSpan(position, segmentEnd - position).ContainsAnyExcept((byte)0);

            if (isZeroFilled && writeStart >= 0)
            {
                bytesWritten += await Write(destination, buffer, chunkOffset, writeStart, position - writeStart,
                    byteSwap, token);
                writeStart = -1;
            }
            else if (!isZeroFilled && writeStart < 0)
            {
                writeStart = position;
            }

            position = segmentEnd;
        }

        if (writeStart >= 0)
        {
            bytesWritten += await Write(destination, buffer, chunkOffset, writeStart, end - writeStart, byteSwap,
                token);
        }

        return bytesWritten;
    }

    private static async Task<long> Write(Stream destination, byte[] buffer, long chunkOffset, int bufferOffset,
        int count, bool byteSwap, CancellationToken token)
    {
        if (byteSwap)
        {
            SwapBytes(buffer, bufferOffset, count);
        }

        destination.Seek(chunkOffset + bufferOffset, SeekOrigin.Begin);
        await destination.WriteAsync(buffer.AsMemory(bufferOffset, count), token);
        return count;
    }

    private static void SwapBytes(byte[] buffer, int offset, int count)
    {
        for (var i = offset; i + 1 < offset + count; i += 2)
        {
            (buffer[i], buffer[i + 1]) = (buffer[i + 1], buffer[i]);
        }
    }

    private void OnDataProcessed(long size, long bytesProcessed, TimeSpan timeElapsed)
    {
        var percentComplete = size == 0 ? 100 : Math.Round((double)100 / size * bytesProcessed, 1);
        var timeRemaining = TimeHelper.CalculateTimeRemaining(percentComplete, timeElapsed);
        var bytesPerSecond = timeElapsed.TotalSeconds > 0
            ? Convert.ToInt64(bytesProcessed / timeElapsed.TotalSeconds)
            : 0;

        DataProcessed?.Invoke(this, new DataProcessedEventArgs(false, percentComplete, bytesProcessed,
            size - bytesProcessed, size, timeElapsed, timeRemaining, timeElapsed + timeRemaining, bytesPerSecond));
    }
}
