namespace Hst.Imager.Core.Medias
{
    using Commands;
    using Hst.Core;

    /// <summary>
    /// Resolves paths to media path, file system path and modifiers.
    /// </summary>
    public interface IMediaPathResolver
    {
        /// <summary>
        /// Resolves a path into media path and file system path, e.g. disk.img\rdb\dh0 resolves to
        /// media path disk.img and file system path rdb\dh0.
        /// </summary>
        Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false);

        /// <summary>
        /// Resolves modifiers in the start of the path, e.g. +bs:/path/to/file.
        /// </summary>
        ModifierResult ResolveModifiers(string path);

        /// <summary>
        /// Gets physical drive path for path, if path is a physical drive. Otherwise null is returned.
        /// </summary>
        string GetPhysicalDrivePath(string path);
    }
}
