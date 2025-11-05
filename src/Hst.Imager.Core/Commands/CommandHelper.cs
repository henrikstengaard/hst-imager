using System.IO.Compression;
using Hst.Imager.Core.Apis;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.PartitionTables;
using SharpCompress.Archives.Rar;
using GZipStream = SharpCompress.Compressors.Deflate.GZipStream;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DiscUtils;
    using DiscUtils.Partitions;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Amiga.RigidDiskBlocks;
    using Helpers;
    using Hst.Core;
    using Models;
    using OperatingSystem = Hst.Core.OperatingSystem;
    using Microsoft.Extensions.Logging;

    public class CommandHelper : ICommandHelper
    {
        private readonly ILogger<ICommandHelper> logger;
        private readonly bool isAdministrator;

        private readonly IList<IPhysicalDrive> activePhysicalDrives;
        
        /// <summary>
        /// Active medias contains opened medias and is used to get reuse medias without opening same media twice
        /// </summary>
        private readonly IList<Media> activeMedias;

        public CommandHelper(ILogger<ICommandHelper> logger, bool isAdministrator)
        {
            this.logger = logger;
            this.isAdministrator = isAdministrator;
            this.activePhysicalDrives = new List<IPhysicalDrive>();
            this.activeMedias = new List<Media>();
            DiscUtils.Containers.SetupHelper.SetupContainers();
            DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
        }

        /// <summary>
        /// Clear active medias to avoid source and destination being reused between commands
        /// </summary>
        public void ClearActiveMedias()
        {
            foreach (var activeMedia in this.activeMedias)
            {
                activeMedia.Dispose();
            }

            this.activeMedias.Clear();
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
            activeMedias.Remove(activeMedia);
        }
        
        private Media GetActiveMedia(string path)
        {
            var media = this.activeMedias.FirstOrDefault(x => 
                x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (media == null)
            {
                return null;
            }

            if (media is DiskMedia diskMedia && diskMedia.Type == Media.MediaType.Vhd)
            {
                if (diskMedia.IsDisposed)
                {
                    var vhdDisk =
                        VirtualDisk.OpenDisk(path, media.IsWriteable ? FileAccess.ReadWrite : FileAccess.Read);
                    vhdDisk.Content.Position = 0;
                    diskMedia.SetDisk(vhdDisk);
                }

                return diskMedia;
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
                var mediaResult = ResolveMedia(path);
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

        public virtual Stream CreateWriteableStream(string path, bool create)
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

            return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public virtual Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives,
            string path, ModifierEnum? modifiers = null, bool writeable = false)
        {
            if (modifiers == null)
            {
                var mediaResult = ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return Task.FromResult(new Result<Media>(mediaResult.Error));
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            var byteSwap = modifiers?.HasFlag(ModifierEnum.ByteSwap) ?? false;

            var physicalDrivePath = GetPhysicalDrivePath(path);
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
            var physicalDriveMedia = new PhysicalDriveMedia(physicalDrivePath, physicalDrive.Name, physicalDrive.Size,
                Media.MediaType.Raw, true, physicalDrive, byteSwap);

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

        private static string GetPhysicalDrivePath(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                var diskPathMatch = Regexs.DiskPathRegex.Match(path);
                if (diskPathMatch.Success)
                {
                    return $"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}";
                }

                return Regexs.PhysicalDrivePathRegex.IsMatch(path) ? path : null;
            }

            if (OperatingSystem.IsMacOs() || OperatingSystem.IsLinux())
            {
                return Regexs.DevicePathRegex.IsMatch(path) ? path : null;
            }

            return null;
        }

        public virtual async Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new Result<Media>(new Error("Path not defined"));
            }

            if (modifiers == null)
            {
                var mediaResult = ResolveMedia(path);
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

            var name = Path.GetFileName(path);

            if (!IsVhd(path))
            {
                var fileMedia =await GetFileMedia(path, name, false, (ModifierEnum)modifiers);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            Media vhdMedia;
            
            try
            {
                vhdMedia = GetVhdMedia(path, name, false, byteSwap);
            }
            catch (Exception e)
            {
                return new Result<Media>(new Error($"Failed to open vhd media '{path}': {e}"));
            }

            activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        private async Task<Media> GetFileMedia(string path, string name, bool writeable, ModifierEnum modifiers)
        {
            var fileStream = File.Open(path, FileMode.Open, FileAccess.Read);
            return await ResolveFileMedia(path, name, fileStream, modifiers);
        }

        private static Media GetVhdMedia(string path, string name, bool writeable, bool byteSwap)
        {
            var vhdDisk = VirtualDisk.OpenDisk(path, writeable ? FileAccess.ReadWrite : FileAccess.Read);
            vhdDisk.Content.Position = 0;

            // sector stream is only used byteswapping disk media
            return new DiskMedia(path, name, vhdDisk.Capacity, Media.MediaType.Vhd, false, vhdDisk,
                byteSwap, byteSwap ? new SectorStream(vhdDisk.Content, byteSwap: true, leaveOpen: true) : vhdDisk.Content);
        }

        private async Task<Media> ResolveFileMedia(string path, string name, Stream stream, ModifierEnum modifiers)
        {
            var byteSwap = modifiers.HasFlag(ModifierEnum.ByteSwap);

            stream.Position = 0;
            
            // floppy image
            if (stream.Length == 1474560)
            {
                return new Media(path, name, stream.Length, Media.MediaType.Floppy, false,
                    stream, false);
            }
            
            var headerBytes = new byte[512];
            var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);

            // rar media
            var rarMedia = headerBytes.HasMagicNumber(MagicBytes.RarMagicNumber150) ||
                           headerBytes.HasMagicNumber(MagicBytes.RarMagicNumber500)
                ? await ResolveRarMedia(path, name, stream, byteSwap)
                : null;
            if (rarMedia != null)
            {
                return rarMedia;
            }

            // zip media
            var zipMedia = headerBytes.HasMagicNumber(MagicBytes.ZipMagicNumber1) ||
                           headerBytes.HasMagicNumber(MagicBytes.ZipMagicNumber2) ||
                           headerBytes.HasMagicNumber(MagicBytes.ZipMagicNumber3)
                ? await ResolveZipMedia(path, name, stream, byteSwap)
                : null;
            if (zipMedia != null)
            {
                return zipMedia;
            }

            // zx media
            var zxMedia = headerBytes.HasMagicNumber(MagicBytes.ZxHeader)
                ? await ResolveZxMedia(path, name, stream, byteSwap)
                : null;
            if (zxMedia != null)
            {
                return zxMedia;
            }

            // gzip stream
            var gzMedia = headerBytes.HasMagicNumber(MagicBytes.GzHeader)
                ? await ResolveGzMedia(path, name, stream, byteSwap)
                : null;
            if (gzMedia != null)
            {
                return gzMedia;
            }

            // raw stream
            // sector stream is only used for byte swapping disk media
            stream.Position = 0;
            return new Media(path, name, stream.Length, Media.MediaType.Raw, false,
                byteSwap ? new SectorStream(stream, byteSwap: true, leaveOpen: false) : stream, byteSwap);
        }

        private async Task<Media> ResolveRarMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var rarArchive = RarArchive.Open(stream);
            var rarEntry =
                rarArchive.Entries.FirstOrDefault(x =>
                    x.Key.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ||
                    x.Key.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase));
            if (rarEntry == null)
            {
                return null;
            }
            
            var headerBytes = new byte[512];
            stream.Position = 0;
            Stream rarEntryStream;
            await using (rarEntryStream = rarEntry.OpenEntryStream())
            {
                if (await rarEntryStream.ReadAsync(headerBytes, 0, headerBytes.Length) == 0)
                {
                    return null;
                }
            }

            rarEntryStream = rarEntry.OpenEntryStream();
            return new Media(path, Path.GetFileName(rarEntry.Key), rarEntry.Size,
                MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, headerBytes, 0)
                    ? Media.MediaType.CompressedVhd
                    : Media.MediaType.CompressedRaw, false, new InterceptorStream(
                    new SectorStream(rarEntryStream, leaveOpen: true, byteSwap: byteSwap), length: rarEntry.Size,
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        rarEntryStream.Fill(buffer, offset, count)), byteSwap);
        }
        
        private async Task<Media> ResolveZipMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var zipArchive = new ZipArchive(stream);
            var zipEntry =
                zipArchive.Entries.FirstOrDefault(x =>
                    x.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ||
                    x.Name.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase));
            if (zipEntry == null)
            {
                return null;
            }

            var headerBytes = new byte[512];
            stream.Position = 0;
            Stream zipEntryStream;
            await using (zipEntryStream = zipEntry.Open())
            {
                if (await zipEntryStream.ReadAsync(headerBytes, 0, headerBytes.Length) == 0)
                {
                    return null;
                }
            }

            zipEntryStream = zipEntry.Open();
            return new Media(path, Path.GetFileName(zipEntry.Name), zipEntry.Length,
                MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, headerBytes, 0)
                    ? Media.MediaType.CompressedVhd
                    : Media.MediaType.CompressedRaw, false, new InterceptorStream(
                    new SectorStream(zipEntryStream, leaveOpen: true, byteSwap: byteSwap), length: zipEntry.Length,
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        zipEntryStream.Fill(buffer, offset, count)), byteSwap);
        }

        private async Task<Media> ResolveZxMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var sizeAndHeader = await GetStreamLength(new SharpCompress.Compressors.Xz.XZStream(stream));

            stream.Position = 0;
            var zxStream = new SharpCompress.Compressors.Xz.XZStream(stream);
            return new Media(path, name, sizeAndHeader.Item1,
                MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, sizeAndHeader.Item2, 0)
                    ? Media.MediaType.CompressedVhd
                    : Media.MediaType.CompressedRaw, false, new InterceptorStream(
                    new SectorStream(zxStream, leaveOpen: true, byteSwap: byteSwap), length: sizeAndHeader.Item1, 
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        zxStream.Fill(buffer, offset, count)), byteSwap);
        }

        private async Task<Media> ResolveGzMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var sizeAndHeader =
                await GetStreamLength(new System.IO.Compression.GZipStream(stream, CompressionMode.Decompress));

            stream.Position = 0;
            var gZipStream = new System.IO.Compression.GZipStream(stream, CompressionMode.Decompress);
            return new Media(path, name, sizeAndHeader.Item1,
                MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, sizeAndHeader.Item2, 0)
                    ? Media.MediaType.CompressedVhd
                    : Media.MediaType.CompressedRaw, false,
                new InterceptorStream(
                    new SectorStream(gZipStream, leaveOpen: true, byteSwap: byteSwap), length: sizeAndHeader.Item1, 
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        gZipStream.Fill(buffer, offset, count)), byteSwap);
        }

        private async Task<Tuple<long, byte[]>> GetStreamLength(Stream stream)
        {
            var headerBytes = new byte[512];

            var size = 0L;
            var bytesRead = 0;
            var buffer = new byte[1024 * 1024];
            var count = 0;

            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                size += bytesRead;

                if (count == 0)
                {
                    Array.Copy(buffer, 0, headerBytes, 0, headerBytes.Length);
                }

                count++;
            } while (bytesRead > 0);

            return new Tuple<long, byte[]>(size, headerBytes);
        }

        public virtual Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null, long? size = null, bool create = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(path);
            }

            if (modifiers == null)
            {
                var modifiersResult = ResolveModifiers(path);
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
                return Task.FromResult(new Result<Media>(media));
            }

            var destDir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var name = Path.GetFileName(path);

            if (create)
            {
                if (!IsVhd(path))
                {
                    var fileStream = CreateWriteableStream(path, true);
                    var fileMedia = ResolveWritableFileMedia(path, name, fileStream, byteSwap);
                    if (fileMedia.Type is Media.MediaType.Raw or Media.MediaType.Floppy)
                    {
                        fileStream.SetLength(size ?? 0);
                    }
                    activeMedias.Add(fileMedia);
                    return Task.FromResult(new Result<Media>(fileMedia));
                }

                if (size == null || size.Value == 0)
                {
                    throw new ArgumentException("Size is required for creating VHD image file", nameof(size));
                }

                using var vhdStream = CreateWriteableStream(path, true);
                using var newVhdDisk = Disk.InitializeDynamic(vhdStream, Ownership.None, GetVhdSize(size.Value));
            }

            if (!File.Exists(path))
            {
                return Task.FromResult(
                    new Result<Media>(new PathNotFoundError($"Path '{path}' not found", nameof(path))));
            }

            if (!IsVhd(path))
            {
                var fileStream = CreateWriteableStream(path, false);
                var fileMedia = new Media(path, name, fileStream.Length, Media.MediaType.Raw, false,
                    new SectorStream(fileStream, leaveOpen: false, byteSwap: byteSwap), byteSwap);
                this.activeMedias.Add(fileMedia);
                return Task.FromResult(new Result<Media>(fileMedia));
            }

            Media vhdMedia;
            
            try
            {
                vhdMedia = GetVhdMedia(path, name, true, byteSwap);
            }
            catch (Exception e)
            {
                return Task.FromResult(new Result<Media>(new Error($"Failed to open vhd media '{path}': {e}")));
            }
            
            activeMedias.Add(vhdMedia);
            return Task.FromResult(new Result<Media>(vhdMedia));
        }

        private Media ResolveWritableFileMedia(string path, string name, Stream stream, bool byteSwap)
        {
            if (IsZip(path))
            {
                var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
                var filename = Path.GetFileNameWithoutExtension(path);
                if (!filename.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    filename += ".img";
                }
                var zipEntry = zipArchive.CreateEntry(filename);
                var zipEntryStream = zipEntry.Open();

                // create interceptor stream disabling seek, set length and overriding close to dispose zip archive
                var interceptorStream = new InterceptorStream(zipEntryStream, seekHandler: (l, _) => l, 
                    setLengthHandler: _ => { }, closeHandler: () => zipArchive.Dispose());
                return new Media(path, name, stream.Length, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            if (IsGZip(path))
            {
                var gZipStream = new GZipStream(stream, SharpCompress.Compressors.CompressionMode.Compress);

                // create interceptor stream disabling seek and set length
                var interceptorStream = new InterceptorStream(gZipStream, seekHandler: (l, _) => l, 
                    setLengthHandler: _ => { });
                return new Media(path, name, stream.Length, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            // sector stream has leave open set to false, so stream is disposed when media is closed
            return new Media(path, name, stream.Length, Media.MediaType.Raw, false, 
                byteSwap ? new SectorStream(stream, leaveOpen: false, byteSwap: true) : stream, byteSwap);
        }

        public virtual async Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, long? size = null, bool create = false)
        {
            if (!create && modifiers == null)
            {
                var mediaResult = ResolveMedia(path);
                if (mediaResult.IsFaulted)
                {
                    return new Result<Media>(mediaResult.Error);
                }

                path = mediaResult.Value.MediaPath;
                modifiers = mediaResult.Value.Modifiers;
            }

            logger.LogDebug($"Opening '{path}' as readable");

            var physicalDriveMediaResult = await GetPhysicalDriveMedia(physicalDrives, path, modifiers, true);

            if (physicalDriveMediaResult.IsSuccess && physicalDriveMediaResult.Value != null)
            {
                return physicalDriveMediaResult;
            }

            return await GetWritableFileMedia(path, modifiers, size, create);
        }

        public virtual long GetVhdSize(long size)
        {
            // increase size to next sector size, if not dividable by 512
            return size % 512 != 0 ? size + (512 - size % 512) : size;
        }

        public bool IsZip(string path)
        {
            return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsGZip(string path)
        {
            return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsVhd(string path)
        {
            return path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<VirtualDisk> ResolveVirtualDisk(Media media)
        {
            if (media.Type == Media.MediaType.Raw)
            {
                return new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            }

            if (media is DiskMedia diskMedia)
            {
                return diskMedia.Disk;
            }

            if (media.Type != Media.MediaType.CompressedRaw && media.Type != Media.MediaType.CompressedVhd)
            {
                throw new NotSupportedException($"Unable to resolve disk media type '{media.Type}'");
            }

            // read first chunk from compressed stream
            var firstChunk = new byte[1024 * 1024];
            await media.Stream.FillAsync(firstChunk, 0, firstChunk.Length);

            // return compressed vhd, if vhd magic number is present in first chunk
            return MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, firstChunk, 0)
                ? CompressedVhd(media.Stream, firstChunk)
                : CompressedImg(media.Stream, firstChunk);
        }

        private VirtualDisk CompressedVhd(Stream stream, byte[] firstChunk) => 
            new Disk(new MemoryStream(firstChunk), Ownership.None);

        private VirtualDisk CompressedImg(Stream stream, byte[] firstChunk) => 
            new DiscUtils.Raw.Disk(new MemoryStream(firstChunk), Ownership.None);

        public virtual async Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None)
        {
            var partitionTables = new List<PartitionTableInfo>();

            var disk = await ResolveVirtualDisk(media);

            PartitionTableInfo mbrPartitionTableInfo = null;
            
            var biosPartitionTable = MbrPartitionTableReader.Read(disk);
            if (biosPartitionTable != null)
            {
                mbrPartitionTableInfo = await MbrPartitionTableReader.Read(disk, biosPartitionTable);
                partitionTables.Add(mbrPartitionTableInfo);
            }

            PartitionTableInfo guidPartitionTableInfo = null;
            
            var guidPartitionTable = GuidPartitionTableReader.Read(disk);
            if (guidPartitionTable != null)
            {
                guidPartitionTableInfo = await GuidPartitionTableReader.Read(disk, guidPartitionTable);
                partitionTables.Add(guidPartitionTableInfo);
            }

            PartitionTableInfo rdbPartitionTableInfo = null;

            RigidDiskBlock rigidDiskBlock = null;
            try
            {
                disk.Content.Position = 0;
                rigidDiskBlock = await RigidDiskBlockReader.Read(disk.Content);
                if (rigidDiskBlock != null)
                {
                    rdbPartitionTableInfo = PartitionTables.RigidDiskBlockReader.Read(disk, rigidDiskBlock);
                    partitionTables.Add(rdbPartitionTableInfo);
                }
            }
            catch (Exception)
            {
                // ignored, if read rigid disk block fails
            }

            var diskInfo = new DiskInfo
            {
                Path = media.Path,
                Name = media.Model,
                Size = GetDiskSize(media, disk),
                PartitionTables = partitionTables,
                StartOffset = 0,
                EndOffset = media.Size > 0 ? media.Size - 1 : 0,
                RigidDiskBlock = rigidDiskBlock,
            };

            diskInfo.GptPartitionTablePart = CreatePartitionTablePart(diskInfo, guidPartitionTableInfo,
                partitionTableTypeContext);
            diskInfo.MbrPartitionTablePart = CreatePartitionTablePart(diskInfo, mbrPartitionTableInfo,
                partitionTableTypeContext);
            diskInfo.RdbPartitionTablePart = CreatePartitionTablePart(diskInfo, rdbPartitionTableInfo,
                partitionTableTypeContext);
            diskInfo.DiskParts = CreateDiskParts(diskInfo, partitionTableTypeContext).ToList();

            return diskInfo;
        }

        private static long GetDiskSize(Media media, VirtualDisk disk)
        {
            return media.Type switch
            {
                Media.MediaType.CompressedVhd => disk.Capacity,
                _ => media.Size
            };
        }

        private static IEnumerable<PartInfo> CreateDiskParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
        {
            var allocatedParts = new List<PartInfo>();
            if (diskInfo.GptPartitionTablePart != null)
            {
                allocatedParts.AddRange(
                    diskInfo.GptPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
            }

            if (diskInfo.MbrPartitionTablePart != null)
            {
                var mbrParts = diskInfo.MbrPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated)
                    .ToList();

                if (mbrParts.All(x => x.BiosType != BiosPartitionTypes.GptProtective.ToString()) &&
                    diskInfo.GptPartitionTablePart == null)
                {
                    allocatedParts.AddRange(mbrParts);
                }
            }

            if (diskInfo.RdbPartitionTablePart != null)
            {
                allocatedParts.AddRange(
                    diskInfo.RdbPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
            }

            // recalculate percent size against disk size for allocated parts
            foreach (var allocatedPart in allocatedParts)
            {
                allocatedPart.PercentSize = Math.Round(((double)100 / diskInfo.Size) * allocatedPart.Size);
            }

            return allocatedParts.Concat(CreateUnallocatedParts(diskInfo.Size, diskInfo.Size / 512, 0,
                partitionTableTypeContext, allocatedParts, true, false)).OrderBy(x => x.StartOffset).ToList();
        }

        private static PartitionTablePart CreatePartitionTablePart(
            DiskInfo diskInfo,
            PartitionTableInfo partitionTableInfo,
            PartitionTableType partitionTableTypeContext)
        {
            if (partitionTableInfo == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    PartitionType = partitionTableInfo.Type.ToString(),
                    FileSystem = string.Empty,
                    PartitionTableType = partitionTableInfo.Type,
                    PartType = PartType.PartitionTable,
                    Size = partitionTableInfo.Reserved.Size,
                    StartOffset = partitionTableInfo.Reserved.StartOffset,
                    EndOffset = partitionTableInfo.Reserved.EndOffset,
                    StartSector = partitionTableInfo.Reserved.StartSector,
                    EndSector = partitionTableInfo.Reserved.EndSector,
                    StartCylinder = partitionTableInfo.Reserved.StartCylinder,
                    EndCylinder = partitionTableInfo.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * partitionTableInfo.Reserved.Size)
                }
            }.Concat(partitionTableInfo.Partitions.Select(x => new PartInfo
            {
                PartitionType = x.PartitionType,
                FileSystem = x.FileSystem,
                Size = x.Size,
                VolumeSize = x.VolumeSize,
                VolumeFree = x.VolumeFree,
                PartitionTableType = partitionTableInfo.Type,
                PartType = PartType.Partition,
                BiosType = x.BiosType?.ToString() ?? string.Empty,
                GuidType = x.GuidType?.ToString() ?? string.Empty,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size),
                IsActive = x.IsActive,
                IsPrimary = x.IsPrimary,
                StartChs = x.StartChs != null ? new ChsAddress
                {
                    Cylinder = x.StartChs.Cylinder,
                    Head = x.StartChs.Head,
                    Sector = x.StartChs.Sector
                } : null,
                EndChs = x.EndChs != null ? new ChsAddress
                {
                    Cylinder = x.EndChs.Cylinder,
                    Head = x.EndChs.Head,
                    Sector = x.EndChs.Sector
                } : null
            })).ToList();

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = partitionTableInfo.Type,
                DiskGeometry = partitionTableInfo.DiskGeometry != null ? new DiskGeometry
                {
                    Capacity = partitionTableInfo.DiskGeometry.Capacity,
                    TotalSectors = partitionTableInfo.DiskGeometry.TotalSectors,
                    BytesPerSector = partitionTableInfo.DiskGeometry.BytesPerSector,
                    HeadsPerCylinder = partitionTableInfo.DiskGeometry.HeadsPerCylinder,
                    Cylinders = partitionTableInfo.DiskGeometry.Cylinders,
                    SectorsPerTrack = partitionTableInfo.DiskGeometry.SectorsPerTrack
                } : null,
                Size = partitionTableInfo.Size,
                Sectors = partitionTableInfo.Sectors,
                Cylinders = 0,
                Parts = parts.Concat(CreateUnallocatedParts(partitionTableInfo.Size, partitionTableInfo.Sectors, 0,
                    partitionTableTypeContext, parts, true, false)).OrderBy(x => x.StartOffset).ToList()
            };
        }

        private static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
            PartitionTableType partitionTableTypeContext, IEnumerable<PartInfo> parts, bool useSectors,
            bool useCylinders)
        {
            if (diskSize <= 0)
            {
                yield break;
            }

            if (partitionTableTypeContext == PartitionTableType.GuidPartitionTable)
            {
                parts = parts.Where(x => x.BiosType != BiosPartitionTypes.GptProtective.ToString());
            }

            var orderedParts = parts.OrderBy(x => x.StartOffset);
            var overlappingParts = MergeOverlappingParts(orderedParts).ToList();
            var unallocatedParts = new List<PartInfo>();

            var offset = 0L;
            var sector = 0L;
            var cylinder = 0L;
            foreach (var overlappingPart in overlappingParts)
            {
                if (overlappingPart.StartOffset > offset)
                {
                    var unallocatedSize = overlappingPart.StartOffset - offset;
                    yield return new PartInfo
                    {
                        PartitionType = PartType.Unallocated.ToString(),
                        FileSystem = string.Empty,
                        PartitionTableType = PartitionTableType.None,
                        PartType = PartType.Unallocated,
                        Size = unallocatedSize,
                        StartOffset = offset,
                        EndOffset = overlappingPart.StartOffset - 1,
                        StartSector = overlappingPart.StartSector == 0 ? 0 : sector,
                        EndSector = overlappingPart.StartSector == 0 ? 0 : overlappingPart.StartSector - 1,
                        StartCylinder = overlappingPart.StartCylinder == 0 ? 0 : cylinder,
                        EndCylinder = overlappingPart.StartCylinder == 0 ? 0 : overlappingPart.StartCylinder - 1,
                        PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                    };
                }

                offset = overlappingPart.EndOffset + 1;
                sector = useSectors ? overlappingPart.EndSector + 1 : 0;
                cylinder = useCylinders ? overlappingPart.EndCylinder + 1 : 0;
            }

            if (offset < diskSize)
            {
                var unallocatedSize = diskSize - offset;
                yield return new PartInfo
                {
                    PartitionType = PartType.Unallocated.ToString(),
                    FileSystem = string.Empty,
                    PartitionTableType = PartitionTableType.None,
                    PartType = PartType.Unallocated,
                    Size = unallocatedSize,
                    StartOffset = offset,
                    EndOffset = diskSize - 1,
                    StartSector = sectors == 0 ? 0 : sector,
                    EndSector = sectors == 0 ? 0 : sectors - 1,
                    StartCylinder = cylinders == 0 ? 0 : cylinder,
                    EndCylinder = cylinders == 0 ? 0 : cylinders - 1,
                    PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                };
            }
        }

        private static IEnumerable<PartInfo> MergeOverlappingParts(IEnumerable<PartInfo> parts)
        {
            var mergedParts = new List<PartInfo>();

            PartInfo currentPart = null;
            foreach (var part in parts)
            {
                if (currentPart == null)
                {
                    currentPart = part;
                    continue;
                }

                if (!IsOverlapping(part.StartOffset, part.EndOffset, currentPart.StartOffset,
                        currentPart.EndOffset))
                {
                    mergedParts.Add(currentPart);
                    currentPart = part;
                    continue;
                }

                currentPart = new PartInfo
                {
                    StartOffset = Math.Min(part.StartOffset, currentPart.StartOffset),
                    EndOffset = Math.Max(part.EndOffset, currentPart.EndOffset),
                    StartSector = Math.Min(part.StartSector, currentPart.StartSector),
                    EndSector = Math.Max(part.EndSector, currentPart.EndSector),
                    StartCylinder = Math.Min(part.StartCylinder, currentPart.StartCylinder),
                    EndCylinder = Math.Max(part.EndCylinder, currentPart.EndCylinder)
                };
                currentPart.Size = currentPart.EndOffset - currentPart.StartOffset + 1;
            }

            if (currentPart != null)
            {
                mergedParts.Add(currentPart);
            }

            return mergedParts;
        }

        private static bool IsOverlapping(long start1, long end1, long start2, long end2)
        {
            return (start1 <= end2) && (start2 <= end1);
        }

        private static long Next(long value, int size)
        {
            var left = value % size;

            return left == 0 ? value : value - left + size;
        }

        public virtual Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false)
        {
            logger.LogDebug($"Resolving path '{path}'");

            var byteSwap = false;
            
            var modifiersResult = ResolveModifiers(path);

            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                byteSwap = modifiersResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
            }

            logger.LogDebug($"Modifiers: '{modifiersResult.Modifiers}'");

            var diskPathMatch = Regexs.DiskPathRegex.Match(path);
            var physicalDrivePath = diskPathMatch.Success
                ? string.Concat($"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}",
                    path.Substring(diskPathMatch.Groups[1].Value.Length + diskPathMatch.Groups[2].Value.Length))
                : path;

            var directorySeparatorChar = Path.DirectorySeparatorChar;

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '\\' || path[i] == '/')
                {
                    directorySeparatorChar = path[i];
                    break;
                }
            }

            // physical drive
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(physicalDrivePath);
            if (physicalDrivePathMatch.Success)
            {
                var physicalDriveMediaPath = physicalDrivePathMatch.Value;
                var firstSeparatorIndex = physicalDrivePath.IndexOf(directorySeparatorChar.ToString(),
                    physicalDriveMediaPath.Length, StringComparison.Ordinal);

                var fileSystemPath = firstSeparatorIndex >= 0
                        ? physicalDrivePath.Substring(firstSeparatorIndex + 1,
                            physicalDrivePath.Length - (firstSeparatorIndex + 1))
                        : string.Empty;

                logger.LogDebug($"Media Path: '{physicalDriveMediaPath}'");
                logger.LogDebug($"File system Path: '{fileSystemPath}'");

                return new Result<MediaResult>(new MediaResult
                {
                    Exists = true,
                    FullPath = path,
                    MediaPath = physicalDriveMediaPath,
                    FileSystemPath = fileSystemPath,
                    DirectorySeparatorChar = directorySeparatorChar.ToString(),
                    Modifiers = modifiersResult.Modifiers,
                    ByteSwap = byteSwap
                });
            }

            path = PathHelper.GetFullPath(path);

            // media file
            var networkPath = GetNetworkPath(path);
            var next = string.IsNullOrWhiteSpace(networkPath) ? 0 : networkPath.Length;
            do
            {
                next = path.IndexOf(directorySeparatorChar.ToString(), next + 1, StringComparison.OrdinalIgnoreCase);
                var mediaPath = path.Substring(0, next == -1 ? path.Length : next);

                if (File.Exists(mediaPath))
                {
                    var fileSystemPath = mediaPath.Length + 1 < path.Length
                            ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                            : string.Empty;

                    logger.LogDebug($"Media Path: '{mediaPath}'");
                    logger.LogDebug($"File system Path: '{fileSystemPath}'");

                    return new Result<MediaResult>(new MediaResult
                    {
                        Exists = true,
                        FullPath = path,
                        MediaPath = mediaPath,
                        FileSystemPath = fileSystemPath,
                        DirectorySeparatorChar = directorySeparatorChar.ToString(),
                        Modifiers = modifiersResult.Modifiers,
                        ByteSwap = byteSwap
                    });
                }

                if (!Directory.Exists(mediaPath))
                {
                    break;
                }
            } while (next != -1);

            if (!Directory.Exists(path) && !allowNonExisting)
            {
                return new Result<MediaResult>(new PathNotFoundError($"Media not '{path}' found", path));
            }
            
            return new Result<MediaResult>(new MediaResult
            {
                FullPath = path,
                MediaPath = path,
                FileSystemPath = string.Empty,
                DirectorySeparatorChar = directorySeparatorChar.ToString(),
                Modifiers = modifiersResult.Modifiers,
                ByteSwap = byteSwap
            });
        }
        
        private static string GetNetworkPath(string path)
        {
            var networkPathMatch = Regexs.NetworkPathRegex.Match(path);
            return networkPathMatch.Success ? networkPathMatch.Groups[1].Value : null;
        }

        /// <summary>
        /// Resolves modifiers in the start of the path, e.g. +bs:/path/to/file.
        /// </summary>
        /// <param name="path">Path to resolve modifiers from.</param>
        /// <returns>Result</returns>
        public ModifierResult ResolveModifiers(string path)
        {
            if (path.Length == 0)
            {
                return new ModifierResult
                {
                    Path = path,
                    HasModifiers = false,
                    Modifiers = ModifierEnum.None
                };
            }

            var modifierMatch = Regexs.ModifiersRegex.Match(path.ToLower());

            if (!modifierMatch.Success)
            {
                return new ModifierResult
                {
                    Path = path,
                    HasModifiers = false,
                    Modifiers = ModifierEnum.None
                };
            }

            return new ModifierResult
            {
                Path = path.Substring(modifierMatch.Value.Length),
                HasModifiers = true,
                Modifiers = ModifierEnum.ByteSwap
            };
        }

        private string FormatModifiers(ModifierEnum modifiers)
        {
            if (modifiers == ModifierEnum.None)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var modifier in Enum.GetValues<ModifierEnum>())
            {
                switch (modifier)
                {
                    case ModifierEnum.ByteSwap:
                        parts.Add("bs");
                        break;
                }
            }

            return $"+{string.Join("+", parts)}";
        }

        public void Dispose()
        {
            ClearActiveMedias();
            ClearActivePhysicalDrives();
        }
        
        public void ClearActivePhysicalDrives()
        {
            foreach (var activePhysicalDrive in activePhysicalDrives)
            {
                activePhysicalDrive.Dispose();
            }

            activePhysicalDrives.Clear();
        }
        
        public virtual Task RescanPhysicalDrives()
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsDiskManager.RescanDrives();
            }
        
            return Task.CompletedTask;
        }
    }
}