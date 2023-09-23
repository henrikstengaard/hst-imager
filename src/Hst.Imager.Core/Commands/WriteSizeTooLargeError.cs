using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class WriteSizeTooLargeError : Error
{
    public long MediaSize;
    public long WriteSize;

    public WriteSizeTooLargeError(long mediaSize, long writeSize, string message) 
        : base(message)
    {
        MediaSize = mediaSize;
        WriteSize = writeSize;
    }
}