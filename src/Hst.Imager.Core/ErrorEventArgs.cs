using System;

namespace Hst.Imager.Core;

public class ErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}