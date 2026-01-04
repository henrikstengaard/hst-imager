using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Core;
    using Commands;
    using DiscUtils.Streams;
    using Hst.Core.Extensions;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;

    public class TestCommandHelper : CommandHelper
    {
        public readonly List<TestMedia> TestMedias;
        public readonly RigidDiskBlock RigidDiskBlock;
        public static readonly string PhysicalDrivePath = Hst.Core.OperatingSystem.IsWindows() 
            ? @"\\.\PhysicalDrive0"
            : "/dev/disk0";

        public TestCommandHelper(RigidDiskBlock rigidDiskBlock = null) : base(new NullLogger<ICommandHelper>(), true)
        {
            this.RigidDiskBlock = rigidDiskBlock;
            this.TestMedias = new List<TestMedia>();
        }

        public async Task<byte[]> ReadMediaData(string path)
        {
            var mediaResult = await GetReadableMedia([], path);
            if (!mediaResult.IsSuccess)
            {
                throw new IOException($"Unable to get readable media '{path}'");
            }

            using var media = mediaResult.Value;
            var stream = MediaHelper.GetStreamFromMedia(media);
            stream.Position = 0;
            
            return await stream.ReadBytes((int)stream.Length);
        }

        public async Task WriteMediaData(string path, byte[] data)
        {
            var mediaResult = await GetWritableMedia([], path, size: data.Length, create: true);
            if (!mediaResult.IsSuccess)
            {
                throw new IOException($"Unable to get readable media '{path}'");
            }

            using var media = mediaResult.Value;
            var stream = MediaHelper.GetStreamFromMedia(media);
            stream.Position = 0;

            await stream.WriteAsync(data, 0, data.Length);
        }

        public TestMedia GetTestMedia(string path)
        {
            return TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddTestMedia(string path, string name = null, byte[] data = null)
        {
            var testMedia = new TestMedia(path, name ?? Path.GetFileNameWithoutExtension(path), data?.Length ?? 0);
            if (data != null)
            {
                await testMedia.WriteData(data);
            }
            TestMedias.Add(testMedia);
        }

        public void AddTestMedia(string path, long size)
        {
            TestMedias.Add(new TestMedia(path, Path.GetFileNameWithoutExtension(path), size));
        }
        
        public async Task CreateTestMedia(string path, long size = 0, byte[] data = null, bool createTestData = false)
        {
            TestMedias.Add(new TestMedia(path, Path.GetFileNameWithoutExtension(path), 
                Path.GetExtension(path).Equals(".vhd", StringComparison.OrdinalIgnoreCase) ? 0 : size));

            var mediaResult = await GetWritableFileMedia(path, size: size, create: true);
            using var media = mediaResult.Value;

            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
            stream.Position = 0;

            if (data == null && !createTestData)
            {
                return;
            }

            await stream.WriteBytes(data ?? CreateTestData(size));
        }
        
        public void AddTestMediaWithData(string path, long size)
        {
            var testMedia = new TestMedia(path, Path.GetFileNameWithoutExtension(path), size);
            var data = CreateTestData(size);
            testMedia.Stream.Write(data, 0, data.Length);
            TestMedias.Add(testMedia);
        }

        public byte[] CreateTestData(long size)
        {
            var data = new byte[size];

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            return data;
        }

        /// <summary>
        /// Overrides get physical drive media to prevent access to physical drives while running unit tests.
        /// </summary>
        /// <param name="physicalDrives">List of physical drives.</param>
        /// <param name="path">Path to physical drive.</param>
        /// <param name="modifiers">Modifiers to use.</param>
        /// <param name="writeable">Get writeable media.</param>
        /// <returns>Media.</returns>
        /// <exception cref="ArgumentNullException">Null exception thrown, if path is null.</exception>
        public override Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, bool writeable = false)
        {
            ArgumentNullException.ThrowIfNull(path);

            if (path != PhysicalDrivePath)
            {
                return Task.FromResult(new Result<Media>((Media)null));
            }

            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (testMedia == null)
            {
                return Task.FromResult(new Result<Media>((Media)null));
            }

            var stream = testMedia.Stream;
            if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Size,
                    Media.MediaType.Raw, false, stream, false)));
            }
            
            var disk = new DiscUtils.Vhd.Disk(stream, Ownership.Dispose);
            return Task.FromResult(new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, disk.Capacity, Media.MediaType.Vhd, 
                false, disk, false, stream)));
        }

        public override async Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (testMedia == null)
            {
                return await base.GetReadableFileMedia(path, modifiers);
            }
            
            //
            var byteSwap = false;
            var modifierResult = ResolveModifiers(path);
            if (modifierResult.HasModifiers)
            {
                path = modifierResult.Path;
                byteSwap = modifierResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
            }

            var stream = testMedia.Stream;
            if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
            {
                return new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Size,
                    Media.MediaType.Raw, false, stream, byteSwap));
            }

            stream.Position = 0;
            var vhdDisk = new DiscUtils.Vhd.Disk(stream, Ownership.None);
            var sectorStream = new SectorStream(vhdDisk.Content, byteSwap: byteSwap, leaveOpen: true);
            return new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, vhdDisk.Capacity, Media.MediaType.Vhd, 
                false, vhdDisk, byteSwap, sectorStream));        
        }

        public override Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null, long? size = null, bool create = false)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (testMedia == null)
            {
                return base.GetWritableFileMedia(path, modifiers, size, create);
            }

            if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Size,
                    Media.MediaType.Raw, false, testMedia.Stream, false)));
            }

            if (size.HasValue)
            {
                size = GetVhdSize(size.Value);
            }
            
            if (create && size == null)
            {
                return Task.FromResult(new Result<Media>(new Error("Vhd requires size")));
            }

            //
            var byteSwap = false;
            var modifierResult = ResolveModifiers(path);
            if (modifierResult.HasModifiers)
            {
                path = modifierResult.Path;
                byteSwap = modifierResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
            }

            var stream = testMedia.Stream;
            var isEmptyStream = stream.Length == 0;
            var vhdDisk = create || isEmptyStream
                ? DiscUtils.Vhd.Disk.InitializeDynamic(stream, Ownership.None, size ?? 0)
                : new DiscUtils.Vhd.Disk(stream, Ownership.None);

            var sectorStream = new SectorStream(vhdDisk.Content, byteSwap: byteSwap, leaveOpen: true);
            return Task.FromResult(new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, vhdDisk.Capacity, 
                Media.MediaType.Vhd, false, vhdDisk, false, sectorStream)));
        }

        public override Stream CreateWriteableStream(string path, bool create)
        {
            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            return testMedia != null
                ? testMedia.Stream
                : base.CreateWriteableStream(path, create);
        }

        public override async Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None)
        {
            var diskInfo = await base.ReadDiskInfo(media, partitionTableTypeContext);
            if (RigidDiskBlock != null)
            {
                diskInfo.RigidDiskBlock = RigidDiskBlock;
            }

            return diskInfo;
        }

        public override Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false)
        {
            ArgumentNullException.ThrowIfNull(path);

            var testMedia = TestMedias.FirstOrDefault(x => path.StartsWith(x.Path, StringComparison.OrdinalIgnoreCase));
            if (testMedia == null)
            {
                return base.ResolveMedia(path, allowNonExisting);
            }
            
            var directorySeparatorChar = path.IndexOf("\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "\\" : "/";
            var fileSystemPath = path.StartsWith(testMedia.Path, StringComparison.OrdinalIgnoreCase) && path.Length > testMedia.Path.Length
                ? path.Substring(testMedia.Path.Length + 1)
                : string.Empty;
            
            return new Result<MediaResult>(new MediaResult
            {
                Exists = true,
                FullPath = path,
                MediaPath = testMedia.Path,
                DirectorySeparatorChar = directorySeparatorChar,
                FileSystemPath = fileSystemPath,
                Modifiers = ModifierEnum.None,
                ByteSwap = false
            });
        }

        public override Task RescanPhysicalDrives()
        {
            return Task.CompletedTask;
        }
    }
}