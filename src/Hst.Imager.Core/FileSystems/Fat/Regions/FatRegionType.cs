namespace Hst.Imager.Core.FileSystems.Fat.Regions;

public enum FatRegionType
{
    /// <summary>
    /// Reserved region.
    /// </summary>
    Reserved,

    /// <summary>
    /// File allocation table region containing cluster chains for directories and files.
    /// </summary>
    Fat,

    /// <summary>
    /// Root region containing root directory.
    /// </summary>
    Root,

    /// <summary>
    /// Directory region containing entries for directories and files.
    /// </summary>
    Directory,

    /// <summary>
    /// File region containing file data.
    /// </summary>
    File
}