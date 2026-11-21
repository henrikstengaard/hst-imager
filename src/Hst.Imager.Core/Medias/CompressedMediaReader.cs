namespace Hst.Imager.Core.Medias
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Streams;
    using Hst.Core;
    using Compressions.Xz;
    using Extensions;
    using MagicBytes;
    using Models;
    using SharpCompress.Archives.Rar;
    using SharpCompress.Compressors.Xz;

    /// <summary>
    /// Reads media from compressed files: .rar, .zip, .xz and .gz.
    /// </summary>
    public static class CompressedMediaReader
    {
        /// <summary>
        /// Resolve compressed media from stream with data type. Returns null, if data type is not a supported
        /// compressed data type or compressed file doesn't contain a supported image file.
        /// </summary>
        public static async Task<Media> Resolve(string path, string name, Stream stream, DataType dataType,
            bool byteSwap)
        {
            return dataType switch
            {
                DataType.Rar => await ResolveRarMedia(path, name, stream, byteSwap),
                DataType.Zip => await ResolveZipMedia(path, name, stream, byteSwap),
                DataType.Xz => await ResolveXzMedia(path, name, stream, byteSwap),
                DataType.Gzip => await ResolveGzMedia(path, name, stream, byteSwap),
                _ => null
            };
        }

        private static async Task<Media> ResolveRarMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var rarArchive = RarArchive.OpenArchive(stream);
            var rarEntry =
                rarArchive.Entries.FirstOrDefault(x =>
                    !string.IsNullOrEmpty(x.Key) &&
                    (x.Key.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ||
                    x.Key.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ||
                    x.Key.EndsWith(".hdf", StringComparison.OrdinalIgnoreCase)));
            if (rarEntry == null)
            {
                return null;
            }
            
            var headerBytes = new byte[512];
            stream.Position = 0;
            Stream rarEntryStream;
            await using (rarEntryStream = await rarEntry.OpenEntryStreamAsync())
            {
                if (await rarEntryStream.ReadAsync(headerBytes, 0, headerBytes.Length) == 0)
                {
                    return null;
                }
            }

            rarEntryStream = await rarEntry.OpenEntryStreamAsync();
            var baseStream = byteSwap
                ? new SectorStream(rarEntryStream, leaveOpen: false, byteSwap: true)
                : rarEntryStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, Path.GetFileName(rarEntry.Key),
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw, false, new InterceptorStream(baseStream,
                    length: rarEntry.Size, closeHandler: stream.Dispose, readAsyncHandler: async (buffer, offset, count) => 
                        await rarEntryStream.FillAsync(buffer, offset, count)), byteSwap);
        }
        
        private static async Task<Media> ResolveZipMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var zipArchive = new ZipArchive(stream);
            var zipEntry =
                zipArchive.Entries.FirstOrDefault(x =>
                    x.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ||
                    x.Name.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ||
                    x.Name.EndsWith(".hdf", StringComparison.OrdinalIgnoreCase));
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
            var baseStream = byteSwap
                ? new SectorStream(zipEntryStream, leaveOpen: false, byteSwap: true)
                : zipEntryStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, Path.GetFileName(zipEntry.Name),
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw,
                false, new InterceptorStream(
                    baseStream, length: zipEntry.Length, closeHandler: stream.Dispose,
                    readHandler: (buffer, offset, count) =>
                        zipEntryStream.Fill(buffer, offset, count)), byteSwap);
        }

        private static async Task<Media> ResolveXzMedia(string path, string name, Stream stream, bool byteSwap)
        {
            // read uncompressed size from xz indexes at end of xz streams.
            // uncompressed size is unknown, if xz indexes are not valid
            var size = await XzIndexReader.ReadUncompressedSize(stream);

            stream.Position = 0;
            // xz stream used to read header is not disposed, as it would dispose compressed stream
            var headerBytes = await ReadHeader(new XZStream(stream));

            stream.Position = 0;
            var xzStream = new XZStream(stream);
            Stream baseStream = byteSwap
                ? new SectorStream(xzStream, leaveOpen: false, byteSwap: true)
                : xzStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, name,
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw,
                false, new InterceptorStream(
                    baseStream, length: size ?? 0,
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) =>
                        xzStream.Fill(buffer, offset, count)), byteSwap)
            {
                IsSizeUnknown = size == null
            };
        }

        /// <summary>
        /// Resolve gzip compressed media. Uncompressed size is unknown, as gzip only stores uncompressed size
        /// modulo 2^32 and only for the last member. Determining uncompressed size requires decompressing all data.
        /// </summary>
        private static async Task<Media> ResolveGzMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            byte[] headerBytes;
            await using (var headerStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true))
            {
                headerBytes = await ReadHeader(headerStream);
            }

            stream.Position = 0;
            var gZipStream = new GZipStream(stream, CompressionMode.Decompress);
            Stream baseStream = byteSwap
                ? new SectorStream(gZipStream, leaveOpen: false, byteSwap: true)
                : gZipStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, name,
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw,
                false,
                new InterceptorStream(
                    baseStream, length: 0,
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) =>
                        gZipStream.Fill(buffer, offset, count)), byteSwap)
            {
                IsSizeUnknown = true
            };
        }

        /// <summary>
        /// Get uncompressed size of gzip or xz compressed file by decompressing all data.
        /// Used when uncompressed size is unknown and required, e.g. for creating a vhd image file.
        /// </summary>
        public static async Task<Result<long>> GetUncompressedSize(string path, CancellationToken token)
        {
            // file is opened with read write share, as compressed media is already opened for reading
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var headerBytes = new byte[512];
            await stream.FillAsync(headerBytes, 0, headerBytes.Length);
            stream.Position = 0;

            MagicBytesRegister.Instance.TryResolve(headerBytes, out var dataType);

            await using Stream decompressingStream = dataType switch
            {
                DataType.Gzip => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true),
                DataType.Xz => new XZStream(stream),
                _ => null
            };

            if (decompressingStream == null)
            {
                return new Result<long>(new Error($"Unable to get uncompressed size of '{path}'"));
            }

            var size = 0L;
            var buffer = new byte[1024 * 1024];
            int bytesRead;
            while ((bytesRead = await decompressingStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                size += bytesRead;
            }

            return new Result<long>(size);
        }

        /// <summary>
        /// Read first 512 bytes from decompressing stream used to detect data type.
        /// </summary>
        private static async Task<byte[]> ReadHeader(Stream decompressingStream)
        {
            var headerBytes = new byte[512];
            await decompressingStream.FillAsync(headerBytes, 0, headerBytes.Length);
            return headerBytes;
        }
    }
}
