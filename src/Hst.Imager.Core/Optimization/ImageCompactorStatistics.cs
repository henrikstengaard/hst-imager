namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Statistics from compacting an image.
/// </summary>
/// <param name="Size">Size of image in bytes.</param>
/// <param name="BytesWritten">Bytes written to destination (used and not zero filled).</param>
/// <param name="BytesFree">Bytes skipped as free by file system.</param>
/// <param name="BytesZeroFilled">Bytes skipped as zero filled.</param>
public record ImageCompactorStatistics(long Size, long BytesWritten, long BytesFree, long BytesZeroFilled);
