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
        public static readonly string PhysicalDrivePath = "physical-drive";

        public TestCommandHelper(RigidDiskBlock rigidDiskBlock = null) : base(new NullLogger<ICommandHelper>(), true)
        {
            this.RigidDiskBlock = rigidDiskBlock;
            this.TestMedias = new List<TestMedia>();
        }

        public async Task<byte[]> ReadMediaData(string path)
        {
            var testMedia = GetTestMedia(path);
            if (testMedia != null)
            {
                return await testMedia.ReadData();
            }

            var mediaResult = await GetReadableMedia(Enumerable.Empty<IPhysicalDrive>(), path);
            using var media = mediaResult.Value;
            var stream = media.Stream;
            return await stream.ReadBytes((int)stream.Length);
        }

        public async Task WriteMediaData(string path, byte[] data)
        {
            var testMedia = GetTestMedia(path);
            if (testMedia != null)
            {
                await testMedia.WriteData(data);
                return;
            }

            var destinationMediaResult = await GetWritableMedia(new List<IPhysicalDrive>(), path, size: data.Length, create: true);
            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;
            await destinationStream.WriteAsync(data, 0, data.Length);
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

        public override Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, bool writeable = false)
        {
            // return writable file, if path is equal physical drive path. otherwise null
            return path == PhysicalDrivePath ? GetWritableFileMedia(path, modifiers) : Task.FromResult(new Result<Media>((Media)null));
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

            var stream = testMedia.Stream;
            if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
            {
                return new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Size,
                    Media.MediaType.Raw, false, stream, false));
            }
            
            var disk = new DiscUtils.Vhd.Disk(stream, Ownership.Dispose);
            return new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, testMedia.Size, Media.MediaType.Vhd, 
                false, disk, false, stream));        
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

            var stream = testMedia.Stream;
            var disk = create || testMedia.Stream.Length == 0
                ? DiscUtils.Vhd.Disk.InitializeDynamic(testMedia.Stream, Ownership.None, size ?? 0)
                : new DiscUtils.Vhd.Disk(stream, Ownership.None);
            return Task.FromResult(new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, testMedia.Size, 
                Media.MediaType.Vhd, false, disk, false, stream)));
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

        public override Result<MediaResult> ResolveMedia(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var testMedia = TestMedias.FirstOrDefault(x => path.StartsWith(x.Path, StringComparison.OrdinalIgnoreCase));
            if (testMedia == null)
            {
                return base.ResolveMedia(path);
            }
            
            return new Result<MediaResult>(new MediaResult
            {
                FullPath = path,
                MediaPath = testMedia.Path,
                DirectorySeparatorChar = path.IndexOf("\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "\\" : "//",
                FileSystemPath = path.Length > testMedia.Path.Length ? path.Substring(testMedia.Path.Length + 1) : path,
                Modifiers = ModifierEnum.None,
                ByteSwap = false
            });
        }
    }
}