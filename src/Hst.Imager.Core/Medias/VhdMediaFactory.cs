namespace Hst.Imager.Core.Medias
{
    using System;
    using System.IO;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Helpers;
    using Models;

    /// <summary>
    /// Opens vhd files as media with optional layered cache.
    /// </summary>
    public class VhdMediaFactory
    {
        private readonly bool useCache;
        private readonly CacheType cacheType;
        private readonly int blockSize;

        public VhdMediaFactory(bool useCache, CacheType cacheType, int blockSize = 1024 * 1024)
        {
            this.useCache = useCache;
            this.cacheType = cacheType;
            this.blockSize = blockSize;
        }

        /// <summary>
        /// Open vhd media from stream. Stream is disposed when media is disposed.
        /// </summary>
        public Media Open(string path, string name, Stream stream, bool writeable, bool byteSwap)
        {
            var vhdDisk = new Disk(stream, Ownership.Dispose);

            return CreateMedia(path, name, vhdDisk, writeable, byteSwap);
        }

        /// <summary>
        /// Open vhd media from path.
        /// </summary>
        public Media Open(string path, string name, bool writeable, bool byteSwap)
        {
            Stream fileStream = null;
            Disk vhdDisk;

            try
            {
                fileStream = File.Open(path, FileMode.Open, writeable ? FileAccess.ReadWrite : FileAccess.Read);
                vhdDisk = new Disk(fileStream, Ownership.Dispose);
            }
            catch (Exception e)
            {
                fileStream?.Close();
                fileStream?.Dispose();
                throw new InvalidOperationException($"Failed to open vhd disk '{path}': ", e);
            }

            return CreateMedia(path, name, vhdDisk, writeable, byteSwap);
        }

        private Media CreateMedia(string path, string name, Disk vhdDisk, bool writeable, bool byteSwap)
        {
            if (vhdDisk == null)
            {
                throw new InvalidOperationException($"Failed to open vhd disk '{path}'");
            }

            vhdDisk.Content.Position = 0;
            var baseStream = new SectorStream(vhdDisk.Content, byteSwap: byteSwap, leaveOpen: false);
            var stream = useCache
                ? CacheHelper.AddLayeredCache(path, baseStream, writeable, blockSize, cacheType)
                : baseStream;

            return new DiskMedia(path, name, Media.MediaType.Vhd, false, vhdDisk,
                byteSwap, stream);
        }
    }
}
