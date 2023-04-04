namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;
using File = System.IO.File;

public class BlockReadCommand : CommandBase
{
    private readonly ILogger<BlockReadCommand> logger;
    private readonly byte[] buffer;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly string outputPath;
    private readonly int blockSize;
    private readonly bool used;
    private readonly long? start;
    private readonly long? end;

    public BlockReadCommand(ILogger<BlockReadCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string outputPath, int blockSize, bool used,
        long? start, long? end)
    {
        this.logger = logger;
        
        this.buffer = new byte[blockSize > 1024 * 1024 ? blockSize : 1024 * 1024];
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.outputPath = outputPath;
        this.blockSize = blockSize;
        this.used = used;
        this.start = start;
        this.end = end;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (blockSize % 512 != 0)
        {
            return new Result(new Error("Block size must be dividable by 512"));
        }

        if (start.HasValue && start.Value % blockSize != 0)
        {
            return new Result(new Error($"Start offset must be dividable by block size {blockSize}"));
        }

        if (end.HasValue && end.Value % blockSize != 0)
        {
            return new Result(new Error($"End offset must be dividable by block size {blockSize}"));
        }

        OnInformationMessage($"Reading blocks from '{path}' to '{outputPath}'");

        OnDebugMessage($"Opening '{path}' as readable");

        var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path, allowPhysicalDrive: true);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        stream.Position = start ?? 0;
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        var offset = start ?? 0;
        long blocksRead = 0;
        int bytesRead;
        do
        {
            
            try
            {
                stream.Seek(offset, SeekOrigin.Begin);
                bytesRead = await stream.ReadAsync(this.buffer, 0, this.buffer.Length, token);
            }
            catch (Exception e)
            {
                return new Result(new Error($"Failed to read: {e}"));
            }

            var sectors = DataSectorReader.Read(buffer, blockSize, bytesRead, !used).ToList();
            
            foreach (var sector in sectors)
            {
                var sectorStart = offset + sector.Start;
                if (start.HasValue && sectorStart < start.Value)
                {
                    continue;
                }
                
                if (end.HasValue && sectorStart >= end.Value)
                {
                    break;
                }

                OnDebugMessage($"Writing block offset {sectorStart}");

                var sectorPath = Path.Combine(outputPath, $"{sectorStart}.bin");
                var sectorBytes = new byte[blockSize];
                Array.Copy(buffer, sector.Start, sectorBytes, 0, sector.Size);
                await File.WriteAllBytesAsync(sectorPath, sectorBytes, cancellationTokenSource.Token);
                blocksRead++;
            }

            offset += bytesRead;

            if (end.HasValue && offset > end.Value || cancellationTokenSource.Token.IsCancellationRequested)
            {
                break;
            }
        } while (bytesRead == this.buffer.Length);

        OnInformationMessage($"Read {blocksRead} blocks");

        return new Result();
    }
}