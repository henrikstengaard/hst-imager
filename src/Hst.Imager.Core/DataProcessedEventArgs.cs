﻿namespace Hst.Imager.Core
{
    using System;

    public class DataProcessedEventArgs : EventArgs
    {
        public readonly double PercentComplete;
        public readonly long BytesProcessed;
        public readonly long BytesRemaining;
        public readonly long BytesTotal;
        public readonly TimeSpan TimeElapsed;
        public readonly TimeSpan TimeRemaining;
        public readonly TimeSpan TimeTotal;
        public readonly long BytesPerSecond;

        public DataProcessedEventArgs(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            PercentComplete = percentComplete;
            BytesProcessed = bytesProcessed;
            BytesRemaining = bytesRemaining;
            BytesTotal = bytesTotal;
            TimeElapsed = timeElapsed;
            TimeRemaining = timeRemaining;
            TimeTotal = timeTotal;
            BytesPerSecond = bytesPerSecond;
        }
    }
}