namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Hst.Core;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

public class FsCopyCommand : FsCommandBase
{
    private readonly ILogger<FsCopyCommand> logger;
    private readonly string srcPath;
    private readonly string destPath;
    private readonly bool recursive;
    private readonly bool skipAttributes;
    private readonly bool quiet;
    private readonly UaeMetadata uaeMetadata;

    public FsCopyCommand(ILogger<FsCopyCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string srcPath, string destPath, bool recursive,
        bool skipAttributes, bool quiet, UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
        : base(commandHelper, physicalDrives)
    {
        this.logger = logger;
        this.srcPath = srcPath;
        this.destPath = destPath;
        this.recursive = recursive;
        this.skipAttributes = skipAttributes;
        this.quiet = quiet;
        this.uaeMetadata = uaeMetadata;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Copying from source Path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get destination entry writer
        var destEntryWriterResult = await GetEntryWriter(destPath, false);
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

        var supportsUaeMetadata = UaeMetadataHelper.EntryIteratorSupportsUaeMetadata(srcEntryIteratorResult.Value) &&
            UaeMetadataHelper.EntryWriterSupportsUaeMetadata(destEntryWriterResult.Value);

        srcEntryIteratorResult.Value.UaeMetadata = supportsUaeMetadata ? uaeMetadata : UaeMetadata.None;
        destEntryWriterResult.Value.UaeMetadata = supportsUaeMetadata ? uaeMetadata : UaeMetadata.None;

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

                    switch (entry.Type)
                    {
                        case EntryType.Dir:
                            dirsCount++;
                            await destEntryWriter.CreateDirectory(entry, entry.RelativePathComponents, skipAttributes);
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
                            await destEntryWriter.WriteEntry(entry, entry.RelativePathComponents, stream,
                                skipAttributes);
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
            }

            await destEntryWriter.Flush();

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

        OnInformationMessage(
            $"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}, {totalBytes.FormatBytes()} copied in {stopwatch.Elapsed.FormatElapsed()}");

        return new Result();
    }

    protected async Task<Result<IEntryIterator>> GetCopyEntryIterator(IEntryWriter entryWriter, string path)
    {
        // get directory entry iterator and return if successful
        var directoryEntryIterator = await GetDirectoryEntryIterator(path, recursive);
        if (directoryEntryIterator != null && directoryEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(directoryEntryIterator.Value);
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
                return new Result<IEntryIterator>(fileEntryIterator.Value);
            }
        }
        
        // create entry iterator from entry writer, if media is the same (copy from and to same media)
        var entryIteratorFileSystemPathComponents =
            mediaResult.Value.FileSystemPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        var entryWriterFileSystemPathComponents =
            entryWriter.FileSystemPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
                .ToArray();

        if (mediaResult.Value.MediaPath.EndsWith(".adf") && mediaResult.Value.MediaPath == entryWriter.MediaPath)
        {
            var rootPath = mediaResult.Value.FileSystemPath;
            return new Result<IEntryIterator>(entryWriter.CreateEntryIterator(rootPath,
                recursive));
        }
        
        if (mediaResult.Value.MediaPath == entryWriter.MediaPath &&
            entryIteratorFileSystemPathComponents.Take(2).SequenceEqual(
                entryWriterFileSystemPathComponents)) // and only if first two parts of file system path is equal
        {
            var rootPath = string.Join("/", entryIteratorFileSystemPathComponents.Skip(2));
            return new Result<IEntryIterator>(entryWriter.CreateEntryIterator(rootPath,
                recursive));
        }

        // floppy or disk entry iterator
        var diskEntryIterator = await GetDiskEntryIterator(mediaResult.Value, recursive, false, 100 * 1024 * 1024, 512);
        if (diskEntryIterator != null && diskEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(diskEntryIterator.Value);
        }

        return new Result<IEntryIterator>(new Error($"Unsupported path '{path}'"));
    }
}