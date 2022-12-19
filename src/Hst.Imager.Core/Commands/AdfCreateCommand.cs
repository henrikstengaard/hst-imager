namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amiga;
using Amiga.Extensions;
using Amiga.FileSystems.FastFileSystem;
using Amiga.RigidDiskBlocks;
using Hst.Core;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging;
using File = System.IO.File;

public class AdfCreateCommand : CommandBase
{
    private readonly ILogger<AdfCreateCommand> logger;
    private readonly string adfPath;
    private readonly bool format;
    private readonly string name;
    private readonly string dosType;
    private readonly bool bootable;

    public AdfCreateCommand(ILogger<AdfCreateCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string adfPath, bool format, string name, string dosType,
        bool bootable)
    {
        this.logger = logger;
        this.adfPath = adfPath;
        this.format = format;
        this.name = name;
        this.dosType = string.IsNullOrWhiteSpace(dosType) ? "DOS3" : dosType;
        this.bootable = bootable;
    }

    private static readonly Regex
        DosTypeRegex = new Regex("^DOS[0-7]$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Creating blank ADF file at '{adfPath}'");
        
        await using var adfStream = File.Open(adfPath, FileMode.Create, FileAccess.ReadWrite);

        adfStream.SetLength(FloppyDiskConstants.DoubleDensity.Size);

        if (!format)
        {
            return new Result();
        }

        OnInformationMessage("Formatting ADF");
        
        if (!DosTypeRegex.IsMatch(dosType))
        {
            return new Result(new Error($"Unsupported DOS type '{dosType}'. Only Fast File System DOS types DOS1-7 are supported"));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return new Result(new Error("Name required for formatting"));
        }

        var dosTypeBytes = DosTypeHelper.FormatDosType(dosType);

        OnInformationMessage(
            $"- DOS type '0x{dosTypeBytes.FormatHex()}' ({dosTypeBytes.FormatDosType()})");
        OnInformationMessage($"- Volume name '{name}'");
        
        await FastFileSystemFormatter.Format(adfStream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize,
            FloppyDiskConstants.BlockSize, dosTypeBytes, name);

        if (bootable)
        {
            OnInformationMessage($"- Bootable");
            adfStream.Seek(4, SeekOrigin.Begin);
            await adfStream.WriteAsync(FloppyDiskConstants.BootableBootBlockBytes, 0,
                FloppyDiskConstants.BootableBootBlockBytes.Length);
        }
        
        return new Result();
    }
}