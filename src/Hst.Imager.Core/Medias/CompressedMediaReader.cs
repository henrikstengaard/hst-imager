namespace Hst.Imager.Core.Medias
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using DiscUtils.Streams;
    using Extensions;
    using MagicBytes;
    using Models;
    using SharpCompress.Archives.Rar;

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
            stream.Position = 0;
            var sizeAndHeader = await GetStreamLength(new SharpCompress.Compressors.Xz.XZStream(stream));

            stream.Position = 0;
            var zxStream = new SharpCompress.Compressors.Xz.XZStream(stream);
            Stream baseStream = byteSwap
                ? new SectorStream(zxStream, leaveOpen: false, byteSwap: true)
                : zxStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(sizeAndHeader.Item2, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, name,
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw,
                false, new InterceptorStream(
                    baseStream, length: sizeAndHeader.Item1, 
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        zxStream.Fill(buffer, offset, count)), byteSwap);
        }

        private static async Task<Media> ResolveGzMedia(string path, string name, Stream stream, bool byteSwap)
        {
            stream.Position = 0;
            var sizeAndHeader =
                await GetStreamLength(new System.IO.Compression.GZipStream(stream, CompressionMode.Decompress));

            stream.Position = 0;
            var gZipStream = new System.IO.Compression.GZipStream(stream, CompressionMode.Decompress);
            Stream baseStream = byteSwap
                ? new SectorStream(gZipStream, leaveOpen: false, byteSwap: true)
                : gZipStream;
            var isVhd = MagicBytesRegister.Instance.TryResolve(sizeAndHeader.Item2, out var dataType)
                        && dataType == DataType.Vhd;
            return new Media(path, name,
                isVhd ? Media.MediaType.CompressedVhd : Media.MediaType.CompressedRaw,
                false,
                new InterceptorStream(
                    baseStream, length: sizeAndHeader.Item1, 
                    closeHandler: stream.Dispose, readHandler: (buffer, offset, count) => 
                        gZipStream.Fill(buffer, offset, count)), byteSwap);
        }

        private static async Task<Tuple<long, byte[]>> GetStreamLength(Stream stream)
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
    }
}
