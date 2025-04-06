using Hst.Core;

namespace Hst.Imager.Core.Commands;

public class CompareSizeTooLargeError(long srcPartSize, long destPartSize, string message)
    : Error(message)
{
    public long SrcPartSize = srcPartSize;
    public long DestPartSize = destPartSize;
}