using Hst.Imager.Core.MagicBytes;

namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Hst.Core;
using UaeMetadatas;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

public class FsExtractCommand(
    ILogger<FsExtractCommand> logger,
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
    private readonly ILogger<FsExtractCommand> logger = logger;

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
        OnInformationMessage($"Extracting from source path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get source extract entry iterator
        var srcEntryIteratorResult = await GetExtractEntryIterator(srcPath, recursive);
        if (srcEntryIteratorResult.IsFaulted)
        {
            return new Result(srcEntryIteratorResult.Error);
        }

        // get destination entry writer
        var destEntryWriterResult = await GetEntryWriter(destPath, recursive, makeDirectory, forceOverwrite);
        if (destEntryWriterResult.IsFaulted)
        {
            return new Result(destEntryWriterResult.Error);
        }

        if (destEntryWriterResult.Value.ArePathComponentsSelfCopy(srcEntryIteratorResult.Value))
        {
            return new Result(new CyclicPathError($"Unable to extract from source path '{srcPath}' to destination path '{destPath}' onto itself"));
        }

        if (destEntryWriterResult.Value.ArePathComponentsCyclic(srcEntryIteratorResult.Value))
        {
            return new Result(new CyclicPathError($"Unable to extract cyclic path from source path '{srcPath}' to destination path '{destPath}'"));
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
                        case EntryType.LinkDir:
                            dirsCount++;
                            var createDirectoryResult = await destEntryWriter.CreateDirectory(entry, entry.RelativePathComponents, skipAttributes,
                                isSingleFileOrUsesPattern);
                            if (createDirectoryResult.IsFaulted)
                            {
                                return new Result(createDirectoryResult.Error);
                            }
                            break;
                        case EntryType.File:
                        case EntryType.LinkFile:
                        {
                            filesCount++;
                            totalBytes += entry.Size;

                            if (!quiet)
                            {
                                OnInformationMessage($"{entry.FormattedName} ({entry.Size.FormatBytes()})");
                            }

                            await using var stream = await srcEntryIterator.OpenEntry(entry);
                            var createFileResult = await destEntryWriter.CreateFile(entry, entry.RelativePathComponents, stream, skipAttributes,
                                isSingleFileOrUsesPattern);
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
        stats.Add($"{totalBytes.FormatBytes()} extracted in {stopwatch.Elapsed.FormatElapsed()}");
        
        OnInformationMessage(string.Join(", ", stats));

        return new Result();
    }
    
    protected async Task<Result<IEntryIterator>> GetExtractEntryIterator(string path, bool recursive)
    {
        OnDebugMessage($"Resolving path '{path}'");

        var mediaResult = commandHelper.ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{mediaResult.Value.FileSystemPath}'");

        if (string.IsNullOrWhiteSpace(mediaResult.Value.MediaPath))
        {
            return new Result<IEntryIterator>(
                new PathNotFoundError($"Media path not defined",
                    mediaResult.Value.MediaPath));
        }

        // set recursive true, if not recursive and virtual path is empty
        // when extracting an archive specifying only the archive filename,
        // it should extract all directories and files in it.
        // when extracting an archive specifying archive filename and a virtual path to a file,
        // it should leave recursive as is.
        if (!recursive && string.IsNullOrEmpty(mediaResult.Value.FileSystemPath))
        {
            recursive = true;
        }
        
        var dataType = await commandHelper.DetectDataType(mediaResult.Value.MediaPath);

        Result<IEntryIterator> entryIteratorResult = null;
        switch (dataType)
        {
            case DataType.Zip:
                entryIteratorResult = await GetZipEntryIterator(mediaResult.Value, recursive);
                break;
            case DataType.Lha:
                entryIteratorResult = await GetLhaEntryIterator(mediaResult.Value, recursive);
                break;
            case DataType.Lzx:
                entryIteratorResult = await GetLzxEntryIterator(mediaResult.Value, recursive);
                break;
            case DataType.Lzw:
                entryIteratorResult = await GetLzwEntryIterator(mediaResult.Value);
                break;
            case DataType.Adf:
                entryIteratorResult = await GetAdfEntryIterator(mediaResult.Value, recursive);
                break;
            case DataType.Iso9660:
                entryIteratorResult = await GetIso9660EntryIterator(mediaResult.Value, recursive);
                break;
        }
        
        if (entryIteratorResult != null && entryIteratorResult.IsSuccess)
        {
            var initializeResult = await entryIteratorResult.Value.Initialize();
            return initializeResult.IsSuccess
                ? new Result<IEntryIterator>(entryIteratorResult.Value)
                : new Result<IEntryIterator>(initializeResult.Error);
        }

        return new Result<IEntryIterator>(new Error($"File system at path '{path}' not supported"));
    }
}