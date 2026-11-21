namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Commands;
    using Hst.Core.Extensions;
    using Helpers;

    /// <summary>
    /// Command helper opening test medias in memory with test media manager, test media path resolver
    /// and test disk info reader sharing the same list of test medias.
    /// </summary>
    public class TestCommandHelper : CommandHelper
    {
        public readonly List<TestMedia> TestMedias;
        public readonly RigidDiskBlock RigidDiskBlock;
        public static readonly string PhysicalDrivePath = TestMediaManager.PhysicalDrivePath;

        public TestCommandHelper(RigidDiskBlock rigidDiskBlock = null)
            : this(new List<TestMedia>(), rigidDiskBlock)
        {
        }

        private TestCommandHelper(List<TestMedia> testMedias, RigidDiskBlock rigidDiskBlock)
            : this(testMedias, rigidDiskBlock, new TestMediaPathResolver(testMedias))
        {
        }

        private TestCommandHelper(List<TestMedia> testMedias, RigidDiskBlock rigidDiskBlock,
            TestMediaPathResolver pathResolver)
            : base(new TestMediaManager(testMedias, pathResolver), pathResolver,
                new TestDiskInfoReader(rigidDiskBlock))
        {
            this.RigidDiskBlock = rigidDiskBlock;
            this.TestMedias = testMedias;
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

            var stream = media.Stream;
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
    }
}
