using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Imager.Core.Volumes;

namespace Hst.Imager.Core.Commands.FsCommands;

public class FsMkDirCommand(
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives, string path)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Creating directory path: '{path}'");

        using var appCache = new MemoryAppCache();
        using var volumeFactory = CreateVolumeFactory(new UaeMetadataHelper(appCache), UaeMetadata.None);

        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Write);
        if (volumePathResult.IsFaulted)
        {
            return new Result(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        // create directories for each path component, which doesn't exist
        for (var i = 1; i <= pathComponents.Length; i++)
        {
            var dirPathComponents = pathComponents.Take(i).ToArray();

            var entryResult = await volume.GetEntry(dirPathComponents);
            if (entryResult.IsSuccess)
            {
                if (entryResult.Value.IsDir())
                {
                    continue;
                }

                return new Result(new PathExistsError(
                    $"Path '{volume.MediaPath.Join(dirPathComponents)}' already exists as a file"));
            }

            var createDirectoryResult = await volume.CreateDirectory(dirPathComponents);
            if (createDirectoryResult.IsFaulted)
            {
                return createDirectoryResult;
            }
        }

        await volume.Flush();

        return new Result();
    }
}
