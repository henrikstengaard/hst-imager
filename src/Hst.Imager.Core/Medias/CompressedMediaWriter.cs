namespace Hst.Imager.Core.Medias
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using DiscUtils.Streams;
    using Helpers;
    using Models;
    using GZipStream = SharpCompress.Compressors.Deflate.GZipStream;

    /// <summary>
    /// Creates writable media for new files, compressing data written to .zip and .gz files.
    /// </summary>
    public static class CompressedMediaWriter
    {
        /// <summary>
        /// Create writable media for stream. Compressed media is created for .zip and .gz paths,
        /// otherwise raw media with stream set to size.
        /// </summary>
        public static Media Create(string path, string name, long size, Stream stream, bool byteSwap)
        {
            if (MediaHelper.IsZip(path))
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
                return new Media(path, name, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            if (MediaHelper.IsGZip(path))
            {
                var gZipStream = new GZipStream(stream, SharpCompress.Compressors.CompressionMode.Compress);

                // create interceptor stream disabling seek and set length
                var interceptorStream = new InterceptorStream(gZipStream, seekHandler: (l, _) => l, 
                    setLengthHandler: _ => { });
                return new Media(path, name, Media.MediaType.CompressedRaw, false,
                    interceptorStream, false);
            }

            if (size > 0)
            {
                stream.SetLength(size);
            }
            
            var baseStream = byteSwap
                ? new SectorStream(stream, leaveOpen: false, byteSwap: true)
                : stream;
            return new Media(path, name, Media.MediaType.Raw, false, baseStream, byteSwap);
        }
    }
}
