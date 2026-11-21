using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Imager.Core.Volumes;

namespace Hst.Imager.Core.Commands.FsCommands;

public class FsMkLinkCommand(ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives, string fromPath, string toPath)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Creating link from path '{fromPath}' to path '{toPath}'");

        using var appCache = new MemoryAppCache();
        using var volumeFactory = CreateVolumeFactory(new UaeMetadataHelper(appCache), UaeMetadata.None);

        var volumePathResult = await volumeFactory.Open(fromPath, VolumeAccess.Write);
        if (volumePathResult.IsFaulted)
        {
            return new Result(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        var createLinkResult = await volume.CreateLink(pathComponents, toPath);
        if (createLinkResult.IsFaulted)
        {
            return createLinkResult;
        }

        await volume.Flush();

        return new Result();
    }
}
