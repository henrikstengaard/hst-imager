using Hst.Core;

namespace Hst.Imager.Core.Commands;

/// <summary>
/// Source with unknown size has more data than max size, e.g. a gzip compressed image larger than destination.
/// </summary>
public class SourceSizeTooLargeError : Error
{
    public long MaxSize;

    public SourceSizeTooLargeError(long maxSize, string message)
        : base(message)
    {
        MaxSize = maxSize;
    }
}
