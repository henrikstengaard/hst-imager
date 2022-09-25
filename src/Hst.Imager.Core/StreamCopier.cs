namespace Hst.Imager.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Helpers;
    using Hst.Core;

    public class StreamCopier
    {
        private readonly int bufferSize;
        private readonly System.Timers.Timer timer;
        private bool sendDataProcessed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public StreamCopier(int bufferSize = 1024 * 1024)
        {
            this.bufferSize = bufferSize;
            timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Elapsed += (_, _) => sendDataProcessed = true;
            sendDataProcessed = false;
        }

        public async Task<Result> Copy(CancellationToken token, Stream source, Stream destination, long size,
            long sourceOffset = 0, long destinationOffset = 0, bool skipZeroFilled = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            source.Seek(sourceOffset, SeekOrigin.Begin);
            destination.Seek(destinationOffset, SeekOrigin.Begin);

            var dataSectorReader = new DataSectorReader(source, bufferSize: bufferSize);

            sendDataProcessed = true;
            OnDataProcessed(0, 0, size, size, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
            
            timer.Start();

            long bytesProcessed = 0;
            int length;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return new Result<Error>(new Error("Cancelled"));
                }

                var readBytes = Convert.ToInt32(bytesProcessed + bufferSize > size ? size - bytesProcessed : bufferSize);
                SectorResult sectorResult;
                try
                {
                    sectorResult = await dataSectorReader.ReadNext(readBytes);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"processed: {bytesProcessed}, size {size}, buffer size {bufferSize}, read bytes {readBytes}     -");
                    Console.WriteLine(e);
                    throw;
                }
                length = Convert.ToInt32(bytesProcessed + sectorResult.BytesRead > size
                    ? size - bytesProcessed
                    : sectorResult.BytesRead);

                if (skipZeroFilled)
                {
                    foreach (var sector in sectorResult.Sectors.Where(x => !x.IsZeroFilled))
                    {
                        var sectorOffsetDiff = sourceOffset == 0 ? sector.Start : sector.Start - sourceOffset;

                        if (sectorOffsetDiff > size)
                        {
                            break;
                        }

                        destination.Seek(destinationOffset + sectorOffsetDiff, SeekOrigin.Begin);

                        var sectorSize = Convert.ToInt32(bytesProcessed + sector.Data.Length > size
                            ? size - bytesProcessed
                            : sector.Data.Length);

                        await destination.WriteAsync(sector.Data, 0, sectorSize, token);
                    }
                }
                else
                {
                    await destination.WriteAsync(sectorResult.Data, 0, length, token);
                }

                bytesProcessed += length;
                var bytesRemaining = size - bytesProcessed;
                var percentComplete = bytesProcessed == 0 ? 0 : Math.Round((double)100 / size * bytesProcessed, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = TimeHelper.CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = timeElapsed + timeRemaining;
                var bytesPerSecond = Convert.ToInt64(bytesProcessed / timeElapsed.TotalSeconds);

                OnDataProcessed(percentComplete, bytesProcessed, bytesRemaining, size, timeElapsed, timeRemaining,
                    timeTotal, bytesPerSecond);
            } while (length == bufferSize && bytesProcessed < size);

            timer.Stop();
            stopwatch.Stop();

            sendDataProcessed = true;
            OnDataProcessed(100, bytesProcessed, 0, size, stopwatch.Elapsed, TimeSpan.Zero, stopwatch.Elapsed, 0);

            return new Result();
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            if (!sendDataProcessed)
            {
                return;
            }

            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
            sendDataProcessed = false;
        }
    }
}