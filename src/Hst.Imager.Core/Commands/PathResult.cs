namespace Hst.Imager.Core.Commands;

public class MediaResult
{
    /// <summary>
    /// Full path
    /// </summary>
    public string FullPath { get; set; }
    
    /// <summary>
    /// part of path pointing to a media file (img, hdf, adf, iso, lha, zip)
    /// </summary>
    public string MediaPath { get; set; }
    
    /// <summary>
    /// File system path in media
    /// </summary>
    public string FileSystemPath { get; set; }
    
    public string DirectorySeparatorChar { get; set; }
    
    public bool ByteSwap { get; set; }
}