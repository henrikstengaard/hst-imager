namespace Hst.Imager.Core.Commands.PathComponents;

using System;

public class AmigaPath : IMediaPath
{
    public char PathSeparator => '/';

    public string[] Split(string path) =>
        path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    public string Join(string[] pathComponents) =>
        string.Join("/", pathComponents);
}