namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

public class FsCopyCommand : FsCommandBase
{
    private readonly ILogger<FsCopyCommand> logger;
    private readonly string srcPath;
    private readonly string destPath;
    private readonly bool recursive;
    private readonly bool quiet;

    public FsCopyCommand(ILogger<FsCopyCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string srcPath, string destPath, bool recursive, bool quiet)
        : base(commandHelper, physicalDrives)
    {
        this.logger = logger;
        this.srcPath = srcPath;
        this.destPath = destPath;
        this.recursive = recursive;
        this.quiet = quiet;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Copying from source Path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get source copy entry iterator
        var srcEntryIteratorResult = await GetCopyEntryIterator(srcPath, recursive);
        if (srcEntryIteratorResult.IsFaulted)
        {
            return new Result(srcEntryIteratorResult.Error);
        }

        // get destination entry writer
        var destEntryWriterResult = await GetEntryWriter(destPath);
        if (destEntryWriterResult.IsFaulted)
        {
            return new Result(destEntryWriterResult.Error);
        }

        // iterate through source entries and write in destination
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
                            if (!recursive || srcEntryIterator.UsesPattern)
                            {
                                continue;
                            }

                            dirsCount++;
                            await destEntryWriter.CreateDirectory(entry, entry.RelativePathComponents);
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
                            await destEntryWriter.WriteEntry(entry, entry.RelativePathComponents, stream);
                            break;
                        }
                    }
                }
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

        OnInformationMessage(
            $"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}, {totalBytes.FormatBytes()} copied in {stopwatch.Elapsed.FormatElapsed()}");

        return new Result();
    }

    protected async Task<Result<IEntryIterator>> GetCopyEntryIterator(string path, bool recursive)
    {
        // directory entry iterator
        var directoryEntryIterator = await GetDirectoryEntryIterator(path, recursive);
        if (directoryEntryIterator != null && directoryEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(directoryEntryIterator.Value);
        }

        // file entry iterator
        var fileEntryIterator = await GetFileEntryIterator(path, recursive);
        if (fileEntryIterator != null && fileEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(fileEntryIterator.Value);
        }
        
        OnDebugMessage($"Resolving path '{path}'");

        var mediaResult = ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"File system Path: '{mediaResult.Value.FileSystemPath}'");
        
        // disk entry iterator
        var diskEntryIterator = await GetDiskEntryIterator(mediaResult.Value, recursive, false, 100 * 1024 * 1024, 512);
        if (diskEntryIterator != null && diskEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(diskEntryIterator.Value);
        }

        return new Result<IEntryIterator>(new Error($"Unsupported path '{path}'"));
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        for (int i = 0; i < normalizedString.Length; i++)
        {
            char c = normalizedString[i];
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}