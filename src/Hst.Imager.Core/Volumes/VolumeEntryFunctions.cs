using System;
using System.Collections.Generic;

namespace Hst.Imager.Core.Volumes;

public static class VolumeEntryFunctions
{
    /// <summary>
    /// Is relative path components valid examines if an entry with relative path components should be iterated.
    /// </summary>
    /// <param name="relativePathComponents">Relative path components.</param>
    /// <param name="recursive">Recursive.</param>
    /// <returns>True, if relative path components are valid, otherwise false.</returns>
    public static bool IsRelativePathComponentsValid(string[] relativePathComponents, bool recursive) =>
        recursive ? relativePathComponents.Length > 0 : relativePathComponents.Length == 1;

    /// <summary>
    /// Get relative path components of full path components relative to root path components.
    /// If full path components are equal to root path components, the last path component is returned.
    /// </summary>
    /// <param name="rootPathComponents">Root path components.</param>
    /// <param name="fullPathComponents">Full path components.</param>
    /// <param name="caseSensitive">Compare path components case-sensitive.</param>
    /// <returns>Relative path components.</returns>
    public static IEnumerable<string> GetRelativePathComponents(string[] rootPathComponents,
        string[] fullPathComponents, bool caseSensitive = false)
    {
        // skip, if full path components are less than root path components
        // for example full path is "dir1" and root path components "dir1/dir2"
        if (fullPathComponents.Length < rootPathComponents.Length)
        {
            yield break;
        }

        var stringComparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var equalPathComponentCount = 0;
        for (var pathComponentIndex = 0; pathComponentIndex < fullPathComponents.Length; pathComponentIndex++)
        {
            if (pathComponentIndex < rootPathComponents.Length)
            {
                if (!rootPathComponents[pathComponentIndex].Equals(fullPathComponents[pathComponentIndex],
                        stringComparison))
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
}
