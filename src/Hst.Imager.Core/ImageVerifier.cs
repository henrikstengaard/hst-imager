namespace Hst.Imager.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Commands;
    using Helpers;
    using Hst.Core;

    public class ImageVerifier : IDisposable
    {
        private readonly int retries;
        private readonly bool force;
        private readonly int bufferSize;
        private readonly System.Timers.Timer timer;
        private DataProcessedEventArgs dataProcessedEventArgs;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public ImageVerifier(int bufferSize = 1024 * 1024, int retries = 0, bool force = false)
        {
            this.bufferSize = bufferSize;
            this.retries = retries;
            this.force = force;
            timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Elapsed += SendDataProcessed;
            dataProcessedEventArgs = null;
        }

        private void SendDataProcessed(object sender, EventArgs args)
        {
            if (dataProcessedEventArgs == null)
            {
                return;
            }

            DataProcessed?.Invoke(this, dataProcessedEventArgs);
            dataProcessedEventArgs = null;
        }
        
        public async Task<Result> Verify(CancellationToken token, Stream source, long sourceOffset, Stream destination,
            long destinationOffset, long size, bool skipZeroFilled = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (source.CanSeek)
            {
                source.Seek(sourceOffset, SeekOrigin.Begin);
            }

            if (destination.CanSeek)
            {
                destination.Seek(destinationOffset, SeekOrigin.Begin);
            }

            OnDataProcessed(size == 0, 0, 0, size, size, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
            
            timer.Start();
            
            var srcBuffer = new byte[bufferSize];
            var destBuffer = new byte[bufferSize];

            long bytesProcessed = 0;
            var srcBytesRead = 0;
            bool endOfStream;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return new Result<Error>(new Error("Cancelled"));
                }

                var verifyBytes = size == 0
                    ? bufferSize
                    : Convert.ToInt32(bytesProcessed + bufferSize > size ? size - bytesProcessed : bufferSize);
                
                bool srcReadFailed;
                var srcRetry = 0;
                do
                {
                    try
                    {
                        if (source.CanSeek)
                        {
                            source.Seek(sourceOffset, SeekOrigin.Begin);
                        }

                        srcBytesRead = await source.ReadAsync(srcBuffer, 0, verifyBytes, token);
                        srcReadFailed = false;
                    }
                    catch (Exception e)
                    {
                        srcReadFailed = true;
                        if (!force && srcRetry >= retries)
                        {
                            throw;
                        }
                        
                        OnSrcError(bytesProcessed, verifyBytes, e.ToString());
                    }

                    srcRetry++;
                } while (srcReadFailed && srcRetry <= retries);

                bool destReadFailed;
                var destBytesRead = 0;
                var destRetry = retries;
                do
                {
                    try
                    {
                        if (destination.CanSeek)
                        {
                            destination.Seek(destinationOffset, SeekOrigin.Begin);
                        }

                        destBytesRead = await destination.ReadAsync(destBuffer, 0, verifyBytes, token);
                        destReadFailed = false;
                    }
                    catch (Exception e)
                    {
                        destReadFailed = true;
                        if (!force && destRetry >= retries)
                        {
                            throw;
                        }

                        OnDestError(bytesProcessed, verifyBytes, e.ToString());
                    }

                    destRetry++;
                } while (destReadFailed && destRetry <= retries);
                
                if (size < bufferSize)
                {
                    verifyBytes = (int)size;
                }
                
                if (srcBytesRead != destBytesRead)
                {
                    var minBytesRead = Math.Min(srcBytesRead, destBytesRead);
                    verifyBytes = Math.Min(minBytesRead, verifyBytes);
                }

                if (verifyBytes < bufferSize && bytesProcessed + verifyBytes != size)
                {
                    return new Result(new SizeNotEqualError(bytesProcessed, bytesProcessed + verifyBytes, size));
                }

                // compare, if not skipping zero filled or if src buffer is not zero filled.
                // skip zero filled doesn't apply to dest buffer.
                if (!skipZeroFilled ||
                    !DataSectorReader.IsZeroFilled(srcBuffer, 0, verifyBytes))
                {
                    for (var i = 0; i < verifyBytes; i++)
                    {
                        if (srcBuffer[i] == destBuffer[i])
                        {
                            continue;
                        }

                        return new Result(new ByteNotEqualError(bytesProcessed + i, srcBuffer[i], destBuffer[i]));
                    }
                }

                bytesProcessed += verifyBytes;
                var bytesRemaining = size - bytesProcessed;
                var percentComplete = bytesProcessed == 0 ? 0 : Math.Round((double)100 / size * bytesProcessed, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = TimeHelper.CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = timeElapsed + timeRemaining;
                var bytesPerSecond = Convert.ToInt64(bytesProcessed / timeElapsed.TotalSeconds);

                sourceOffset += srcBytesRead;
                destinationOffset += srcBytesRead;
                
                var indeterminate = size == 0;
                OnDataProcessed(indeterminate, percentComplete, bytesProcessed, bytesRemaining, size,
                    timeElapsed, timeRemaining, timeTotal, bytesPerSecond);
                endOfStream = srcBytesRead == 0 || bytesProcessed >= size;
            } while (!endOfStream);

            timer.Stop();
            stopwatch.Stop();
            
            OnDataProcessed(size == 0, 100, bytesProcessed, 0, bytesProcessed, stopwatch.Elapsed, TimeSpan.Zero, stopwatch.Elapsed, 0);
            
            return new Result();
        }

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            dataProcessedEventArgs = new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed,
                bytesRemaining, bytesTotal, timeElapsed, timeRemaining, timeTotal, bytesPerSecond);
                        
            if (percentComplete >= 100)
            {
                SendDataProcessed(this, EventArgs.Empty);
            }
        }
        
        private void OnSrcError(long offset, int count, string errorMessage)
        {
            SrcError?.Invoke(this, new IoErrorEventArgs(new IoError(offset, count, errorMessage)));
        }
        
        private void OnDestError(long offset, int count, string errorMessage)
        {
            DestError?.Invoke(this, new IoErrorEventArgs(new IoError(offset, count, errorMessage)));
        }

        public void Dispose()
        {
            timer.Elapsed -= SendDataProcessed;
            timer.Stop();
            SendDataProcessed(this, EventArgs.Empty);
            timer?.Dispose();
        }
    }
}