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
    using Models;

    public class FakeCommandHelper : CommandHelper
    {
        public readonly List<Media> ReadableMedias;
        public readonly List<Media> WriteableMedias;

        public readonly RigidDiskBlock rigidDiskBlock;
        public const int ImageSize = 512 * 512;
        public const int RigidDiskBlockSize = 16 * 1024;

        public FakeCommandHelper(IEnumerable<string> readableMediaPaths = null,
            IEnumerable<string> writeableMediaPaths = null, RigidDiskBlock rigidDiskBlock = null) : base(true)
        {
            ReadableMedias = new List<Media>();
            foreach (var readableMediaPath in readableMediaPaths ?? Enumerable.Empty<string>())
            {
                var data = File.Exists(readableMediaPath) ? File.ReadAllBytes(readableMediaPath) : CreateTestData(); 
                ReadableMedias.Add(new Media(readableMediaPath, Path.GetFileName(readableMediaPath), data.Length, Media.MediaType.Raw, false,
                    new MemoryStream(data)));
            }

            WriteableMedias = new List<Media>();
            foreach (var writeableMediaPath in writeableMediaPaths ?? Enumerable.Empty<string>())
            {
                if (IsVhd(writeableMediaPath))
                {
                    continue;
                }
                
                WriteableMedias.Add(new Media(writeableMediaPath, Path.GetFileName(writeableMediaPath), 0, Media.MediaType.Raw, false,
                    new MemoryStream()));
            }

            this.rigidDiskBlock = rigidDiskBlock;
        }

        public Media GetMedia(string path)
        {
            return ReadableMedias.Concat(WriteableMedias)
                .FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] CreateTestData()
        {
            var data = new byte[ImageSize];

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            return data;
        }

        public override Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            bool allowPhysicalDrive = true)
        {
            return path.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? new Result<Media>(ReadableMedias.Concat(WriteableMedias)
                    .FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                : base.GetReadableMedia(physicalDrives, path, allowPhysicalDrive);
        }

        public override Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, long? size = null,
            bool allowPhysicalDrive = true, bool create = false)
        {
            return path.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? new Result<Media>(WriteableMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                : base.GetWritableMedia(physicalDrives, path, size, allowPhysicalDrive, create);
        }
        
        public async Task AppendWriteableMediaDataVhd(string path, long size, byte[] data = null)
        {
            if (!IsVhd(path))
            {
                throw new ArgumentException("Path is not vhd", nameof(path));
            }
            
            var destinationMediaResult = GetWritableMedia(new List<IPhysicalDrive>(), path,
                size, false, true);
            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;
            if (data == null)
            {
                return;
            }

            await destinationStream.WriteAsync(data, 0, data.Length);
        }
        
        public override Stream CreateWriteableStream(string path)
        {
            var media = this.GetMedia(path);
            if (media != null)
            {
                return media.Stream;
            }
            
            return base.CreateWriteableStream(path);
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