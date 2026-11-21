namespace Hst.Imager.Core.Medias
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Apis;
    using Commands;
    using DiscUtils;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Helpers;
    using Hst.Core;
    using MagicBytes;
    using Microsoft.Extensions.Logging;
    using Models;
    using PhysicalDrives;
    using SparseFiles;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class MediaManager : IMediaManager, INotification
    {
        private readonly ILogger logger;
        private readonly bool isAdministrator;
        private readonly bool createSparseFiles;
        private readonly bool useCache;
        private readonly CacheType cacheType;
        private readonly VhdMediaFactory vhdMediaFactory;
        private readonly IList<IPhysicalDrive> activePhysicalDrives;

        /// <summary>
        /// Active medias contains opened medias and is used to get reuse medias without opening same media twice
        /// </summary>
        private readonly IList<Media> activeMedias;

        public event EventHandler<string> DebugMessage;
        public event EventHandler<string> WarningMessage;
        public event EventHandler<string> InformationMessage;
        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public MediaManager(ILogger logger, IMediaPathResolver pathResolver, bool isAdministrator,
            bool createSparseFiles, bool useCache, CacheType cacheType)
        {
            this.logger = logger;
            PathResolver = pathResolver;
            this.isAdministrator = isAdministrator;
            this.createSparseFiles = createSparseFiles;
            this.useCache = useCache;
            this.cacheType = cacheType;
            this.vhdMediaFactory = new VhdMediaFactory(useCache, cacheType);
            this.activePhysicalDrives = new List<IPhysicalDrive>();
            this.activeMedias = new List<Media>();
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
        }

        /// <summary>
        /// Path resolver used to resolve media path and modifiers from paths.
        /// </summary>
        protected IMediaPathResolver PathResolver { get; }

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

        public virtual void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed,
            long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal,
                    timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }

        public virtual async Task<DataType> DetectDataType(string path)
        {
            var activeMedia = GetActiveMedia(path);
            if (activeMedia != null)
            {
                return await DetectDataType(activeMedia.Stream);
            }

            if (!File.Exists(path))
            {
                return DataType.Unknown;
            }

            await using var stream = File.OpenRead(path);

            return await DetectDataType(stream);
        }

        protected async Task<DataType> DetectDataType(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var data = new byte[65536];
            if (await stream.ReadAsync(data) == 0)
            {
                return DataType.Unknown;
            }

            MagicBytesRegister.Instance.TryResolve(data, out var dataType);

            return dataType;
        }

        public virtual bool IsVhdValid(string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
            {
                return false;
            }

            Stream stream = null;
            VirtualDisk disk;

            try
            {
                stream = File.OpenRead(path);
                disk = new Disk(stream, Ownership.Dispose);
            }
            catch (Exception)
            {
                stream?.Close();
                stream?.Dispose();
                return false;
            }

            disk.Dispose();
            return true;
        }

        /// <summary>
        /// Clear active medias to avoid source and destination being reused between commands
        /// </summary>
        public void ClearActiveMedias()
        {
            foreach (var activeMedia in this.activeMedias)
            {
                activeMedia.Dispose();

                DeleteLayerPath(activeMedia.Path);
            }

            this.activeMedias.Clear();
        }

        private static void DeleteLayerPath(string path)
        {
            var layerPath = PathHelper.GetLayerPath(path);

            // delete layer path, if file exists
            if (File.Exists(layerPath))
            {
                File.Delete(layerPath);
            }
        }

        public void ClearActiveMedia(string path)
        {
            var activeMedia = this.activeMedias.FirstOrDefault(x =>
                x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (activeMedia == null)
            {
                return;
            }

            activeMedia.Dispose();

            DeleteLayerPath(path);

            activeMedias.Remove(activeMedia);
        }

        private Media GetActiveMedia(string path)
        {
            var media = activeMedias.FirstOrDefault(x =>
                x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (media == null)
            {
                return null;
            }

            // remove active media and return null, if it's disposed.
            // this will open the media again.
            if (media.IsDisposed)
            {
                activeMedias.Remove(media);
                return null;
            }

            if (media is PhysicalDriveMedia physicalDriveMedia)
            {
                physicalDriveMedia.OpenStream();
                return physicalDriveMedia;
            }

            if (media.Stream == null)
            {
                media.SetStream(File.Open(path, FileMode.Open,
                    media.IsWriteable ? FileAccess.ReadWrite : FileAccess.Read));
            }

            return media;
        }

        public virtual async Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null)
        {
            if (modifiers == null)
            {
                var mediaResult = PathResolver.ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return new Result<Media>(mediaResult.Error);
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            logger.LogDebug($"Opening '{path}' as readable");

            var physicalDriveMediaResult = await GetPhysicalDriveMedia(physicalDrives, path, modifiers);

            if (physicalDriveMediaResult.IsSuccess && physicalDriveMediaResult.Value != null)
            {
                return physicalDriveMediaResult;
            }

            return await GetReadableFileMedia(path, modifiers);
        }

        public virtual Stream CreateWriteableStream(string path, bool create, long size)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            path = PathHelper.GetFullPath(path);
            if (create && File.Exists(path))
            {
                File.Delete(path);
            }

            if (create && createSparseFiles && OperatingSystem.IsWindows())
            {
                try
                {
                    SparseFile.CreateSparseFile(path, size);
                }
                catch (Exception e)
                {
                    OnDebugMessage($"Failed to create sparse file '{path}': {e.Message}");
                    OnWarningMessage($"Failed to create sparse file '{path}', fall back to regular file");
                }
            }

            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives,
            string path, ModifierEnum? modifiers = null, bool writeable = false)
        {
            if (modifiers == null)
            {
                var mediaResult = PathResolver.ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return Task.FromResult(new Result<Media>(mediaResult.Error));
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            var byteSwap = modifiers?.HasFlag(ModifierEnum.ByteSwap) ?? false;

            var physicalDrivePath = PathResolver.GetPhysicalDrivePath(path);
            if (string.IsNullOrEmpty(physicalDrivePath))
            {
                return Task.FromResult(new Result<Media>((Media)null));
            }

            var activeMedia = GetActiveMedia(physicalDrivePath);
            if (activeMedia != null)
            {
                return Task.FromResult(!isAdministrator
                    ? new Result<Media>(new Error($"Path '{path}' requires administrator privileges"))
                    : new Result<Media>(activeMedia));
            }

            var physicalDrive = GetPhysicalDrive(physicalDrives, path);

            if (physicalDrive == null)
            {
                return Task.FromResult(new Result<Media>(new Error($"Physical drive '{path}' not found")));
            }

            if (!isAdministrator)
            {
                return Task.FromResult(new Result<Media>(new Error($"Path '{path}' requires administrator privileges")));
            }

            physicalDrive.SetWritable(writeable);
            physicalDrive.SetByteSwap(byteSwap);
            var physicalDriveMedia = new PhysicalDriveMedia(physicalDrivePath, physicalDrive.Name,
                Media.MediaType.Raw, physicalDrive, byteSwap, useCache: useCache, cacheType: cacheType);

            // add physical drive to active drives if not already present
            if (activePhysicalDrives.All(x => x.Path != physicalDrivePath))
            {
                this.activePhysicalDrives.Add(physicalDrive);
            }

            this.activeMedias.Add(physicalDriveMedia);
            return Task.FromResult(new Result<Media>(physicalDriveMedia));
        }

        private IPhysicalDrive GetPhysicalDrive(IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            var activePhysicalDrive = activePhysicalDrives.FirstOrDefault(x =>
                    x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (activePhysicalDrive != null)
            {
                return activePhysicalDrive;
            }

            return physicalDrives.FirstOrDefault(x =>
                x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        public virtual async Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new Result<Media>(new Error("Path not defined"));
            }

            if (modifiers == null)
            {
                var mediaResult = PathResolver.ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return new Result<Media>(mediaResult.Error);
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            var byteSwap = modifiers?.HasFlag(ModifierEnum.ByteSwap) ?? false;

            path = PathHelper.GetFullPath(path);
            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path ?? "null"}' not found", nameof(path)));
            }

            var activeMedia = GetActiveMedia(path);
            if (activeMedia != null)
            {
                return new Result<Media>(activeMedia);
            }

            var isVhd = await DetectDataType(path) == DataType.Vhd;

            var name = Path.GetFileName(path);

            if (!isVhd)
            {
                var fileStream = File.Open(path, FileMode.Open, FileAccess.Read);
                var fileMedia = await GetFileMedia(path, name, fileStream, byteSwap);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            Media vhdMedia;

            try
            {
                vhdMedia = vhdMediaFactory.Open(path, name, false, byteSwap);
            }
            catch (Exception e)
            {
                return new Result<Media>(new Error($"Failed to open vhd media '{path}': {e}"));
            }

            activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        private async Task<Media> GetFileMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;

            // pc floppy image: 3,5" HD, 3.5" DD, 5.25" DD
            if (stream.Length == 1474560 || stream.Length == 737280 || stream.Length == 368640)
            {
                return new Media(path, name, Media.MediaType.Floppy, false,
                    stream, false);
            }

            var headerBytes = new byte[512];
            _ = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);

            MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType);

            // compressed media: rar, zip, xz or gzip
            var compressedMedia = await CompressedMediaReader.Resolve(path, name, stream, dataType, byteSwap);
            if (compressedMedia != null)
            {
                return compressedMedia;
            }

            // vhd media
            if (dataType == DataType.Vhd)
            {
                return vhdMediaFactory.Open(path, name, stream, false, byteSwap);
            }

            // raw stream
            stream.Position = 0;
            var baseStream = byteSwap
                ? new SectorStream(stream, byteSwap: true, leaveOpen: false)
                : stream;
            return new Media(path, name, Media.MediaType.Raw, false, baseStream, byteSwap);
        }

        public virtual async Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null,
            long? size = null, bool create = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(path);
            }

            if (modifiers == null)
            {
                var modifiersResult = PathResolver.ResolveModifiers(path);
                if (modifiersResult.HasModifiers)
                {
                    path = modifiersResult.Path;
                    modifiers = modifiersResult.Modifiers;
                }
            }

            var byteSwap = modifiers?.HasFlag(ModifierEnum.ByteSwap) ?? false;

            path = PathHelper.GetFullPath(path);

            var media = GetActiveMedia(path);
            if (media != null)
            {
                return new Result<Media>(media);
            }

            var destDir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var isVhd = MediaHelper.IsVhd(path);
            var name = Path.GetFileName(path);

            if (create)
            {
                if (!isVhd)
                {
                    var fileStream = CreateWriteableStream(path, true, size ?? 0);
                    var fileMedia = CompressedMediaWriter.Create(path, name, size ?? 0, fileStream, byteSwap);
                    activeMedias.Add(fileMedia);
                    return new Result<Media>(fileMedia);
                }

                if (size == null || size.Value == 0)
                {
                    throw new ArgumentException("Size is required for creating VHD image file", nameof(size));
                }

                using var vhdStream = CreateWriteableStream(path, true, 0);
                using var newVhdDisk = Disk.InitializeDynamic(vhdStream, Ownership.None,
                    MediaHelper.GetVhdSize(size.Value));
            }

            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path}' not found", nameof(path)));
            }

            if (isVhd && !IsVhdValid(path))
            {
                return new Result<Media>(new Error($"VHD file '{path}' is not valid"));
            }

            if (await DetectDataType(path) != DataType.Vhd)
            {
                var fileStream = CreateWriteableStream(path, false, size ?? 0);
                var baseStream = byteSwap
                    ? new SectorStream(fileStream, leaveOpen: false, byteSwap: true)
                    : fileStream;
                var fileMedia = new Media(path, name, Media.MediaType.Raw, false, baseStream, byteSwap);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            Media vhdMedia;

            try
            {
                vhdMedia = vhdMediaFactory.Open(path, name, true, byteSwap);
            }
            catch (Exception e)
            {
                return new Result<Media>(new Error($"Failed to open vhd media '{path}': {e}"));
            }

            activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        public virtual async Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, long? size = null, bool create = false)
        {
            if (!create && modifiers == null)
            {
                var mediaResult = PathResolver.ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return new Result<Media>(mediaResult.Error);
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            logger.LogDebug($"Opening '{path}' as writable");

            var physicalDriveMediaResult = await GetPhysicalDriveMedia(physicalDrives, path, modifiers, true);

            if (physicalDriveMediaResult.IsSuccess && physicalDriveMediaResult.Value != null)
            {
                return physicalDriveMediaResult;
            }

            return await GetWritableFileMedia(path, modifiers, size, create);
        }

        /// <summary>
        /// Create a new sparse file media, where unwritten areas don't allocate disk space.
        /// Vhd files are created as dynamic vhd. Other files are created as sparse files, if supported by
        /// the file system, otherwise as regular files.
        /// </summary>
        /// <param name="path">Path to file to create. Existing file is overwritten.</param>
        /// <param name="size">Size of file.</param>
        public virtual async Task<Result<Media>> CreateSparseFileMedia(string path, long size)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (MediaHelper.IsVhd(path))
            {
                return await GetWritableFileMedia(path, size: size, create: true);
            }

            path = PathHelper.GetFullPath(path);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            // windows requires sparse attribute set on file. linux and macos file systems
            // creates sparse files by default when extending file size with set length
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    SparseFile.CreateSparseFile(path, size);
                }
                catch (Exception e)
                {
                    OnDebugMessage($"Failed to create sparse file '{path}': {e.Message}");
                    OnWarningMessage($"Failed to create sparse file '{path}', fall back to regular file");
                }
            }

            var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            stream.SetLength(size);

            var media = new Media(path, Path.GetFileName(path), Media.MediaType.Raw, false, stream, false);
            activeMedias.Add(media);
            return new Result<Media>(media);
        }

        public void ClearActivePhysicalDrives()
        {
            foreach (var activePhysicalDrive in activePhysicalDrives)
            {
                activePhysicalDrive.Dispose();

                DeleteLayerPath(activePhysicalDrive.Path);
            }

            activePhysicalDrives.Clear();
        }

        public virtual Task RescanPhysicalDrives()
        {
            if (OperatingSystem.IsWindows())
            {
                var disksUpdated = true;

                foreach (var windowsPhysicalDrive in activePhysicalDrives.OfType<WindowsPhysicalDrive>())
                {
                    // update disk properties, so windows reads partition table written to physical drive
                    if (!windowsPhysicalDrive.UpdateDiskProperties())
                    {
                        disksUpdated = false;
                    }

                    // remount volumes, so windows detects file systems written to physical drive
                    windowsPhysicalDrive.RemountVolumes();
                }

                // fallback to rescan drives with diskpart, if updating disk properties failed
                if (!disksUpdated)
                {
                    WindowsDiskManager.RescanDrives();
                }
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            ClearActiveMedias();
            ClearActivePhysicalDrives();
        }
    }
}
