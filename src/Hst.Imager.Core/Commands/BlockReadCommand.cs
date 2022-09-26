namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

public class BlockReadCommand : CommandBase
{
    private readonly ILogger<BlockReadCommand> logger;
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
        await using var stream = media.Stream;

        if (start.HasValue)
        {
            var startOffset = start.Value / blockSize;
            stream.Seek(startOffset, SeekOrigin.Begin);
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        stream.Position = start ?? 0;
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        var dataSectorReader = new DataSectorReader(stream, blockSize);

        SectorResult sectorResult;
        long blocksRead = 0;
        do
        {
            sectorResult = await dataSectorReader.ReadNext();

            var sectors = used ? sectorResult.Sectors.Where(x => !x.IsZeroFilled) : sectorResult.Sectors;

            foreach (var sector in sectors)
            {
                if (end.HasValue && sector.Start >= end.Value)
                {
                    cancellationTokenSource.Cancel();
                    break;
                }

                var sectorPath = Path.Combine(outputPath, $"{sector.Start}.bin");
                await File.WriteAllBytesAsync(sectorPath, sector.Data, cancellationTokenSource.Token);
                blocksRead++;
            }

            if (end.HasValue && sectorResult.Start > end.Value || cancellationTokenSource.Token.IsCancellationRequested)
            {
                break;
            }
        } while (!sectorResult.EndOfSectors);

        OnInformationMessage($"Read {blocksRead} blocks");

        return new Result();
    }
}