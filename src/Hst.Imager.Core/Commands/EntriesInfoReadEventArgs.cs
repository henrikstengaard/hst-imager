namespace Hst.Imager.Core.Commands;

using System;

public class EntriesInfoReadEventArgs : EventArgs
{
    public readonly EntriesInfo EntriesInfo;

    public EntriesInfoReadEventArgs(EntriesInfo entries)
    {
        this.EntriesInfo = entries;
    }
}