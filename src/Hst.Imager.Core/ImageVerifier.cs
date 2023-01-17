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

    public class ImageVerifier
    {
        private readonly int retries;
        private readonly int bufferSize;
        private readonly System.Timers.Timer timer;
        private bool sendDataProcessed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public ImageVerifier(int bufferSize = 1024 * 1024, int retries = 0)
        {
            this.bufferSize = bufferSize;
            this.retries = retries;
            timer = new System.Timers.Timer();
            timer.Enabled = true;
            timer.Interval = 1000;
            timer.Elapsed += (_, _) => sendDataProcessed = true;
            sendDataProcessed = false;
        }

        public async Task<Result> Verify(CancellationToken token, Stream source, Stream destination, long size)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            source.Seek(0, SeekOrigin.Begin);
            destination.Seek(0, SeekOrigin.Begin);

            sendDataProcessed = true;
            OnDataProcessed(0, 0, size, size, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
            
            timer.Start();
            
            var srcBuffer = new byte[bufferSize];
            var destBuffer = new byte[bufferSize];

            long offset = 0;
            var srcBytesRead = 0;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return new Result<Error>(new Error("Cancelled"));
                }

                var verifyBytes = Convert.ToInt32(offset + bufferSize > size ? size - offset : bufferSize);
                
                var srcRetry = 0;
                do
                {
                    try
                    {
                        srcBytesRead = await source.ReadAsync(srcBuffer, 0, verifyBytes, token);
                    }
                    catch (Exception e)
                    {
                        if (srcRetry >= retries)
                        {
                            throw;
                        }
                        
                        OnSrcError(offset, verifyBytes, e.ToString());
                        source.Seek(offset, SeekOrigin.Begin);
                    }

                    srcRetry++;
                } while (srcRetry <= retries);

                var destBytesRead = 0;
                var destRetry = 0;
                do
                {
                    try
                    {
                        destBytesRead = await destination.ReadAsync(destBuffer, 0, verifyBytes, token);
                    }
                    catch (Exception e)
                    {
                        if (destRetry >= retries)
                        {
                            throw;
                        }

                        OnDestError(offset, verifyBytes, e.ToString());
                        destination.Seek(offset, SeekOrigin.Begin);
                    }

                    destRetry++;
                } while (destRetry <= retries);
                
                if (size < bufferSize)
                {
                    verifyBytes = (int)size;
                }

                if (srcBytesRead != destBytesRead)
                {
                    verifyBytes = Math.Min(srcBytesRead, destBytesRead);
                }

                if (verifyBytes < bufferSize && offset + verifyBytes != size)
                {
                    return new Result(new SizeNotEqualError(offset, size));
                }

                for (int i = 0; i < verifyBytes; i++)
                {
                    if (srcBuffer[i] == destBuffer[i])
                    {
                        continue;
                    }

                    return new Result(new ByteNotEqualError(offset + i, srcBuffer[i], destBuffer[i]));
                }

                offset += verifyBytes;
                var bytesRemaining = size - offset;
                var percentComplete = offset == 0 ? 0 : Math.Round((double)100 / size * offset, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = TimeHelper.CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = timeElapsed + timeRemaining;
                var bytesPerSecond = Convert.ToInt64(offset / timeElapsed.TotalSeconds);

                OnDataProcessed(percentComplete, offset, bytesRemaining, size, timeElapsed, timeRemaining, timeTotal,
                    bytesPerSecond);
            } while (srcBytesRead == bufferSize && offset < size);

            timer.Stop();
            stopwatch.Stop();
            
            sendDataProcessed = true;
            OnDataProcessed(100, offset, 0, offset, stopwatch.Elapsed, TimeSpan.Zero, stopwatch.Elapsed, 0);
            
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
        
        private void OnSrcError(long offset, int count, string errorMessage)
        {
            SrcError?.Invoke(this, new IoErrorEventArgs(new IoError(offset, count, errorMessage)));
        }
        
        private void OnDestError(long offset, int count, string errorMessage)
        {
            DestError?.Invoke(this, new IoErrorEventArgs(new IoError(offset, count, errorMessage)));
        }
    }
}