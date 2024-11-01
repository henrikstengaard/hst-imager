namespace Hst.Imager.Core.Commands;

using System;
using System.Linq;

public class PathComponentMatcher
{
    private readonly string[] rootPathComponents;
    private readonly string pattern;
    private readonly bool recursive;
    private readonly PatternMatcher patternMatcher;

    public PathComponentMatcher(string[] rootPathComponents, string pattern = null, bool recursive = false)
    {
        this.rootPathComponents = rootPathComponents;
        this.pattern = pattern;
        this.recursive = recursive;
        this.patternMatcher = pattern is null or "*" ? null : new PatternMatcher(pattern);
    }

    public bool UsesPattern => !string.IsNullOrEmpty(this.pattern);

    public bool IsMatch(string[] pathComponents)
    {
        // return false, if path components is less than root path components
        if (pathComponents.Length < this.rootPathComponents.Length)
        {
            return false;
        }

        // return false, when path component doesn't match root path components
        int i;
        for (i = 0; i < this.rootPathComponents.Length; i++)
        {
            if (this.rootPathComponents[i].Length != pathComponents[i].Length ||
                this.rootPathComponents[i].IndexOf(pathComponents[i], StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        // return true, if all path components matches root path components and no filename matcher is used
        if (this.patternMatcher == null && pathComponents.Length == this.rootPathComponents.Length)
        {
            return true;
        }

        // return true, if pattern matcher is not used or if result of pattern matcher matches last path component
        // for recursive or next path component after root path components
        return this.patternMatcher?.IsMatch(pathComponents[recursive ? pathComponents.Length - 1 : i]) ?? true;
    }
}


public class PathComponentMatcherV3
{
    public readonly string[] PathComponents;
    private readonly bool recursive;
    private readonly PatternMatcher patternMatcher;

    public PathComponentMatcherV3(string[] pathComponents, bool recursive = false)
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

        this.patternMatcher = UsesPattern ? new PatternMatcher(lastPathComponent) : null;
    }

    public readonly bool UsesPattern;

    public bool IsMatch(string[] pathComponents)
    {
        if (pathComponents.Length < PathComponents.Length)
        {
            return false;
        }

        int pathComponentIndex;
        for(pathComponentIndex = 0; pathComponentIndex < Math.Min(PathComponents.Length, pathComponents.Length); pathComponentIndex++)
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
            ? this.patternMatcher.IsMatch(recursive ? pathComponents[^1] : pathComponents[pathComponentIndex])
            : true;
    }
}