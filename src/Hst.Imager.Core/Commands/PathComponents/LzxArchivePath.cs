namespace Hst.Imager.Core.Commands.PathComponents;

using System;

/// <summary>
/// Lzx archive path for splitting and joining paths.
/// Lzx archive uses forward slash as directory path separator.
/// </summary>
public class LzxArchivePath : IMediaPath
{
    public char PathSeparator => '/';

    public string[] Split(string path) =>
        path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    public string Join(string[] pathComponents) =>
        string.Join("/", pathComponents);
}