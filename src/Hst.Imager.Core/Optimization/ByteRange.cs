namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Range of bytes defined by an offset and a length.
/// </summary>
/// <param name="Offset">Start offset in bytes.</param>
/// <param name="Length">Length in bytes.</param>
public readonly record struct ByteRange(long Offset, long Length)
{
    /// <summary>
    /// Exclusive end offset in bytes.
    /// </summary>
    public long End => Offset + Length;
}
