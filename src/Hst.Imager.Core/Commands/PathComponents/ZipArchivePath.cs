namespace Hst.Imager.Core.Commands.PathComponents;

using System;

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
public class ZipArchivePath : IMediaPath
{
    public char PathSeparator => '/';

    public string[] Split(string path) =>
        path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    public string Join(string[] pathComponents) =>
        string.Join("/", pathComponents);
}