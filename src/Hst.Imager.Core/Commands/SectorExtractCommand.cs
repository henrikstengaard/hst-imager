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

    public SectorExtractCommand(ILogger<SectorExtractCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string outputPath)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.outputPath = outputPath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnProgressMessage($"Opening '{path}' for read");

        var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path, allowPhysicalDrive: true);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        await using var stream = media.Stream;

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        stream.Position = 0;
        await Amiga.Disk.FindUsedSectors(stream, 512, async (offset, bytes) =>
        {
            OnProgressMessage($"Writing sector offset '{offset}'");
            var sectorPath = Path.Combine(outputPath, $"{offset}.bin");
            await File.WriteAllBytesAsync(sectorPath, bytes, token);
        });
            
        return new Result();
    }
}