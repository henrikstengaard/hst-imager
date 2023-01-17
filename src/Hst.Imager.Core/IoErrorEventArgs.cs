namespace Hst.Imager.Core;

using System;

public class IoErrorEventArgs : EventArgs
{
    public readonly IoError IoError;

    public IoErrorEventArgs(IoError ioError)
    {
        this.IoError = ioError;
    }
}