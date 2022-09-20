﻿namespace HstWbInstaller.Imager.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Helpers;
    using Hst.Core;

    public class ImageConverter
    {
        private readonly int bufferSize;
        private readonly System.Timers.Timer timer;
        private bool sendDataProcessed;
        
        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public ImageConverter(int bufferSize = 1024 * 1024)
        {
            this.bufferSize = bufferSize;
            timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Elapsed += (_, _) => sendDataProcessed = true;
            sendDataProcessed = false;
        }

        public async Task<Result> Convert(CancellationToken token, Stream source, Stream destination, long size, bool skipZeroFilled = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            source.Seek(0, SeekOrigin.Begin);
            destination.Seek(0, SeekOrigin.Begin);
            
            var dataSectorReader = new DataSectorReader(source, bufferSize: bufferSize);
            
            timer.Start();
            
            var bytesProcessed = 0L;
            long bytesRead = 0;
            SectorResult sectorResult;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return new Result<Error>(new Error("Cancelled"));
                }
                
                sectorResult = await dataSectorReader.ReadNext();
                bytesRead += sectorResult.BytesRead;

                if (skipZeroFilled)
                {
                    foreach (var sector in sectorResult.Sectors.Where(x => x.Start < size))
                    {
                        destination.Seek(sector.Start, SeekOrigin.Begin);
                        await destination.WriteAsync(sector.Data, 0, sector.Data.Length, token);
                    }
                }
                else
                {
                    var length = sectorResult.End > size ? size - sectorResult.Start : sectorResult.Data.Length;
                    await destination.WriteAsync(sectorResult.Data, 0, System.Convert.ToInt32(length), token);
                }

                var sectorBytesProcessed = sectorResult.End >= size ? size - sectorResult.Start : sectorResult.BytesRead;
                bytesProcessed += sectorBytesProcessed;
                var bytesRemaining = size - bytesProcessed;
                var percentComplete = bytesProcessed == 0 ? 0 : Math.Round((double)100 / size * bytesProcessed, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = TimeHelper.CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = timeElapsed + timeRemaining;
                
                OnDataProcessed(percentComplete, bytesProcessed, bytesRemaining, size, timeElapsed, timeRemaining, timeTotal);
            } while (sectorResult.BytesRead == bufferSize && bytesRead < size && !sectorResult.EndOfSectors);

            timer.Stop();
            stopwatch.Stop();

            sendDataProcessed = true;
            OnDataProcessed(100, bytesProcessed, 0, size, stopwatch.Elapsed, TimeSpan.Zero, stopwatch.Elapsed);
            
            return new Result();
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal, TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal)
        {
            if (!sendDataProcessed)
            {
                return;
            }
            
            DataProcessed?.Invoke(this, new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed, timeRemaining, timeTotal));
            sendDataProcessed = false;
        }
    }
}