namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// Result of optimizing an image file.
/// </summary>
/// <param name="OriginalFileSize">Size of image file before optimizing. Size on disk, when compacted.</param>
/// <param name="OptimizedFileSize">Size of image file after optimizing. Size on disk, when compacted.</param>
/// <param name="IsCompacted">Image file is compacted.</param>
/// <param name="PercentReduced">Percent file size is reduced by. Null, if file size is not reduced.</param>
public record OptimizeResult(long OriginalFileSize, long OptimizedFileSize, bool IsCompacted, double? PercentReduced);
