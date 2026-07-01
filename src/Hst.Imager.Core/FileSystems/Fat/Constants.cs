namespace Hst.Imager.Core.FileSystems.Fat;

public static class Constants
{
    /// <summary>
    /// Fat entry size is 32 bytes.
    /// </summary>
    public const int FatEntrySize = 32;

    /// <summary>
    /// Number of reserved clusters.
    /// </summary>
    public const int ReservedClusters = 2;
    
    public const string CurrentDirectory = ".          ";
    public const string ParentDirectory = "..         ";

    public const int LastLongEntry = 0x40;
}