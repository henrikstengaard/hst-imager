namespace Hst.Imager.Core.Commands;

/// <summary>
/// TODO: Rename to ResolvedMedia
/// </summary>
public class MediaResult
{
    public bool Exists { get; set; }
    
    /// <summary>
    /// Full path
    /// </summary>
    public string FullPath { get; set; }
    
    /// <summary>
    /// part of path pointing to a media file (img, hdf, adf, iso, lha, zip)
    /// </summary>
    public string MediaPath { get; set; }
    
    /// <summary>
    /// Virtual path in media (rename from file system path to virtual path)
    /// </summary>
    public string FileSystemPath { get; set; }
    
    public string DirectorySeparatorChar { get; set; }
    
    public ModifierEnum Modifiers { get; set; }
    public bool ByteSwap { get; set; }
}