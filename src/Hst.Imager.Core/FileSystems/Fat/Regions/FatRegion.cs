namespace Hst.Imager.Core.FileSystems.Fat.Regions;

/// <summary>
/// Fat region represent part of the fat file system.
/// </summary>
/// <param name="Offset">Offset where region starts.</param>
/// <param name="Size">Size of region in bytes.</param>
/// <param name="Type">Type of region.</param>
/// <param name="Id">Id.</param>
public record FatRegion(long Offset, long Size, FatRegionType Type, string Id);