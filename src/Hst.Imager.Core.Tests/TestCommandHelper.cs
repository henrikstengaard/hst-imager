﻿namespace Hst.Imager.Core.Tests
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
    using Hst.Core.Extensions;
    using Models;

    public class TestCommandHelper : CommandHelper
    {
        public readonly List<TestMedia> TestMedias;
        public readonly RigidDiskBlock rigidDiskBlock;

        public TestCommandHelper(RigidDiskBlock rigidDiskBlock = null) : base(true)
        {
            this.rigidDiskBlock = rigidDiskBlock;
            this.TestMedias = new List<TestMedia>();
        }

        public async Task<byte[]> ReadMediaData(string path)
        {
            var testMedia = GetTestMedia(path);
            if (testMedia != null)
            {
                return testMedia.Data;
            }

            var mediaResult = GetReadableMedia(Enumerable.Empty<IPhysicalDrive>(), path, false);
            using var media = mediaResult.Value;
            var stream = media.Stream;
            return await stream.ReadBytes((int)stream.Length);
        }

        public async Task WriteMediaData(string path, byte[] data)
        {
            var testMedia = GetTestMedia(path);
            if (testMedia != null)
            {
                testMedia.SetData(testMedia.Data.Concat(data).ToArray());
                return;
            }
            var destinationMediaResult = GetWritableMedia(new List<IPhysicalDrive>(), path, data.Length, false, true);
            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;
            await destinationStream.WriteAsync(data, 0, data.Length);
        }
        
        
        public TestMedia GetTestMedia(string path)
        {
            return TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }
        
        public void AddTestMedia(string path, string name = null, byte[] data = null)
        {
            TestMedias.Add(new TestMedia(path, name ?? Path.GetFileNameWithoutExtension(path),
                data ?? Array.Empty<byte>()));
        }

        public void AddTestMedia(string path, long size)
        {
            TestMedias.Add(new TestMedia(path, Path.GetFileNameWithoutExtension(path),
                CreateTestData(size)));
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

        public override Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            bool allowPhysicalDrive = true)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (testMedia != null)
            {
                return new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Data.Length, 
                    testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ? Media.MediaType.Vhd : Media.MediaType.Raw,
                    false, new MemoryStream(testMedia.Data)));
            }

            return base.GetReadableMedia(physicalDrives, path, allowPhysicalDrive);            
        }

        public override Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, long? size = null,
            bool allowPhysicalDrive = true, bool create = false)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var testMedia = TestMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (testMedia != null)
            {
                return new Result<Media>(new Media(testMedia.Path, testMedia.Name, testMedia.Data.Length, 
                    testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ? Media.MediaType.Vhd : Media.MediaType.Raw,
                    false, new TestMediaStream(testMedia)));
            }
            
            return base.GetWritableMedia(physicalDrives, path, size, allowPhysicalDrive, create);
        }
        
        public override async Task<DiskInfo> ReadDiskInfo(Media media, Stream stream)
        {
            var diskInfo = await base.ReadDiskInfo(media, stream);
            if (rigidDiskBlock != null)
            {
                diskInfo.RigidDiskBlock = rigidDiskBlock;
            }

            return diskInfo;
        }

        public override async Task<RigidDiskBlock> GetRigidDiskBlock(Stream stream)
        {
            if (rigidDiskBlock != null)
            {
                return rigidDiskBlock;
            }
            
            return await base.GetRigidDiskBlock(stream);
        }
    }
}