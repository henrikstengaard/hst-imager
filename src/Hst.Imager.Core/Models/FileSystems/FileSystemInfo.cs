namespace Hst.Imager.Core.Models.FileSystems;

public class FileSystemInfo
{
    public string FileSystemType { get; set; }
    public string VolumeName { get; set; }
    
    /// <summary>
    /// Size of volume in bytes
    /// </summary>
    public long VolumeSize { get; set; }
    
    /// <summary>
    /// Free volume disk space in bytes
    /// </summary>
    public long VolumeFree { get; set; }
    
    /// <summary>
    /// size of cluster
    /// </summary>
    public long ClusterSize { get; set; }
}