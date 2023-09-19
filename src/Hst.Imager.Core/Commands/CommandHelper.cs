using System.IO.Compression;
using Hst.Imager.Core.Extensions;
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

    public class CommandHelper : ICommandHelper
    {
        private readonly bool isAdministrator;

        /// <summary>
        /// Active medias contains opened medias and is used to get reuse medias without opening same media twice
        /// </summary>
        private readonly IList<Media> activeMedias;

        public CommandHelper(bool isAdministrator)
        {
            this.isAdministrator = isAdministrator;
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

        private Media GetActiveMedia(string path)
        {
            var media = this.activeMedias.FirstOrDefault(x => x.Path == path);
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

            if (media.Stream == null)
            {
                media.SetStream(File.Open(path, FileMode.Open,
                    media.IsWriteable ? FileAccess.ReadWrite : FileAccess.Read));
            }

            return media;
        }

        public virtual Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            return GetPhysicalDriveMedia(physicalDrives, path).Then(() => GetReadableFileMedia(path));
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

        public virtual async Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives,
            string path,
            bool writeable = false)
        {
            var modifiersResult = ResolveModifiers(path);
            var modifiers = ModifierEnum.None;
            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                modifiers = modifiersResult.Modifiers;
            }

            var byteSwap = !writeable && modifiers.HasFlag(ModifierEnum.ByteSwap);

            var physicalDrivePath = GetPhysicalDrivePath(path);
            if (string.IsNullOrEmpty(physicalDrivePath))
            {
                return new Result<Media>((Media)null);
            }

            var media = GetActiveMedia(physicalDrivePath);
            if (media != null)
            {
                return !isAdministrator
                    ? new Result<Media>(new Error($"Path '{path}' requires administrator privileges"))
                    : new Result<Media>(media);
            }

            var physicalDrive =
                physicalDrives.FirstOrDefault(x =>
                    x.Path.Equals(physicalDrivePath, StringComparison.OrdinalIgnoreCase));

            if (physicalDrive == null)
            {
                return new Result<Media>(new Error($"Physical drive '{path}' not found"));
            }

            if (!isAdministrator)
            {
                return new Result<Media>(new Error($"Path '{path}' requires administrator privileges"));
            }

            physicalDrive.SetWritable(writeable);
            physicalDrive.SetByteSwap(byteSwap);
            var physicalDriveMedia = new Media(physicalDrivePath, physicalDrive.Name, physicalDrive.Size,
                Media.MediaType.Raw, true, physicalDrive.Open(), byteSwap);
            this.activeMedias.Add(physicalDriveMedia);
            return new Result<Media>(physicalDriveMedia);
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

        public virtual async Task<Result<Media>> GetReadableFileMedia(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new Result<Media>(new Error("Path not defined"));
            }

            var modifiersResult = ResolveModifiers(path);
            var modifiers = ModifierEnum.None;
            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                modifiers = modifiersResult.Modifiers;
            }

            var byteSwap = modifiers.HasFlag(ModifierEnum.ByteSwap);

            path = PathHelper.GetFullPath(path);
            if (!File.Exists(path))
            {
                return new Result<Media>(new PathNotFoundError($"Path '{path ?? "null"}' not found", nameof(path)));
            }

            var media = GetActiveMedia(path);
            if (media != null)
            {
                return new Result<Media>(media);
            }

            var name = Path.GetFileName(path);
            if (!IsVhd(path))
            {
                var fileStream = File.Open(path, FileMode.Open, FileAccess.Read);
                var fileMedia = await ResolveFileMedia(path, name, fileStream, modifiers);
                this.activeMedias.Add(fileMedia);
                return new Result<Media>(fileMedia);
            }

            var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.Read);
            vhdDisk.Content.Position = 0;
            var vhdMedia = new DiskMedia(path, name, vhdDisk.Capacity, Media.MediaType.Vhd, false, vhdDisk,
                byteSwap, new SectorStream(vhdDisk.Content, byteSwap, leaveOpen: true));
            this.activeMedias.Add(vhdMedia);
            return new Result<Media>(vhdMedia);
        }

        private async Task<Media> ResolveFileMedia(string path, string name, Stream stream, ModifierEnum modifiers)
        {
            stream.Position = 0;
            var headerBytes = new byte[512];
            var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);
            var byteSwap = modifiers.HasFlag(ModifierEnum.ByteSwap);

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
            stream.Position = 0;
            return new Media(path, name, stream.Length, Media.MediaType.Raw, false,
                new SectorStream(stream, byteSwap: byteSwap, leaveOpen: false), byteSwap);
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

        public virtual Task<Result<Media>> GetWritableFileMedia(string path, long? size = null, bool create = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(path);
            }

            var modifiersResult = ResolveModifiers(path);
            var modifiers = ModifierEnum.None;
            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                modifiers = modifiersResult.Modifiers;
            }

            var byteSwap = modifiers.HasFlag(ModifierEnum.ByteSwap);

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
                    var fileMedia = ResolveWritableFileMedia(path, name, fileStream);
                    this.activeMedias.Add(fileMedia);
                    return Task.FromResult(new Result<Media>(fileMedia));
                }

                if (size == null || size.Value == 0)
                {
                    throw new ArgumentNullException(nameof(size), "Size is required for creating VHD image file");
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

            var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            disk.Content.Position = 0;
            var vhdMedia = new DiskMedia(path, name, disk.Capacity, Media.MediaType.Vhd, false,
                disk, byteSwap, new SectorStream(disk.Content, leaveOpen: true, byteSwap: byteSwap));
            this.activeMedias.Add(vhdMedia);
            return Task.FromResult(new Result<Media>(vhdMedia));
        }

        private Media ResolveWritableFileMedia(string path, string name, Stream stream)
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
                var interceptorStream = new InterceptorStream(zipEntryStream, seekHandler: (l, _) => l, 
                    setLengthHandler: _ => { }, closeHandler: () => zipArchive.Dispose());
                return new Media(path, name, stream.Length, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            if (IsGZip(path))
            {
                var gZipStream = new GZipStream(stream, SharpCompress.Compressors.CompressionMode.Compress);
                var interceptorStream = new InterceptorStream(gZipStream, seekHandler: (l, _) => l, 
                    setLengthHandler: _ => { });
                return new Media(path, name, stream.Length, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            return new Media(path, name, stream.Length, Media.MediaType.Raw, false, stream, false);
        }

        public virtual Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            long? size = null, bool create = false)
        {
            return GetPhysicalDriveMedia(physicalDrives, path, true)
                .Then(() => GetWritableFileMedia(path, size, create));
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

        public virtual async Task<RigidDiskBlock> GetRigidDiskBlock(Stream stream)
        {
            return await RigidDiskBlockReader.Read(stream);
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
            if (MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, firstChunk, 0))
            {
                return await CompressedVhd(media.Stream, firstChunk);
            }

            return await CompressedImg(media.Stream, firstChunk);
        }

        private async Task<VirtualDisk> CompressedVhd(Stream stream, byte[] firstChunk)
        {
            // open vhd disk from first chunk
            var vhdDisk = new Disk(new MemoryStream(firstChunk), Ownership.None);
            var firstRdbChunk = new byte[(1024 * 1024) / 2];
            vhdDisk.Content.Position = 0;
            var bytesRead = await vhdDisk.Content.ReadAsync(firstRdbChunk, 0, firstRdbChunk.Length);

            // return vhd disk as is, if no bytes read
            if (bytesRead == 0)
            {
                return vhdDisk;
            }

            // read rdb from first rdb chunk
            var rigidDiskBlock = await ParseRigidDiskBlock(firstRdbChunk);
            if (rigidDiskBlock == null)
            {
                return vhdDisk;
            }

            // return disk as is, if rdb size is less than bytes read
            var rdbSize = rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize;
            if (rdbSize < bytesRead)
            {
                return vhdDisk;
            }

            // dispose vhd disk with first chunk
            vhdDisk.Dispose();

            // read second chunk
            var secondChunkSize = Convert.ToInt32(Math.Floor(((double)rdbSize - bytesRead) / firstChunk.Length) + 1) *
                                  firstChunk.Length;
            var secondChunk = new byte[secondChunkSize];
            await stream.FillAsync(secondChunk, 0, secondChunk.Length);

            // return first and second chunk as vhd disk
            return new Disk(new MemoryStream(firstChunk.Concat(secondChunk).ToArray()), Ownership.None);
        }

        private async Task<VirtualDisk> CompressedImg(Stream stream, byte[] firstChunk)
        {
            var rigidDiskBlock = await ReadRigidDiskBlock(firstChunk);

            // return first chunk as raw disk, if no rigid disk block present in first chunk
            if (rigidDiskBlock == null)
            {
                return new DiscUtils.Raw.Disk(new MemoryStream(firstChunk), Ownership.None);
            }

            var rdbSize = rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize;

            // return raw disk with first chunk, if rdb size is less than first chunk size
            if (rdbSize < firstChunk.Length)
            {
                return new DiscUtils.Raw.Disk(new MemoryStream(firstChunk), Ownership.None);
            }

            // read second chunk
            var secondChunkSize =
                Convert.ToInt32(Math.Floor(((double)rdbSize - firstChunk.Length) / firstChunk.Length) + 1) *
                firstChunk.Length;
            var secondChunk = new byte[secondChunkSize];
            await stream.FillAsync(secondChunk, 0, secondChunk.Length);

            // return first and second chunk as raw disk
            return new DiscUtils.Raw.Disk(new MemoryStream(firstChunk.Concat(secondChunk).ToArray()), Ownership.None);
        }

        private async Task<RigidDiskBlock> ReadRigidDiskBlock(byte[] firstChunk)
        {
            // read rigid disk block from first chunk, if first chunk doesn't contain vhd magic number
            if (!MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, firstChunk, 0))
            {
                return await ParseRigidDiskBlock(firstChunk);
            }

            // open vhd disk from first chunk
            using var disk = new Disk(new MemoryStream(firstChunk), Ownership.Dispose);
            var rdbChunk = new byte[512 * 16];
            disk.Content.Position = 0;
            var bytesRead = await disk.Content.ReadAsync(rdbChunk, 0, rdbChunk.Length);
            if (bytesRead != rdbChunk.Length)
            {
                return null;
            }

            // read rdb from rdb chunk
            return await ParseRigidDiskBlock(rdbChunk);
        }

        private async Task<RigidDiskBlock> ParseRigidDiskBlock(byte[] buffer)
        {
            var sector = 0;
            var rdbLocationLimit = 16;
            RigidDiskBlock rigidDiskBlock = null;
            var blockBytes = new byte[512];
            do
            {
                //var blockBytes = new Span<byte>(buffer, sector * 512, 512);
                Array.Copy(buffer, sector * 512, blockBytes, 0, 512);

                // skip, if identifier doesn't match rigid disk block
                var identifier = BitConverter.ToUInt32(blockBytes, 0);
                if (!identifier.Equals(BlockIdentifiers.RigidDiskBlock))
                {
                    sector++;
                    continue;
                }

                // read rigid disk block
                rigidDiskBlock = await RigidDiskBlockReader.Parse(blockBytes, false);
            } while (sector < rdbLocationLimit && rigidDiskBlock == null);

            return rigidDiskBlock;
        }

        public virtual async Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None)
        {
            var partitionTables = new List<PartitionTableInfo>();

            var disk = await ResolveVirtualDisk(media);

            try
            {
                var biosPartitionTable = new BiosPartitionTable(disk);

                var mbrPartitionNumber = 0;

                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableType.MasterBootRecord,
                    Size = disk.Geometry.Capacity,
                    Sectors = disk.Geometry.TotalSectorsLong,
                    Cylinders = 0,
                    Partitions = biosPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++mbrPartitionNumber,
                        FileSystem = x.TypeAsString,
                        BiosType = x.BiosType,
                        Size = x.SectorCount * disk.BlockSize,
                        StartOffset = x.FirstSector * disk.BlockSize,
                        EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1,
                        StartSector = x.FirstSector,
                        EndSector = x.LastSector,
                        StartCylinder = 0,
                        EndCylinder = 0
                    }).ToList(),
                    Reserved = new PartitionTableReservedInfo
                    {
                        StartOffset = 0,
                        EndOffset = 511,
                        StartSector = 0,
                        EndSector = 0,
                        StartCylinder = 0,
                        EndCylinder = 0,
                        Size = 512
                    },
                    StartOffset = 0,
                    EndOffset = disk.Geometry.Capacity - 1,
                    StartSector = 0,
                    EndSector = disk.Geometry.TotalSectorsLong - 1
                });
            }
            catch (Exception)
            {
                // ignored, if read bios partition table fails
            }

            try
            {
                var guidPartitionTable = new GuidPartitionTable(disk);

                var guidPartitionNumber = 0;

                var guidReservedSize = guidPartitionTable.FirstUsableSector * disk.BlockSize;
                partitionTables.Add(new PartitionTableInfo
                {
                    Type = PartitionTableType.GuidPartitionTable,
                    Size = disk.Capacity,
                    Sectors = guidPartitionTable.LastUsableSector + 1,
                    Cylinders = 0,
                    Partitions = guidPartitionTable.Partitions.Select(x => new PartitionInfo
                    {
                        PartitionNumber = ++guidPartitionNumber,
                        FileSystem = x.TypeAsString,
                        BiosType = x.BiosType,
                        Size = x.SectorCount * disk.BlockSize,
                        StartOffset = x.FirstSector * disk.BlockSize,
                        EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1,
                        StartSector = x.FirstSector,
                        EndSector = x.LastSector,
                        StartCylinder = 0,
                        EndCylinder = 0
                    }).ToList(),
                    Reserved = new PartitionTableReservedInfo
                    {
                        StartOffset = 0,
                        EndOffset = guidReservedSize - 1,
                        StartSector = 0,
                        EndSector = guidPartitionTable.FirstUsableSector > 0
                            ? guidPartitionTable.FirstUsableSector - 1
                            : 0,
                        StartCylinder = 0,
                        EndCylinder = 0,
                        Size = guidReservedSize
                    },
                    StartOffset = 0,
                    EndOffset = disk.Capacity - 1,
                    StartSector = guidPartitionTable.FirstUsableSector,
                    EndSector = guidPartitionTable.LastUsableSector,
                    StartCylinder = 0,
                    EndCylinder = 0
                });
            }
            catch (Exception)
            {
                // ignored, if read guid partition table fails
            }

            RigidDiskBlock rigidDiskBlock = null;
            try
            {
                rigidDiskBlock = await GetRigidDiskBlock(disk.Content);
                if (rigidDiskBlock != null)
                {
                    var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
                    var rdbPartitionNumber = 0;

                    var rdbStartCyl = 0;
                    var rdbEndCyl =
                        Convert.ToInt32(Math.Ceiling((double)rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize /
                                                     cylinderSize)) - 1;

                    var rdbStartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize;
                    var rdbEndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1;
                    partitionTables.Add(new PartitionTableInfo
                    {
                        Type = PartitionTableType.RigidDiskBlock,
                        Size = rigidDiskBlock.DiskSize,
                        Sectors = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
                        Cylinders = rigidDiskBlock.Cylinders,
                        Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
                        {
                            PartitionNumber = ++rdbPartitionNumber,
                            FileSystem = x.DosTypeFormatted,
                            Size = x.PartitionSize,
                            StartOffset = (long)x.LowCyl * cylinderSize,
                            EndOffset = ((long)x.HighCyl + 1) * cylinderSize - 1,
                            StartSector = (long)x.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                            EndSector = (long)x.HighCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                            StartCylinder = x.LowCyl,
                            EndCylinder = x.HighCyl,
                        }).ToList(),
                        Reserved = new PartitionTableReservedInfo
                        {
                            StartOffset = rdbStartOffset,
                            EndOffset = rdbEndOffset,
                            StartSector = rigidDiskBlock.RdbBlockLo,
                            EndSector = rigidDiskBlock.RdbBlockHi,
                            StartCylinder = rdbStartCyl,
                            EndCylinder = rdbEndCyl,
                            Size = rdbEndOffset - rdbStartOffset + 1
                        },
                        StartOffset = rdbStartOffset,
                        EndOffset = rdbStartOffset + rigidDiskBlock.DiskSize - 1,
                        StartCylinder = 0,
                        EndCylinder = rigidDiskBlock.HiCylinder,
                        StartSector = rigidDiskBlock.RdbBlockLo,
                        EndSector = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
                    });
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

            diskInfo.GptPartitionTablePart = CreateGptParts(diskInfo, partitionTableTypeContext);
            diskInfo.MbrPartitionTablePart = CreateMbrParts(diskInfo, partitionTableTypeContext);
            diskInfo.RdbPartitionTablePart = CreateRdbParts(diskInfo, partitionTableTypeContext);
            diskInfo.DiskParts = CreateDiskParts(diskInfo, partitionTableTypeContext);

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
                allocatedParts.AddRange(
                    diskInfo.MbrPartitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated));
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

            return CreateUnallocatedParts(diskInfo.Size, diskInfo.Size / 512, 0,
                partitionTableTypeContext, allocatedParts, true, false);
        }

        private static PartitionTablePart CreateGptParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
        {
            var gptPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.GuidPartitionTable);

            if (gptPartitionTable == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    FileSystem = "Reserved",
                    PartitionTableType = PartitionTableType.GuidPartitionTable,
                    PartType = PartType.PartitionTable,
                    Size = gptPartitionTable.Reserved.Size,
                    StartOffset = gptPartitionTable.Reserved.StartOffset,
                    EndOffset = gptPartitionTable.Reserved.EndOffset,
                    StartSector = gptPartitionTable.Reserved.StartSector,
                    EndSector = gptPartitionTable.Reserved.EndSector,
                    StartCylinder = gptPartitionTable.Reserved.StartCylinder,
                    EndCylinder = gptPartitionTable.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * gptPartitionTable.Reserved.Size)
                }
            }.Concat(gptPartitionTable.Partitions.Select(x => new PartInfo
            {
                FileSystem = x.FileSystem,
                Size = x.Size,
                PartitionTableType = gptPartitionTable.Type,
                PartType = PartType.Partition,
                BiosType = x.BiosType,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size)
            }));

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = gptPartitionTable.Type,
                Size = gptPartitionTable.Size,
                Sectors = gptPartitionTable.Sectors,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(gptPartitionTable.Size, gptPartitionTable.Sectors, 0,
                    partitionTableTypeContext, parts, true, false)
            };
        }

        private static PartitionTablePart CreateMbrParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
        {
            var mbrPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.MasterBootRecord);

            if (mbrPartitionTable == null)
            {
                return null;
            }

            var parts = new List<PartInfo>
            {
                new()
                {
                    FileSystem = "Reserved",
                    PartitionTableType = PartitionTableType.MasterBootRecord,
                    PartType = PartType.PartitionTable,
                    Size = mbrPartitionTable.Reserved.Size,
                    StartOffset = mbrPartitionTable.Reserved.StartOffset,
                    EndOffset = mbrPartitionTable.Reserved.EndOffset,
                    StartSector = mbrPartitionTable.Reserved.StartSector,
                    EndSector = mbrPartitionTable.Reserved.EndSector,
                    StartCylinder = mbrPartitionTable.Reserved.StartCylinder,
                    EndCylinder = mbrPartitionTable.Reserved.EndCylinder,
                    PercentSize = Math.Round(((double)100 / diskInfo.Size) * mbrPartitionTable.Reserved.Size)
                }
            }.Concat(mbrPartitionTable.Partitions.Select(x => new PartInfo
            {
                FileSystem = x.FileSystem,
                Size = x.Size,
                PartitionTableType = mbrPartitionTable.Type,
                PartType = PartType.Partition,
                BiosType = x.BiosType,
                PartitionNumber = x.PartitionNumber,
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                StartSector = x.StartSector,
                EndSector = x.EndSector,
                StartCylinder = x.StartCylinder,
                EndCylinder = x.EndCylinder,
                PercentSize = Math.Round(((double)100 / diskInfo.Size) * x.Size)
            }));

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = mbrPartitionTable.Type,
                Size = mbrPartitionTable.Size,
                Sectors = mbrPartitionTable.Sectors,
                Cylinders = 0,
                Parts = CreateUnallocatedParts(mbrPartitionTable.Size, mbrPartitionTable.Sectors, 0,
                    partitionTableTypeContext, parts, true, false)
            };
        }

        private static PartitionTablePart CreateRdbParts(DiskInfo diskInfo,
            PartitionTableType partitionTableTypeContext)
        {
            var parts = new List<PartInfo>();
            if (diskInfo.RigidDiskBlock == null)
            {
                return null;
            }

            var cylinderSize = diskInfo.RigidDiskBlock.Heads * diskInfo.RigidDiskBlock.Sectors *
                               diskInfo.RigidDiskBlock.BlockSize;

            var rdbStartCyl = 0;
            var rdbEndCyl = (Next(diskInfo.RigidDiskBlock.RdbBlockHi * diskInfo.RigidDiskBlock.BlockSize,
                (int)cylinderSize) / cylinderSize) - 1;
            var rdbSize = (diskInfo.RigidDiskBlock.RdbBlockHi - diskInfo.RigidDiskBlock.RdbBlockLo + 1) *
                          diskInfo.RigidDiskBlock.BlockSize;

            parts.Add(new PartInfo
            {
                FileSystem = "Reserved",
                PartitionTableType = PartitionTableType.RigidDiskBlock,
                PartType = PartType.PartitionTable,
                Size = rdbSize,
                StartOffset = diskInfo.RigidDiskBlock.RdbBlockLo * diskInfo.RigidDiskBlock.BlockSize,
                EndOffset = ((diskInfo.RigidDiskBlock.RdbBlockHi + 1) * diskInfo.RigidDiskBlock.BlockSize) - 1,
                StartSector = diskInfo.RigidDiskBlock.RdbBlockLo,
                EndSector = diskInfo.RigidDiskBlock.RdbBlockHi,
                StartCylinder = rdbStartCyl,
                EndCylinder = rdbEndCyl,
                PercentSize = Math.Round(((double)100 / diskInfo.RigidDiskBlock.DiskSize) * rdbSize)
            });

            var partitionNumber = 0;
            foreach (var partitionBlock in diskInfo.RigidDiskBlock.PartitionBlocks.OrderBy(x => x.LowCyl).ToList())
            {
                parts.Add(new PartInfo
                {
                    FileSystem = partitionBlock.DosTypeFormatted,
                    PartitionTableType = PartitionTableType.RigidDiskBlock,
                    PartType = PartType.Partition,
                    PartitionNumber = ++partitionNumber,
                    Size = partitionBlock.PartitionSize,
                    StartOffset = (long)partitionBlock.LowCyl * cylinderSize,
                    EndOffset = ((long)partitionBlock.HighCyl + 1) * cylinderSize - 1,
                    StartSector = (long)partitionBlock.LowCyl * diskInfo.RigidDiskBlock.Heads *
                                  diskInfo.RigidDiskBlock.Sectors,
                    EndSector = (((long)partitionBlock.HighCyl + 1) * diskInfo.RigidDiskBlock.Heads *
                                 diskInfo.RigidDiskBlock.Sectors) - 1,
                    StartCylinder = partitionBlock.LowCyl,
                    EndCylinder = partitionBlock.HighCyl,
                    PercentSize = Math.Round(((double)100 / diskInfo.RigidDiskBlock.DiskSize) *
                                             partitionBlock.PartitionSize)
                });
            }

            return new PartitionTablePart
            {
                Path = diskInfo.Path,
                PartitionTableType = PartitionTableType.RigidDiskBlock,
                Size = diskInfo.RigidDiskBlock.DiskSize,
                Sectors = diskInfo.RigidDiskBlock.Sectors,
                Cylinders = diskInfo.RigidDiskBlock.Cylinders,
                Parts = CreateUnallocatedParts(diskInfo.RigidDiskBlock.DiskSize,
                    diskInfo.RigidDiskBlock.DiskSize / diskInfo.RigidDiskBlock.BlockSize, 0,
                    partitionTableTypeContext, parts, true, true)
            };
        }

        private static IEnumerable<PartInfo> CreateUnallocatedParts(long diskSize, long sectors, long cylinders,
            PartitionTableType partitionTableTypeContext, IEnumerable<PartInfo> parts, bool useSectors,
            bool useCylinders)
        {
            if (diskSize <= 0)
            {
                throw new ArgumentException($"Invalid disk size '{diskSize}'", nameof(diskSize));
            }

            if (partitionTableTypeContext == PartitionTableType.GuidPartitionTable)
            {
                parts = parts.Where(x => x.BiosType != BiosPartitionTypes.GptProtective);
            }

            parts = parts.OrderBy(x => x.StartOffset);
            var partsList = MergeOverlappingParts(parts).ToList();
            var unallocatedParts = new List<PartInfo>();

            var offset = 0L;
            var sector = 0L;
            var cylinder = 0L;
            foreach (var part in partsList)
            {
                if (part.StartOffset > offset)
                {
                    var unallocatedSize = part.StartOffset - offset;
                    unallocatedParts.Add(new PartInfo
                    {
                        FileSystem = "Unallocated",
                        PartitionTableType = PartitionTableType.None,
                        PartType = PartType.Unallocated,
                        Size = unallocatedSize,
                        StartOffset = offset,
                        EndOffset = part.StartOffset - 1,
                        StartSector = part.StartSector == 0 ? 0 : sector,
                        EndSector = part.StartSector == 0 ? 0 : part.StartSector - 1,
                        StartCylinder = part.StartCylinder == 0 ? 0 : cylinder,
                        EndCylinder = part.StartCylinder == 0 ? 0 : part.StartCylinder - 1,
                        PercentSize = Math.Round(((double)100 / diskSize) * unallocatedSize)
                    });
                }

                offset = part.EndOffset + 1;
                sector = useSectors ? part.EndSector + 1 : 0;
                cylinder = useCylinders ? part.EndCylinder + 1 : 0;
            }

            if (offset < diskSize)
            {
                var unallocatedSize = diskSize - offset;
                unallocatedParts.Add(new PartInfo
                {
                    FileSystem = "Unallocated",
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
                });
            }

            return partsList.Concat(unallocatedParts).OrderBy(x => x.StartOffset).ToList();
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

        public virtual Result<MediaResult> ResolveMedia(string path)
        {
            var modifiersResult = ResolveModifiers(path);
            var modifiers = ModifierEnum.None;
            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                modifiers = modifiersResult.Modifiers;
            }

            var byteSwap = modifiers.HasFlag(ModifierEnum.ByteSwap);

            var diskPathMatch = Regexs.DiskPathRegex.Match(path);
            var physicalDrivePath = diskPathMatch.Success
                ? string.Concat($"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}",
                    path.Substring(diskPathMatch.Groups[1].Value.Length + diskPathMatch.Groups[2].Value.Length))
                : path;

            var directorySeparatorChar = Path.DirectorySeparatorChar.ToString();

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '\\' || path[i] == '/')
                {
                    directorySeparatorChar = path[i].ToString();
                    break;
                }
            }

            // physical drive
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(physicalDrivePath);
            if (physicalDrivePathMatch.Success)
            {
                var physicalDriveMediaPath = physicalDrivePathMatch.Value;
                var firstSeparatorIndex = physicalDrivePath.IndexOf(directorySeparatorChar,
                    physicalDriveMediaPath.Length, StringComparison.Ordinal);

                return new Result<MediaResult>(new MediaResult
                {
                    FullPath = path,
                    MediaPath = string.Concat(physicalDriveMediaPath, modifiersResult.Raw),
                    FileSystemPath = firstSeparatorIndex >= 0
                        ? physicalDrivePath.Substring(firstSeparatorIndex + 1,
                            physicalDrivePath.Length - (firstSeparatorIndex + 1))
                        : string.Empty,
                    DirectorySeparatorChar = directorySeparatorChar,
                    ByteSwap = byteSwap
                });
            }

            path = PathHelper.GetFullPath(path);

            // media file
            var next = 0;
            do
            {
                next = path.IndexOf(directorySeparatorChar, next + 1, StringComparison.OrdinalIgnoreCase);
                var mediaPath = path.Substring(0, next == -1 ? path.Length : next);

                if (File.Exists(mediaPath))
                {
                    return new Result<MediaResult>(new MediaResult
                    {
                        FullPath = path,
                        MediaPath = string.Concat(mediaPath, modifiersResult.Raw),
                        FileSystemPath = mediaPath.Length + 1 < path.Length
                            ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                            : string.Empty,
                        DirectorySeparatorChar = directorySeparatorChar,
                        ByteSwap = byteSwap
                    });
                }

                if (!Directory.Exists(mediaPath))
                {
                    break;
                }
            } while (next != -1);

            return new Result<MediaResult>(new PathNotFoundError($"Media not '{path}' found", path));
        }

        public ModifierResult ResolveModifiers(string path)
        {
            var modifiersResult = ModifierEnum.None;

            //
            var modifierMatch = Regexs.ModifiersRegex.Match(path.ToLower());

            if (!modifierMatch.Success)
            {
                return new ModifierResult
                {
                    Raw = string.Empty,
                    Path = path,
                    HasModifiers = false,
                    Modifiers = ModifierEnum.None
                };
            }

            var modifiers = modifierMatch.Groups[1].Value.Split('+').ToList();
            path = path.Substring(modifierMatch.Index);

            foreach (var modifier in modifiers)
            {
                switch (modifier)
                {
                    case "bs":
                        modifiersResult |= ModifierEnum.ByteSwap;
                        break;
                }
            }

            return new ModifierResult
            {
                Raw = modifierMatch.Groups[1].Value,
                Path = path.Substring(0, modifierMatch.Groups[1].Index),
                HasModifiers = modifiersResult != ModifierEnum.None,
                Modifiers = modifiersResult
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
    }
}