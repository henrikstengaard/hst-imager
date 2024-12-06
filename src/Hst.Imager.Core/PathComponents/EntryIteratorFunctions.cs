using System.Collections.Generic;
using System.Linq;
using System;

namespace Hst.Imager.Core.PathComponents;

public static class EntryIteratorFunctions
{
    public static bool IsFullPathComponentsValid(string[] rootPathComponents, string[] fullPathComponents)
    {
        if (fullPathComponents.Length < rootPathComponents.Length)
        {
            return false;
        }

        if (rootPathComponents.Length > 0 && !fullPathComponents[0].Equals(rootPathComponents[0]))
        {
            return false;
        }

        return true;
    }

    public static bool IsRelativePathComponentsValid(PathComponentMatcher pathComponentMatcher,
        string[] relativePathComponents, bool recursive)
    {
        if (!recursive && relativePathComponents.Length > 1)
        {
            return false;
        }

        return relativePathComponents.Length > 0 && pathComponentMatcher.IsMatch(relativePathComponents.ToArray());
    }

    public static bool IsRelativePathComponentsValid2(string[] relativePathComponents, bool recursive)
    {
        if (!recursive)
        {
            return relativePathComponents.Length == 1;
        }

        return relativePathComponents.Length > 0;
    }

    public static IEnumerable<string> GetRelativePathComponents(string[] rootPathComponents, string[] fullPathComponents)
    {
        // skip, if full path componenets are less than root path components
        // for example full path is "dir1" and root path components "dir1/dir2"
        if (fullPathComponents.Length < rootPathComponents.Length)
        {
            yield break;
        }

        var equalPathComponentCount = 0;
        for (var pathComponentIndex = 0; pathComponentIndex < fullPathComponents.Length; pathComponentIndex++)
        {
            if (pathComponentIndex < rootPathComponents.Length)
            {
                var pathComponentsEqual = rootPathComponents[pathComponentIndex].Length == fullPathComponents[pathComponentIndex].Length &&
                    rootPathComponents[pathComponentIndex].Equals(fullPathComponents[pathComponentIndex], StringComparison.OrdinalIgnoreCase);

                if (!pathComponentsEqual)
                {
                    break;
                }

                equalPathComponentCount++;
                continue;
            }

            yield return fullPathComponents[pathComponentIndex];
        }

        if (equalPathComponentCount == fullPathComponents.Length)
        {
            yield return fullPathComponents[^1];
        }
    }

    /// <summary>
    /// Create dir entries from relative path components.
    /// will skip if only 1 relative path component (entry only, no dir path components)
    /// </summary>
    /// <param name="mediaPath"></param>
    /// <param name="fullPathComponents"></param>
    /// <param name="relativePathComponents"></param>
    /// <param name="isDir"></param>
    /// <param name="attributes"></param>
    /// <param name="recursive"></param>
    /// <returns></returns>
    public static IEnumerable<Models.FileSystems.Entry> GetDirEntries2(IMediaPath mediaPath,
    string[] fullPathComponents, string[] relativePathComponents, bool isDir, string attributes, bool recursive)
    {
        if (relativePathComponents.Length <= 1)
        {
            yield break;
        }

        var dirPathComponents = new List<string>();

        var maxPathComponentIndex = recursive ? relativePathComponents.Length - 1 : 1;
        for (var pathComponentIndex = 0; pathComponentIndex < maxPathComponentIndex; pathComponentIndex++)
        {
            dirPathComponents.Add(relativePathComponents[pathComponentIndex]);

            var dirPath = mediaPath.Join(dirPathComponents.ToArray());

            var dirEntry = new Models.FileSystems.Entry
            {
                Name = dirPath,
                FormattedName = dirPath,
                RawPath = dirPath,
                FullPathComponents = fullPathComponents.Concat(dirPathComponents).ToArray(),
                RelativePathComponents = dirPathComponents.ToArray(),
                Date = DateTime.Now,
                Size = 0,
                Type = Models.FileSystems.EntryType.Dir,
                Attributes = attributes,
                Properties = new Dictionary<string, string>()
            };

            yield return dirEntry;
        }
    }

    public static IEnumerable<Models.FileSystems.Entry> CreateEntries(
        IMediaPath mediaPath,
        PathComponentMatcher pathComponentMatcher,
        string[] rootPathComponents,
        bool recursive,
        string entryPath,
        string rawPath,
        bool isDir,
        DateTime date,
        long size,
        string attributes,
        IDictionary<string, string> properties,
        string dirAttributes)
    {
        var fullPathComponents = mediaPath.Split(entryPath);

        if (!IsFullPathComponentsValid(rootPathComponents, fullPathComponents))
        {
            yield break;
        }

        var relativePathComponents = GetRelativePathComponents(
            rootPathComponents, fullPathComponents).ToArray();

        var dirEntries = GetDirEntries2(mediaPath, rootPathComponents, relativePathComponents, isDir, dirAttributes, recursive)
            .ToList();

        foreach (var dirEntry in dirEntries)
        {
            if (!recursive && !pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
            {
                continue;
            }

            yield return dirEntry;
        }

        if (!IsRelativePathComponentsValid2(relativePathComponents, recursive) ||
            (!isDir && !pathComponentMatcher.IsMatch(fullPathComponents)))
        {
            yield break;
        }

        var entryRelativePath = mediaPath.Join(relativePathComponents.ToArray());

        yield return new Models.FileSystems.Entry
        {
            Name = entryRelativePath,
            FormattedName = entryRelativePath,
            RawPath = rawPath,
            FullPathComponents = fullPathComponents.ToArray(),
            RelativePathComponents = relativePathComponents.ToArray(),
            Date = date,
            Size = size,
            Type = isDir
                ? Models.FileSystems.EntryType.Dir
                : Models.FileSystems.EntryType.File,
            Attributes = attributes,
            Properties = properties
        };
    }

    public static Models.FileSystems.Entry CreateEntry(
        IMediaPath mediaPath,
        string[] rootPathComponents,
        bool recursive,
        string entryPath,
        string rawPath,
        bool isDir,
        DateTime date,
        long size,
        string attributes,
        IDictionary<string, string> properties,
        string dirAttributes)
    {
        var fullPathComponents = mediaPath.Split(entryPath);

        var relativePathComponents = GetRelativePathComponents(
            rootPathComponents, fullPathComponents).ToArray();

        var entryRelativePath = mediaPath.Join(relativePathComponents.ToArray());

        return new Models.FileSystems.Entry
        {
            Name = entryRelativePath,
            FormattedName = entryRelativePath,
            RawPath = rawPath,
            FullPathComponents = fullPathComponents.ToArray(),
            RelativePathComponents = relativePathComponents.ToArray(),
            Date = date,
            Size = size,
            Type = isDir
                ? Models.FileSystems.EntryType.Dir
                : Models.FileSystems.EntryType.File,
            Attributes = attributes,
            Properties = properties
        };
    }
}