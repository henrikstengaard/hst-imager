namespace Hst.Imager.Core.Commands;

using Hst.Core;

public class InvalidCompareSizeError : Error
{
    public long CompareSize;
    public long Size;

    public InvalidCompareSizeError(long compareSize, long size, string message) 
        : base(message)
    {
        CompareSize = compareSize;
        Size = size;
    }
}