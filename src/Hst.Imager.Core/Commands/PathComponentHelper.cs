using System;
using System.Linq;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.Commands;

public static class PathComponentHelper
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="srcEntryType"></param>
    /// <param name="srcPathComponents">Source path components to entry to write.</param>
    /// <param name="destPathComponents">Root path components to where the writer is initialized.</param>
    /// <param name="destEntryType"></param>
    /// <param name="doesLastPathComponentExist">Does the last part of root path component exist.</param>
    /// <param name="isSingleEntryOperation">Is it a single file operation.</param>
    /// <returns></returns>
    public static string[] GetFullPathComponents(EntryType srcEntryType, string[] srcPathComponents,
        EntryType destEntryType, string[] destPathComponents, bool doesLastPathComponentExist,
        bool isSingleEntryOperation)
    {
        var dirPathComponents = doesLastPathComponentExist ? destPathComponents : destPathComponents.Take(destPathComponents.Length - 1).ToArray();
        
        // return dir path components if the entry type is a directory and it is a single entry operation.
        // this allows the directories for the single file to be created for the last path root component that doesn't exist.
        // it basically ignoring the entry path components.
        if (srcEntryType == EntryType.Dir && isSingleEntryOperation)
        {
            return dirPathComponents;
        }

        if (isSingleEntryOperation &&
            doesLastPathComponentExist &&
            srcEntryType == EntryType.File &&
            destEntryType == EntryType.File)
        {
            return destPathComponents;
        }
        
        var isSingleFileCopyAndRename = isSingleEntryOperation &&
                                        !doesLastPathComponentExist;

        var fullPathComponents = isSingleFileCopyAndRename
            ? destPathComponents
            : destPathComponents.Concat(srcPathComponents).ToArray();

        var isNameChanged = isSingleFileCopyAndRename &&
                            srcPathComponents[^1] != destPathComponents[^1];

        if (isNameChanged)
        {
            fullPathComponents[^1] = destPathComponents[^1];
        }

        return fullPathComponents;
    }
    
    /// <summary>
    /// Has wildcard examines if path component contains a * wildcard.
    /// </summary>
    /// <param name="pathComponent"></param>
    /// <returns>True, if path component contains a * wildcard. Otherwise, false.</returns>
    public static bool HasWildcard(string pathComponent) => 
        pathComponent.Length > 0 && pathComponent.Contains('*', StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Matches root path components with entry path components. Last path component supports wildcard.
    /// </summary>
    /// <param name="rootPathComponents">Root path components.</param>
    /// <param name="entryPathComponents">Entry path components.</param>
    /// <returns>Path component match indicating if root path components matches entry path components together with matching path components.</returns>
    public static PathComponentMatch MatchPathComponents(string[] rootPathComponents, string[] entryPathComponents)
    {
        // return all entry path components as match, if root path components is empty or
        // root path components equal entry path components
        if (rootPathComponents.Length == 0 ||
            rootPathComponents.SequenceEqual(entryPathComponents))
        {
            return new PathComponentMatch(true, rootPathComponents);
        }

        var rootPathHasWildcard = HasWildcard(rootPathComponents[^1]);
        var rootPathComponentsWithoutWildcard = rootPathHasWildcard
            ? rootPathComponents.Take(rootPathComponents.Length - 1).ToArray()
            : rootPathComponents;
        if (rootPathHasWildcard && rootPathComponentsWithoutWildcard.SequenceEqual(
                entryPathComponents.Take(rootPathComponentsWithoutWildcard.Length)))
        {
            return new PathComponentMatch(true, rootPathComponentsWithoutWildcard);
        }

        // return root path components as match, if they are equal the beginning of entry path components
        // otherwise return no match
        return rootPathComponents.SequenceEqual(entryPathComponents.Take(rootPathComponents.Length))
            ? new PathComponentMatch(true, rootPathComponents)
            : new PathComponentMatch(false, []);
    }
}

public record PathComponentMatch(bool Success, string[] MatchingPathComponents);