namespace Hst.Imager.Core.Commands;

using System;
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
        OnInformationMessage($"Copying source Path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get source entry iterator
        var srcEntryIteratorResult = await GetEntryIterator(srcPath, recursive);
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

        var srcRootPath = srcEntryIteratorResult.Value.RootPath;

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
                            if (!recursive)
                            {
                                continue;
                            }

                            dirsCount++;
                            entry.Path = TrimRootPath(srcRootPath, entry.Path);
                            await destEntryWriter.CreateDirectory(entry);
                            break;
                        case EntryType.File:
                        {
                            filesCount++;
                            totalBytes += entry.Size;
                            var entryPath = TrimRootPath(srcRootPath, entry.Path);
                            if (!quiet)
                            {
                                OnInformationMessage($"{entryPath} ({entry.Size.FormatBytes()})");
                            }

                            await using var stream = await srcEntryIterator.OpenEntry(entry);
                            entry.Path = entryPath;
                            await destEntryWriter.WriteEntry(entry, stream);
                            break;
                        }
                    }
                }
            }
        }

        stopwatch.Stop();

        OnInformationMessage(
            $"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}, {totalBytes.FormatBytes()} copied in {stopwatch.Elapsed.FormatElapsed()}");

        return new Result();
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