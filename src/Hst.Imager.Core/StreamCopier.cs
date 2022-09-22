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

            timer.Start();

            long bytesProcessed = 0;
            int length;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return new Result<Error>(new Error("Cancelled"));
                }

                var sectorResult = await dataSectorReader.ReadNext();
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

                OnDataProcessed(percentComplete, bytesProcessed, bytesRemaining, size, timeElapsed, timeRemaining,
                    timeTotal);
            } while (length == bufferSize && bytesProcessed < size);

            timer.Stop();
            stopwatch.Stop();

            sendDataProcessed = true;
            OnDataProcessed(100, bytesProcessed, 0, size, stopwatch.Elapsed, TimeSpan.Zero, stopwatch.Elapsed);

            return new Result();
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal)
        {
            if (!sendDataProcessed)
            {
                return;
            }

            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal));
            sendDataProcessed = false;
        }
    }
}