namespace Hst.Imager.Core.Commands.PathComponents;

using System;

/// <summary>
/// backslash is required by diskutils iso9660 to list directories and files in a given iso file.
/// </summary>
public class Iso9660Path : IMediaPath
{
    public char PathSeparator => '\\';

    public string[] Split(string path) =>
        path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

    public string Join(string[] pathComponents) =>
        string.Join("\\", pathComponents);
}