namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

public class SectorExtractCommand : CommandBase
{
    private readonly ILogger<SectorExtractCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly string outputPath;
    private readonly int sectorSize;
    private readonly long? start;
    private readonly long? end;

    public SectorExtractCommand(ILogger<SectorExtractCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string outputPath, int sectorSize, long? start, long? end)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.outputPath = outputPath;
        this.sectorSize = sectorSize;
        this.start = start;
        this.end = end;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (sectorSize % 512 != 0)
        {
            return new Result(new Error("Sector size must be dividable by 512"));
        }
        
        OnInformationMessage($"Extracting sectors from '{path}' to '{outputPath}'");
        
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
            var startOffset = start.Value / sectorSize;
            stream.Seek(startOffset, SeekOrigin.Begin);
        }
        
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        stream.Position = 0;
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        await Amiga.Disk.FindUsedSectors(stream, sectorSize, async (offset, bytes) =>
        {
            if (end.HasValue && offset >= end.Value || cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                return;
            }
            
            var sectorPath = Path.Combine(outputPath, $"{offset}.bin");
            await File.WriteAllBytesAsync(sectorPath, bytes, cancellationTokenSource.Token);
        }, cancellationTokenSource.Token);
            
        return new Result();
    }
}