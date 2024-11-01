using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.PathComponents;

using System;
using System.Linq;

public class PathComponentMatcher
{
    public readonly string[] PathComponents;
    private readonly bool recursive;
    private readonly PatternMatcher patternMatcher;

    public PathComponentMatcher(string[] pathComponents, bool recursive = false)
    {
        var hasPathComponents = pathComponents.Any();
        var lastPathComponent = hasPathComponents ? pathComponents[^1].Trim() : string.Empty;

        // uses pattern is only set if the last path component exists, is more than 1 character long and contains a wildcard
        var hasWildcard = lastPathComponent.Contains("*", StringComparison.OrdinalIgnoreCase);

        UsesPattern = lastPathComponent.Length > 1 && hasWildcard;
        PathComponents = hasWildcard
            ? pathComponents.Take(pathComponents.Length - 1).ToArray()
            : pathComponents;
        this.recursive = recursive;

        patternMatcher = UsesPattern ? new PatternMatcher(lastPathComponent) : null;
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