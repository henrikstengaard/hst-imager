namespace Hst.Imager.Core.Commands;

using System;

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
        
        // return false, when part component doesn't match root path components
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