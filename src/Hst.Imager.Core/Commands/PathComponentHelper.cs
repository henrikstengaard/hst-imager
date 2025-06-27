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
    /// <param name="doesLastPathComponentExist">Does the last part of root path component exist.</param>
    /// <param name="isSingleEntryOperation">Is it a single file operation.</param>
    /// <returns></returns>
    public static string[] GetFullPathComponents(EntryType srcEntryType, string[] srcPathComponents, string[] destPathComponents,
        bool doesLastPathComponentExist, bool isSingleEntryOperation)
    {
        var dirPathComponents = doesLastPathComponentExist ? destPathComponents : destPathComponents.Take(destPathComponents.Length - 1).ToArray();
        
        // return dir path components if the entry type is a directory and it is a single entry operation.
        // this allows the directories for the single file to be created for the last path root component that doesn't exist.
        // it basically ignoring the entry path components.
        if (srcEntryType == EntryType.Dir && isSingleEntryOperation)
        {
            return dirPathComponents;
        }
        
        var isSingleFileCopyAndRename = isSingleEntryOperation && !doesLastPathComponentExist;

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
}