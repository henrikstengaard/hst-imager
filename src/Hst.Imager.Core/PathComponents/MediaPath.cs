namespace Hst.Imager.Core.PathComponents;

public static class MediaPath
{
    public static IMediaPath ForwardSlashMediaPath => new ForwardSlashMediaPath();
    public static IMediaPath BackslashMediaPath => new BackslashMediaPath();

    public static IMediaPath WindowsOsPath => new BackslashMediaPath();
    public static IMediaPath MacOsPath => new ForwardSlashMediaPath();
    public static IMediaPath LinuxOsPath => new ForwardSlashMediaPath();
    public static IMediaPath AmigaOsPath => new ForwardSlashMediaPath();

    /// <summary>
    /// Lha archive path for splitting and joining paths.
    /// Lha archive uses backslash as directory path separator.
    /// </summary>
    public static IMediaPath LhaArchivePath => BackslashMediaPath;

    /// <summary>
    /// Lzx archive path for splitting and joining paths.
    /// Lzx archive uses forward slash as directory path separator.
    /// </summary>
    public static IMediaPath LzxArchivePath => ForwardSlashMediaPath;

    /// <summary>
    /// Zip archive path for splitting and joining paths.
    /// Zip archive uses forward slash as directory path separator.
    ///
    /// From APPNOTE.TXT - .ZIP File Format Specification
    /// -------------------------------------------------
    /// 4.4.17.1 The name of the file, with optional relative path.
    /// The path stored MUST NOT contain a drive or
    /// device letter, or a leading slash.All slashes
    /// MUST be forward slashes '/' as opposed to
    /// backwards slashes '\' for compatibility with Amiga
    /// and UNIX file systems etc.
    /// </summary>
    public static IMediaPath ZipArchivePath => ForwardSlashMediaPath;

    /// <summary>
    /// Iso9660 path for splitting and joining paths.
    /// Iso9660 uses backslash as directory path separator.
    /// </summary>
    public static IMediaPath Iso9660Path => BackslashMediaPath;
}