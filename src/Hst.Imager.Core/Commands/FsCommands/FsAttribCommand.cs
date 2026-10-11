using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Commands.FsCommands;

/// <summary>
/// Set attributes or protection bits of files and directories.
/// A path to a directory sets attributes of the directory, a path with wildcard sets attributes of matching
/// entries and recursive also sets attributes of entries in sub-directories.
/// </summary>
public class FsAttribCommand(
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    IEnumerable<string> attributes,
    bool recursive,
    AttributesMode attributesMode = AttributesMode.Auto,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);

        var entryIteratorResult = await GetWritableEntryIterator(path, recursive, uaeMetadata, uaeMetadataHelper);
        if (entryIteratorResult.IsFaulted)
        {
            return new Result(entryIteratorResult.Error);
        }

        using var entryIterator = entryIteratorResult.Value;

        var mode = AttributesChange.ResolveMode(attributesMode, entryIterator.AttributesMode);
        var attributesChangeResult = AttributesChange.Parse(mode, attributes);
        if (attributesChangeResult.IsFaulted)
        {
            return new Result(attributesChangeResult.Error);
        }

        var attributesChange = attributesChangeResult.Value;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        OnInformationMessage($"Setting {mode.ToString().ToLowerInvariant()} attributes for path '{path}'");

        // path is a directory, when it's not a single file and all path components exist as directories
        var isDirPath = !entryIterator.IsSingleFileEntryNext &&
                        entryIterator.PathComponents.Length == entryIterator.DirPathComponents.Length;

        if (isDirPath && entryIterator.DirPathComponents.Length == 0 && !recursive)
        {
            return new Result(new Error(
                $"Path '{path}' is root directory, use wildcard or recursive to set attributes of entries"));
        }

        var entries = new List<Entry>();

        // set attributes of directory itself
        if (isDirPath && entryIterator.DirPathComponents.Length > 0)
        {
            entries.Add(new Entry
            {
                Type = EntryType.Dir,
                FullPathComponents = entryIterator.DirPathComponents,
                RelativePathComponents = []
            });
        }

        // set attributes of entries, if path is a single file, uses wildcard or is recursive
        if (!isDirPath || recursive)
        {
            while (await entryIterator.Next())
            {
                var entry = entryIterator.Current;

                // skip directory entries when there are no entry path components or when it's a single file.
                if (entry.Type == EntryType.Dir &&
                    (entryIterator.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
                {
                    continue;
                }

                entries.Add(entry);
            }
        }

        var filesCount = 0;
        var dirsCount = 0;

        foreach (var entry in entries)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            var setAttributesResult = await entryIterator.SetAttributes(entry.FullPathComponents, attributesChange);
            if (setAttributesResult.IsFaulted)
            {
                await entryIterator.Flush();
                return new Result(setAttributesResult.Error);
            }

            switch (entry.Type)
            {
                case EntryType.Dir:
                case EntryType.LinkDir:
                    dirsCount++;
                    break;
                default:
                    filesCount++;
                    break;
            }

            OnInformationMessage(
                $"{attributesChange.Format(setAttributesResult.Value)} {entryIterator.MediaPath.Join(entry.FullPathComponents)}");
        }

        await entryIterator.Flush();

        stopwatch.Stop();

        var stats = new List<string>();
        if (dirsCount > 0 || filesCount == 0)
        {
            stats.Add($"{dirsCount} {(dirsCount == 1 ? "directory" : "directories")}");
        }
        if (filesCount > 0 || dirsCount == 0)
        {
            stats.Add($"{filesCount} {(filesCount == 1 ? "file" : "files")}");
        }

        OnInformationMessage($"{string.Join(", ", stats)} updated in {stopwatch.Elapsed.FormatElapsed()}");

        return new Result();
    }
}
