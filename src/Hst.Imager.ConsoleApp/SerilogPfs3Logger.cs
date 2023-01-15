namespace Hst.Imager.ConsoleApp;

using System;
using Amiga.FileSystems.Pfs3;
using Serilog.Events;

public class SerilogPfs3Logger : IPfs3Logger
{
    public void Debug(string message)
    {
        Serilog.Log.Logger.Write(LogEventLevel.Debug, message);
    }

    public void Information(string message)
    {
        Serilog.Log.Logger.Write(LogEventLevel.Information, message);
    }

    public void Warning(string message)
    {
        Serilog.Log.Logger.Write(LogEventLevel.Warning, message);
    }

    public void Error(string message)
    {
        Serilog.Log.Logger.Write(LogEventLevel.Error, message);
    }

    public void Error(Exception exception, string message)
    {
        Serilog.Log.Logger.Write(LogEventLevel.Error, $"{message}: {exception}");
    }
}