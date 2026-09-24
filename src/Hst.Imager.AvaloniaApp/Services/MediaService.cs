using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.AvaloniaApp.Services;

public class MediaService : IMediaService
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AppStateModel _appState;

    public MediaService(ILoggerFactory loggerFactory, AppStateModel appState)
    {
        _loggerFactory = loggerFactory;
        _appState = appState;
    }

    public async Task<IEnumerable<MediaInfo>> ListMediaAsync(CancellationToken cancellationToken = default)
    {
        var physicalDriveManager = CreatePhysicalDriveManager();
        var physicalDrives = (await physicalDriveManager.GetPhysicalDrives(_appState.Settings.AllPhysicalDrives)).ToList();

        using var commandHelper = CreateCommandHelper();
        var listCommand = new ListCommand(_loggerFactory.CreateLogger<ListCommand>(), commandHelper, physicalDrives);

        IEnumerable<MediaInfo> result = [];
        listCommand.ListRead += (_, args) =>
        {
            result = args.MediaInfos.Where(x => !x.SystemDrive).ToList();
        };

        await listCommand.Execute(cancellationToken);
        return result;
    }

    public async Task<MediaInfo?> GetMediaInfoAsync(string path, bool byteswap = false, bool allowNonExisting = false, CancellationToken cancellationToken = default)
    {
        var physicalDriveManager = CreatePhysicalDriveManager();
        var physicalDrives = (await physicalDriveManager.GetPhysicalDrives(_appState.Settings.AllPhysicalDrives)).ToList();

        using var commandHelper = CreateCommandHelper();
        var infoPath = string.Concat(byteswap ? "+bs:" : string.Empty, path);
        var infoCommand = new InfoCommand(_loggerFactory.CreateLogger<InfoCommand>(), commandHelper, physicalDrives, infoPath, allowNonExisting);

        MediaInfo? result = null;
        infoCommand.DiskInfoRead += (_, args) =>
        {
            result = args.MediaInfo;
        };

        await infoCommand.Execute(cancellationToken);
        return result;
    }

    private IPhysicalDriveManager CreatePhysicalDriveManager()
    {
        if (Hst.Core.OperatingSystem.IsWindows())
            return new WindowsPhysicalDriveManager(_loggerFactory.CreateLogger<WindowsPhysicalDriveManager>());
        if (Hst.Core.OperatingSystem.IsMacOs())
            return new MacOsPhysicalDriveManager(_loggerFactory.CreateLogger<MacOsPhysicalDriveManager>());
        if (Hst.Core.OperatingSystem.IsLinux())
            return new LinuxPhysicalDriveManager(_loggerFactory.CreateLogger<LinuxPhysicalDriveManager>());
        return new StaticPhysicalDriveManager([]);
    }

    private CommandHelper CreateCommandHelper() =>
        new(_loggerFactory.CreateLogger<ICommandHelper>(),
            _appState.IsAdministrator,
            _appState.Settings.SparseFiles,
            _appState.Settings.UseCache,
            _appState.Settings.CacheType);
}
