using System;
using Hst.Core.IO;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.Commands;

public class CommandLogger
{
    private static readonly Lazy<CommandLogger> LazyInstance = new(() => new CommandLogger());
    
    public static CommandLogger Instance => LazyInstance.Value;

    public event EventHandler<string> DebugMessage;
    public event EventHandler<string> WarningMessage;
    public event EventHandler<string> InformationMessage;
    public event EventHandler<DataProcessedEventArgs> DataProcessed;
    
    private CommandLogger()
    {
    }
    
    public void OnDebugMessage(string message)
    {
        DebugMessage?.Invoke(this, message);
    }        

    public void OnWarningMessage(string message)
    {
        WarningMessage?.Invoke(this, message);
    }

    public void OnInformationMessage(string message)
    {
        InformationMessage?.Invoke(this, message);
    }        

    public void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed,
        long bytesRemaining, long bytesTotal,
        TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
    {
        DataProcessed?.Invoke(this,
            new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal,
                timeElapsed,
                timeRemaining, timeTotal, bytesPerSecond));
    }

    public void AddLoggingOf(LayeredStream layeredStream)
    {
        layeredStream.FlushStarted += FlushStarted;
        layeredStream.DataFlushed += DataFlushed;
        layeredStream.FlushEnded += FlushEnded;
        return;
        
        void FlushStarted(object sender, EventArgs args)
        {
            OnInformationMessage($"Flushing cache");
        }
        
        void DataFlushed(object sender, LayeredStream.DataFlushedEventArgs args)
        {
            OnDataProcessed(false, args.PercentComplete, args.BytesProcessed, args.BytesRemaining,
                args.BytesTotal, args.TimeElapsed, args.TimeRemaining, args.TimeTotal, args.BytesPerSecond);
        }
        
        void FlushEnded(object sender, LayeredStream.DataFlushedEventArgs args)
        {
            OnInformationMessage($"Flushed '{args.BytesProcessed.FormatBytes()}' ({args.BytesProcessed} bytes) in {args.TimeElapsed.FormatElapsed()}");
        }
    }
}