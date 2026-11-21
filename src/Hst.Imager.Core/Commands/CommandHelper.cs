namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Helpers;
    using Hst.Core;
    using MagicBytes;
    using Medias;
    using Microsoft.Extensions.Logging;
    using Models;

    /// <summary>
    /// Command helper is a facade for commands to open medias, resolve paths and read disk info.
    /// Responsibilities are implemented in media manager, media path resolver and disk info reader.
    /// </summary>
    public class CommandHelper : ICommandHelper, INotification
    {
        public event EventHandler<string> DebugMessage;
        public event EventHandler<string> WarningMessage;
        public event EventHandler<string> InformationMessage;
        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public IMediaManager MediaManager { get; }
        public IMediaPathResolver PathResolver { get; }
        public IDiskInfoReader DiskInfoReader { get; }

        public CommandHelper(IMediaManager mediaManager, IMediaPathResolver pathResolver,
            IDiskInfoReader diskInfoReader)
        {
            MediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
            PathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            DiskInfoReader = diskInfoReader ?? throw new ArgumentNullException(nameof(diskInfoReader));

            // forward notifications from media manager
            if (mediaManager is INotification notification)
            {
                notification.DebugMessage += (_, message) => OnDebugMessage(message);
                notification.WarningMessage += (_, message) => OnWarningMessage(message);
                notification.InformationMessage += (_, message) => OnInformationMessage(message);
                notification.DataProcessed += (_, args) => DataProcessed?.Invoke(this, args);
            }
        }

        public CommandHelper(ILogger<ICommandHelper> logger, bool isAdministrator, bool createSparseFiles,
            bool useCache, CacheType cacheType)
            : this(new MediaPathResolver(logger), logger, isAdministrator, createSparseFiles, useCache, cacheType)
        {
        }

        public CommandHelper(ILogger<ICommandHelper> logger, bool isAdministrator)
            : this(logger, isAdministrator, false, false, cacheType: CacheType.Disk)
        {
        }

        private CommandHelper(IMediaPathResolver pathResolver, ILogger logger, bool isAdministrator,
            bool createSparseFiles, bool useCache, CacheType cacheType)
            : this(new MediaManager(logger, pathResolver, isAdministrator, createSparseFiles, useCache, cacheType),
                pathResolver, new DiskInfoReader())
        {
        }

        public void OnDebugMessage(string message)
        {
            DebugMessage?.Invoke(this, message);
        }

        public void OnWarningMessage(string message)
        {
            WarningMessage?.Invoke(this, message);
        }

        public void OnInformationMessage(string message)
        {
            InformationMessage?.Invoke(this, message);
        }

        public void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed,
            long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal,
                    timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }

        public Task<DataType> DetectDataType(string path) => MediaManager.DetectDataType(path);

        public void ClearActiveMedias() => MediaManager.ClearActiveMedias();

        public void ClearActiveMedia(string path) => MediaManager.ClearActiveMedia(path);

        public void ClearActivePhysicalDrives() => MediaManager.ClearActivePhysicalDrives();

        public Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, bool writeable = false) =>
            MediaManager.GetPhysicalDriveMedia(physicalDrives, path, modifiers, writeable);

        public Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null) =>
            MediaManager.GetReadableFileMedia(path, modifiers);

        public Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null,
            long? size = null, bool create = false) =>
            MediaManager.GetWritableFileMedia(path, modifiers, size, create);

        public Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null) =>
            MediaManager.GetReadableMedia(physicalDrives, path, modifiers);

        public Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, long? size = null, bool create = false) =>
            MediaManager.GetWritableMedia(physicalDrives, path, modifiers, size, create);

        public Task<Result<Media>> CreateSparseFileMedia(string path, long size) =>
            MediaManager.CreateSparseFileMedia(path, size);

        public Task RescanPhysicalDrives() => MediaManager.RescanPhysicalDrives();

        public long GetVhdSize(long size) => MediaHelper.GetVhdSize(size);

        public bool IsZip(string path) => MediaHelper.IsZip(path);

        public bool IsGZip(string path) => MediaHelper.IsGZip(path);

        public Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None) =>
            DiskInfoReader.ReadDiskInfo(media, partitionTableTypeContext);

        public Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false) =>
            PathResolver.ResolveMedia(path, allowNonExisting);

        public ModifierResult ResolveModifiers(string path) => PathResolver.ResolveModifiers(path);

        public void Dispose()
        {
            MediaManager.Dispose();
        }
    }
}
