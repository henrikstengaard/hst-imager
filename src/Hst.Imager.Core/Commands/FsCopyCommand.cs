namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;

public class FsCopyCommand : FsCommandBase
{
    private readonly ILogger<FsCopyCommand> logger;
    private readonly string srcPath;
    private readonly string destPath;
    private readonly bool recursive;

    public FsCopyCommand(ILogger<FsCopyCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string srcPath, string destPath, bool recursive) 
        : base(commandHelper, physicalDrives)
    {
        this.logger = logger;
        this.srcPath = srcPath;
        this.destPath = destPath;
        this.recursive = recursive;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Source Path: '{srcPath}'");
        // var resolvedSrcPathResult = ResolvePath(srcPath);
        // var resolvedDestPathResult = ResolvePath(destPath);
        //
        // OnInformationMessage($"Existing Path: '{resolvedSrcPathResult.Value.ExistingPath}'");
        // OnInformationMessage($"Virtual Path: '{resolvedSrcPathResult.Value.VirtualPath}'");
        
        var isSrcDirectory = Directory.Exists(srcPath);
        var isDestDirectory = Directory.Exists(destPath);
        
        var srcEntryIteratorResult = await GetEntryIterator(srcPath, recursive);
        if (srcEntryIteratorResult.IsFaulted)
        {
            return new Result(srcEntryIteratorResult.Error);
        }

        using (var srcEntryIterator = srcEntryIteratorResult.Value)
        {
            while (await srcEntryIterator.Next())
            {
                var entry = srcEntryIterator.Current;
                OnInformationMessage($"[{entry.Type}] {entry.Name} ({entry.Path})");
            }
        }
        
        return new Result();
    }


    // private Result<MediaResult> ResolvePath(string path)
    // {
    //     string existingPath;
    //     var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(path);
    //     if (physicalDrivePathMatch.Success)
    //     {
    //         existingPath = physicalDrivePathMatch.Value;
    //         var firstSeparatorIndex = path.IndexOf("\\", existingPath.Length, StringComparison.Ordinal);
    //
    //         return new Result<MediaResult>(new MediaResult
    //         {
    //             MediaPath = existingPath,
    //             VirtualPath = firstSeparatorIndex >= 0 ? path.Substring(firstSeparatorIndex + 1, path.Length - (firstSeparatorIndex + 1)) : string.Empty
    //         });
    //     }
    //
    //     var lastSeparatorIndex = path.Length;
    //     var exists = false;
    //
    //     do
    //     {
    //         existingPath = path.Substring(0, lastSeparatorIndex);
    //         if (System.IO.File.Exists(existingPath))
    //         {
    //             exists = true;
    //             break;
    //         }
    //
    //         var start = lastSeparatorIndex - 1;
    //         lastSeparatorIndex = path.LastIndexOf("\\", start, StringComparison.Ordinal);
    //         if (lastSeparatorIndex == -1)
    //         {
    //             lastSeparatorIndex = path.LastIndexOf("/", start, StringComparison.Ordinal);
    //         }
    //     } while (lastSeparatorIndex != -1);
    //
    //     if (!exists)
    //     {
    //         return new Result<MediaResult>(new PathNotFoundError("Path not found", path));
    //     }
    //     
    //     return new Result<MediaResult>(new MediaResult
    //     {
    //         MediaPath = existingPath,
    //         VirtualPath = path.Substring(existingPath.Length + 1, path.Length - (existingPath.Length + 1))
    //     });
    // }

    // private async Task<Result<IEnumerable<Entry>>> ReadEntries(Stream stream, string[] parts)
    // {
    //     if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
    //     {
    //         OnDebugMessage($"Listing partition tables");
    //
    //         var entries = new List<Entry>();
    //         var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);
    //
    //         if (rigidDiskBlock != null)
    //         {
    //             entries.Add(new Entry
    //             {
    //                 Name = "RDB",
    //                 Type = EntryType.Dir,
    //                 Size = 0
    //             });
    //         }
    //
    //         return new Result<IEnumerable<Entry>>(entries);
    //     }
    //
    //     return parts[0] switch
    //     {
    //         "rdb" => await MountRdbFileSystemVolume(stream, parts.Skip(1).ToArray()),
    //         _ => new Result<IEnumerable<Entry>>(new Error($"Unsupported partition table '{parts[0]}'"))
    //     };
    // }
}