using System;

namespace Hst.Imager.Core;

public interface IStreamActivity
{
    public enum ActivityType
    {
        Read,
        Write,
        Seek,
        Flush
    }

    DateTime Date { get; }
    ActivityType Type { get; }
}