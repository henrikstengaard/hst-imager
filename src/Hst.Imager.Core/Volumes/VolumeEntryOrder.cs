namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Order entries in a directory are iterated in.
/// </summary>
public enum VolumeEntryOrder
{
    /// <summary>
    /// Entries ordered by relative path, directories and files are mixed.
    /// </summary>
    RelativePath,

    /// <summary>
    /// Directories ordered by name before files ordered by name.
    /// </summary>
    DirectoriesFirst
}
