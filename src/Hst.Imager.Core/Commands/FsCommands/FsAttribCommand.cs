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
using Hst.Imager.Core.Volumes;

namespace Hst.Imager.Core.Commands.FsCommands;

/// <summary>
/// Set attributes or protection bits of files and directories.
/// A path to a directory sets attributes of the directory, a path with wildcard sets attributes of matching
/// entries and recursive also sets attributes of entries in sub-directories.
/// Amiga protection bits are set for local directories and files, when UAE metadata is specified and attributes
/// mode is auto.
/// </summary>
public class FsAttribCommand(
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    IEnumerable<string> attributes,
    bool recursive,
    AttributesMode attributesMode = AttributesMode.Auto,
    UaeMetadata uaeMetadata = UaeMetadata.None)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);

        using var volumeFactory = CreateVolumeFactory(uaeMetadataHelper, uaeMetadata);

        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Write, openArchives: false);
        if (volumePathResult.IsFaulted)
        {
            return new Result(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        var walkerResult = await VolumeEntryWalker.Create(volume, pathComponents, recursive);
        if (walkerResult.IsFaulted)
        {
            return new Result(walkerResult.Error);
        }

        var walker = walkerResult.Value;

        var mode = AttributesChange.ResolveMode(attributesMode, volume.AttributesMode);
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
        var isDirPath = !walker.IsSingleFileEntryNext &&
                        walker.PathComponents.Length == walker.DirPathComponents.Length;

        if (isDirPath && walker.DirPathComponents.Length == 0 && !recursive)
        {
            return new Result(new Error(
                $"Path '{path}' is root directory, use wildcard or recursive to set attributes of entries"));
        }

        var entries = new List<Entry>();

        // set attributes of directory itself
        if (isDirPath && walker.DirPathComponents.Length > 0)
        {
            entries.Add(new Entry
            {
                Type = EntryType.Dir,
                FullPathComponents = walker.DirPathComponents,
                RelativePathComponents = []
            });
        }

        // set attributes of entries, if path is a single file, uses wildcard or is recursive
        if (!isDirPath || recursive)
        {
            while (await walker.Next())
            {
                var entry = walker.Current;

                // skip directory entries when there are no entry path components or when it's a single file.
                if (entry.Type == EntryType.Dir &&
                    (walker.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
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

            var setAttributesResult = await volume.SetAttributes(entry.FullPathComponents, attributesChange);
            if (setAttributesResult.IsFaulted)
            {
                await volume.Flush();
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
                $"{attributesChange.Format(setAttributesResult.Value)} {volume.MediaPath.Join(entry.FullPathComponents)}");
        }

        await volume.Flush();

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
