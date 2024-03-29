﻿namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

public class FsExtractCommand : FsCommandBase
{
    private readonly ILogger<FsExtractCommand> logger;
    private readonly string srcPath;
    private readonly string destPath;
    private readonly bool recursive;
    private readonly bool skipAttributes;
    private readonly bool quiet;

    public FsExtractCommand(ILogger<FsExtractCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string srcPath, string destPath, bool recursive, bool skipAttributes, bool quiet)
        : base(commandHelper, physicalDrives)
    {
        this.logger = logger;
        this.srcPath = srcPath;
        this.destPath = destPath;
        this.recursive = recursive;
        this.skipAttributes = skipAttributes;
        this.quiet = quiet;
    }

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
        var destEntryWriterResult = await GetEntryWriter(destPath, false);
        if (destEntryWriterResult.IsFaulted)
        {
            return new Result(destEntryWriterResult.Error);
        }

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
                            if (!recursive || srcEntryIterator.UsesPattern)
                            {
                                continue;
                            }

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
                            await destEntryWriter.WriteEntry(entry, entry.RelativePathComponents, stream, skipAttributes);
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
            $"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}, {totalBytes.FormatBytes()} extracted in {stopwatch.Elapsed.FormatElapsed()}");

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

        // zip
        var zipEntryIterator = await GetZipEntryIterator(mediaResult.Value, recursive);
        if (zipEntryIterator != null && zipEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(zipEntryIterator.Value);
        }

        // lha
        var lhaEntryIterator = await GetLhaEntryIterator(mediaResult.Value, recursive);
        if (lhaEntryIterator != null && lhaEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(lhaEntryIterator.Value);
        }

        // lzx
        var lzxEntryIterator = await GetLzxEntryIterator(mediaResult.Value, recursive);
        if (lzxEntryIterator != null && lzxEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(lzxEntryIterator.Value);
        }
        
        // lzw
        var lzwEntryIterator = await GetLzwEntryIterator(mediaResult.Value);
        if (lzwEntryIterator != null && lzwEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(lzwEntryIterator.Value);
        }

        // adf
        var adfEntryIterator = await GetAdfEntryIterator(mediaResult.Value, recursive);
        if (adfEntryIterator != null && adfEntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(adfEntryIterator.Value);
        }

        // iso
        var iso9660EntryIterator = await GetIso9660EntryIterator(mediaResult.Value, recursive);
        if (iso9660EntryIterator != null && iso9660EntryIterator.IsSuccess)
        {
            return new Result<IEntryIterator>(iso9660EntryIterator.Value);
        }

        return new Result<IEntryIterator>(new Error($"File system at path '{path}' not supported"));
    }

    private static string TrimRootPath(string rootPath, string entryPath)
    {
        var trimmedEntryPath = rootPath.Length > 0 ? entryPath.Substring(rootPath.Length) : entryPath;

        return trimmedEntryPath.StartsWith("\\") || trimmedEntryPath.StartsWith("/")
            ? trimmedEntryPath.Substring(1)
            : trimmedEntryPath;
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