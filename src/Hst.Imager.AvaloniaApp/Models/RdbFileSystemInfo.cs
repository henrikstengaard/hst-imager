namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// File system found in media like lha, adf or iso, which can be imported to a rigid disk block.
/// </summary>
/// <param name="Name">Name of file system found in media.</param>
/// <param name="Size">Size of file system in bytes.</param>
/// <param name="Version">Version of file system, e.g. 19.2. Empty, if file system doesn't have a version string.</param>
public record RdbFileSystemInfo(string Name, long Size, string Version);
