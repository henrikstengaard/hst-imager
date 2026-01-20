using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Hst.Core;
using UaeMetadatas;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

public class FsCopyCommand(
    ILogger<FsCopyCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string srcPath,
    string destPath,
    bool recursive,
    bool skipAttributes,
    bool quiet,
    bool makeDirectory = false,
    bool forceOverwrite = false,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    private readonly ILogger<FsCopyCommand> logger = logger;

    /// <summary>
    /// Is single file or uses pattern examines if the operation involves 1 file or if the operation uses pattern.
    /// </summary>
    /// <param name="entry">Entry to examine.</param>
    /// <param name="entryIterator">Entry iterator to examine.</param>
    /// <returns>True, if entry is a file and there are no more entries or if there is only a single file entry next or if the entry iterator uses a pattern. Otherwise, false.</returns>
    private static bool IsSingleFileOrUsesPattern(Entry entry, IEntryIterator entryIterator) =>
        (entry.Type == EntryType.File && !entryIterator.HasMoreEntries) ||
        entryIterator.IsSingleFileEntryNext ||
        entryIterator.UsesPattern;

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Copying from source Path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get destination entry writer
        var destEntryWriterResult = await GetEntryWriter(destPath, recursive, makeDirectory, forceOverwrite);
        if (destEntryWriterResult.IsFaulted)
        {
            return new Result(destEntryWriterResult.Error);
        }

        // get source copy entry iterator
        var srcEntryIteratorResult = await GetCopyEntryIterator(destEntryWriterResult.Value, srcPath);
        if (srcEntryIteratorResult.IsFaulted)
        {
            return new Result(srcEntryIteratorResult.Error);
        }

        if (destEntryWriterResult.Value.ArePathComponentsSelfCopy(srcEntryIteratorResult.Value))
        {
            return new Result(new SelfCopyError($"Unable to copy from source path '{srcPath}' to destination path '{destPath}' onto itself"));
        }

        if (destEntryWriterResult.Value.ArePathComponentsCyclic(srcEntryIteratorResult.Value))
        {
            return new Result(new CyclicPathError($"Unable to copy cyclic path from source path '{srcPath}' to destination path '{destPath}'"));
        }
        
        srcEntryIteratorResult.Value.UaeMetadata = srcEntryIteratorResult.Value.SupportsUaeMetadata &&
                                                   uaeMetadata != UaeMetadata.None ? uaeMetadata : UaeMetadata.None;
        destEntryWriterResult.Value.UaeMetadata = destEntryWriterResult.Value.SupportsUaeMetadata &&
                                                  uaeMetadata != UaeMetadata.None ? uaeMetadata : UaeMetadata.None;

        // iterate through source entries and write in destination
        var count = 0;
        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        stopwatch.Start();

        using (var destEntryWriter = destEntryWriterResult.Value)
        {
            using (var srcEntryIterator = srcEntryIteratorResult.Value)
            {
                while (await srcEntryIterator.Next())
                {
                    var entry = srcEntryIterator.Current;

                    var isSingleFileOrUsesPattern = IsSingleFileOrUsesPattern(entry, srcEntryIterator);

                    // skip directory entries when there are no entry path components or when it is a single file or uses pattern.
                    if (entry.Type == EntryType.Dir &&
                        (isSingleFileOrUsesPattern || entry.RelativePathComponents.Length == 0))
                    {
                        continue;
                    }

                    switch (entry.Type)
                    {
                        case EntryType.Dir:
                            dirsCount++;
                            var createDirectoryResult = await destEntryWriter.CreateDirectory(entry,
                                entry.RelativePathComponents, skipAttributes, isSingleFileOrUsesPattern);
                            if (createDirectoryResult.IsFaulted)
                            {
                                return new Result(createDirectoryResult.Error);
                            }
                            break;
                        case EntryType.File:
                        {
                            filesCount++;
                            totalBytes += entry.Size;

                            if (!quiet)
                            {
                                OnInformationMessage($"{entry.FormattedName} ({entry.Size.FormatBytes()})");
                            }

                            await using var stream = await srcEntryIterator.OpenEntry(entry);
                            var createFileResult = await destEntryWriter.CreateFile(entry,
                                entry.RelativePathComponents, stream,
                                skipAttributes, isSingleFileOrUsesPattern);
                            if (createFileResult.IsFaulted)
                            {
                                return new Result(createFileResult.Error);
                            }
                            break;
                        }
                    }

                    count++;

                    if (count <= 200)
                    {
                        continue;
                    }

                    count = 0;
                    await srcEntryIterator.Flush();
                    await destEntryWriter.Flush();
                }

                await srcEntryIterator.Flush();
                await destEntryWriter.Flush();
            }

            foreach (var log in destEntryWriter.GetDebugLogs())
            {
                OnDebugMessage(log);
            }

            foreach (var log in destEntryWriter.GetLogs())
            {
                OnInformationMessage(log);
            }
        }

        stopwatch.Stop();

        var stats = new List<string>();
        if (dirsCount > 0 || filesCount == 0)
        {
            stats.Add($"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}");
        }
        if (filesCount > 0 || dirsCount == 0)
        {
            stats.Add($"{filesCount} {(filesCount == 1 ? "file" : "files")}");
        }
        stats.Add($"{totalBytes.FormatBytes()} copied in {stopwatch.Elapsed.FormatElapsed()}");
        
        OnInformationMessage(string.Join(", ", stats));

        return new Result();
    }

    protected async Task<Result<IEntryIterator>> GetCopyEntryIterator(IEntryWriter entryWriter, string path)
    {
        // get directory entry iterator and return if successful
        var directoryEntryIterator = await GetDirectoryEntryIterator(path, recursive);
        if (directoryEntryIterator != null && directoryEntryIterator.IsSuccess)
        {
            var initializeResult = await directoryEntryIterator.Value.Initialize();
            return initializeResult.IsSuccess
                ? new Result<IEntryIterator>(directoryEntryIterator.Value)
                : new Result<IEntryIterator>(initializeResult.Error);
        }

        // path is not a directory, so must be a file
        
        OnDebugMessage($"Resolving path '{path}'");

        var mediaResult = commandHelper.ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"File system Path: '{mediaResult.Value.FileSystemPath}'");
        
        // file entry iterator
        if (string.IsNullOrWhiteSpace(mediaResult.Value.FileSystemPath))
        {
            var fileEntryIterator = await GetFileEntryIterator(path, recursive);
            if (fileEntryIterator != null && fileEntryIterator.IsSuccess)
            {
                var initializeResult = await fileEntryIterator.Value.Initialize();
                return initializeResult.IsSuccess
                    ? new Result<IEntryIterator>(fileEntryIterator.Value)
                    : new Result<IEntryIterator>(initializeResult.Error);
            }
        }
        
        // get readable media for entry iterator
        var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
        if (readableMediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(readableMediaResult.Error);
        }
        
        var fileSystemPath = mediaResult.Value.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = mediaResult.Value.DirectorySeparatorChar;

        // get pistorm rdb media result from media
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            readableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        // get file system path and media path
        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;
        var mediaPath = media.Path;
        
        // get entry iterator file system path components
        var entryIteratorFileSystemPathComponents =
            fileSystemPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        
        // get entry writer file system path components
        var entryWriterFileSystemPathComponents =
            entryWriter.FileSystemPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
                .ToArray();

        // return entry iterator from entry writer, if media is adf and media path is the same
        if (mediaPath.EndsWith(".adf") && mediaPath == entryWriter.MediaPath)
        {
            var rootPathComponents = entryIteratorFileSystemPathComponents;
            var entryIterator = entryWriter.CreateEntryIterator(rootPathComponents, recursive);
            var initializeResult = await entryIterator.Initialize();
            return initializeResult.IsSuccess
                ? new Result<IEntryIterator>(entryIterator)
                : new Result<IEntryIterator>(initializeResult.Error);
        }
        
        // return entry iterator from entry writer, if media path is the same and
        // entry iterator and writer file system path components starts with the
        // same 2 path components, e.g. "rdb\1"
        if (mediaPath == entryWriter.MediaPath &&
            entryIteratorFileSystemPathComponents.Take(2).SequenceEqual(
                entryWriterFileSystemPathComponents)) // and only if first two parts of file system path is equal
        {
            var rootPathComponents = entryIteratorFileSystemPathComponents.Skip(2).ToArray();
            var entryIterator = entryWriter.CreateEntryIterator(rootPathComponents, recursive);
            var initializeResult = await entryIterator.Initialize();
            return initializeResult.IsSuccess
                ? new Result<IEntryIterator>(entryIterator)
                : new Result<IEntryIterator>(initializeResult.Error);
        }

        // floppy or disk entry iterator
        var diskEntryIterator = await GetDiskEntryIterator(mediaResult.Value, recursive, false, 100 * 1024 * 1024, 512);
        if (diskEntryIterator != null && diskEntryIterator.IsSuccess)
        {
            var initializeResult = await diskEntryIterator.Value.Initialize();
            return initializeResult.IsSuccess
                ? new Result<IEntryIterator>(diskEntryIterator.Value)
                : new Result<IEntryIterator>(initializeResult.Error);
        }

        return new Result<IEntryIterator>(new Error($"Unsupported path '{path}'"));
    }
}