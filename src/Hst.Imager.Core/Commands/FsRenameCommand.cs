using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands;

public class FsRenameCommand(
    ILogger<FsRenameCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string fromPath,
    string toPath,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    private readonly ILogger<FsRenameCommand> logger = logger;
    public override Task<Result> Execute(CancellationToken token)
    {
        throw new NotImplementedException();
    }
}