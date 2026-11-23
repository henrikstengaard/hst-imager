using Hst.Imager.Core.Caching;
using Hst.Imager.Core.MagicBytes;
using Hst.Imager.Core.Volumes;

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

    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);
        using var volumeFactory = CreateVolumeFactory(uaeMetadataHelper, uaeMetadata);

        OnInformationMessage($"Extracting from source path '{srcPath}' to destination path '{destPath}'");

        var stopwatch = new Stopwatch();

        // get source extract walker
        var srcWalkerResult = await GetExtractWalker(volumeFactory, srcPath, recursive);
        if (srcWalkerResult.IsFaulted)
        {
            return new Result(srcWalkerResult.Error);
        }

        var srcWalker = srcWalkerResult.Value;

        // get destination target
        var destTargetResult = await OpenVolumeTarget(volumeFactory, destPath, recursive, makeDirectory,
            forceOverwrite);
        if (destTargetResult.IsFaulted)
        {
            return new Result(destTargetResult.Error);
        }

        var destTarget = destTargetResult.Value;

        if (destTarget.IsSelfCopy(srcWalker))
        {
            return new Result(new CyclicPathError($"Unable to extract from source path '{srcPath}' to destination path '{destPath}' onto itself"));
        }

        if (destTarget.IsCyclic(srcWalker))
        {
            return new Result(new CyclicPathError($"Unable to extract cyclic path from source path '{srcPath}' to destination path '{destPath}'"));
        }

        // iterate through source entries and write in destination
        var count = 0;
        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        stopwatch.Start();

        while (await srcWalker.Next())
        {
            var entry = srcWalker.Current;

            var isSingleFileOrUsesPattern = IsSingleFileOrUsesPattern(entry, srcWalker);

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
                    var createDirectoryResult = await destTarget.CreateDirectory(entry, entry.RelativePathComponents,
                        skipAttributes, isSingleFileOrUsesPattern);
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

                    var openResult = await srcWalker.Volume.OpenFile(entry.FullPathComponents);
                    if (openResult.IsFaulted)
                    {
                        return new Result(openResult.Error);
                    }

                    await using var stream = openResult.Value;
                    var createFileResult = await destTarget.CreateFile(entry, entry.RelativePathComponents, stream,
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
            await srcWalker.Volume.Flush();
            await destTarget.Volume.Flush();
        }

        await srcWalker.Volume.Flush();
        await destTarget.Volume.Flush();

        foreach (var log in destTarget.Volume.GetDebugLogs())
        {
            OnDebugMessage(log);
        }

        foreach (var log in destTarget.Volume.GetLogs())
        {
            OnInformationMessage(log);
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

    private async Task<Result<VolumeEntryWalker>> GetExtractWalker(VolumeFactory volumeFactory, string path,
        bool recursive)
    {
        OnDebugMessage($"Resolving path '{path}'");

        var mediaResult = commandHelper.ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<VolumeEntryWalker>(mediaResult.Error);
        }

        if (string.IsNullOrWhiteSpace(mediaResult.Value.MediaPath))
        {
            return new Result<VolumeEntryWalker>(
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
        if (dataType is not (DataType.Zip or DataType.Lha or DataType.Lzx or DataType.Lzw or DataType.Adf
            or DataType.Iso9660))
        {
            return new Result<VolumeEntryWalker>(new Error($"File system at path '{path}' not supported"));
        }

        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Read);
        if (volumePathResult.IsFaulted)
        {
            return new Result<VolumeEntryWalker>(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;
        return await VolumeEntryWalker.Create(volume, pathComponents, recursive);
    }
}
