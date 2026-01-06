using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.PathComponents;

using System;
using System.Linq;

public class PathComponentMatcher
{
    public readonly string[] PathComponents;
    private readonly bool isFile;
    private readonly bool recursive;
    private readonly PatternMatcher patternMatcher;

    /*
     * Path component matcher rules:
     * - when last path component contains a wildcard *, it uses pattern. it will therefore not be a single file as it can match multiple.
     * - when last path component is an existing file (isFile = true), it can't be using pattern.
     */
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="pathComponents">Path components to match against.</param>
    /// <param name="isFile">Path components last part is an existing file.</param>
    /// <param name="recursive">Match path components recursively.</param>
    public PathComponentMatcher(string[] pathComponents, bool isFile = false, bool recursive = false)
    {
        var hasPathComponents = pathComponents.Any();
        var lastPathComponent = hasPathComponents ? pathComponents[^1].Trim() : string.Empty;

        // uses pattern is only set if the last path component exists, is more than 1 character long and contains a wildcard
        var hasWildcard = PathComponentHelper.HasWildcard(lastPathComponent);

        UsesPattern = lastPathComponent.Length > 1 && hasWildcard;
        PathComponents = isFile || hasWildcard
            ? pathComponents.Take(pathComponents.Length - 1).ToArray()
            : pathComponents;
        this.isFile = isFile;
        this.recursive = recursive;

        patternMatcher = UsesPattern || isFile ? new PatternMatcher(lastPathComponent) : null;
    }

    public readonly bool UsesPattern;

    public bool IsMatch(string[] pathComponents)
    {
        if (pathComponents.Length < PathComponents.Length)
        {
            return false;
        }

        int pathComponentIndex;
        for (pathComponentIndex = 0; pathComponentIndex < Math.Min(PathComponents.Length, pathComponents.Length); pathComponentIndex++)
        {
            if (!PathComponents[pathComponentIndex].Equals(pathComponents[pathComponentIndex], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // return true, if last path component when it's a file
        if (isFile)
        {
            return patternMatcher.IsMatch(pathComponents[^1]);
        }


        // uses pattern, but no more path components to examine
        if (UsesPattern && pathComponentIndex >= pathComponents.Length)
        {
            return false;
        }

        return UsesPattern && pathComponents.Length > 0
            ? patternMatcher.IsMatch(recursive ? pathComponents[^1] : pathComponents[pathComponentIndex])
            : true;
    }
}