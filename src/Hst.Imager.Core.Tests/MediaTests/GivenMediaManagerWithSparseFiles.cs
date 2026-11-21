using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Medias;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenMediaManagerWithSparseFiles
{
    private static MediaManager CreateMediaManager() =>
        new(NullLogger.Instance, new MediaPathResolver(NullLogger.Instance), false, true, false, CacheType.Disk);

    [Theory]
    [InlineData(".img.gz")]
    [InlineData(".zip")]
    public async Task When_CreatingCompressedMediaWithSize_Then_CompressedFileIsNotPaddedAndReadsBackWithSameSize(
        string extension)
    {
        // arrange
        var path = Path.GetFullPath($"{Guid.NewGuid()}{extension}");
        const int size = 10 * 1024 * 1024;
        var data = new byte[size];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        try
        {
            // act - create compressed media with size and write data
            using (var mediaManager = CreateMediaManager())
            {
                var writableMediaResult = await mediaManager.GetWritableFileMedia(path, size: size, create: true);
                Assert.True(writableMediaResult.IsSuccess);
                await writableMediaResult.Value.Stream.WriteBytes(data);
            }

            // assert - compressed file is smaller than uncompressed size
            Assert.True(new FileInfo(path).Length < size,
                $"Compressed file size {new FileInfo(path).Length} is not smaller than uncompressed size {size}");

            // assert - compressed media reads back with same size and data
            using (var mediaManager = CreateMediaManager())
            {
                var readableMediaResult = await mediaManager.GetReadableFileMedia(path);
                Assert.True(readableMediaResult.IsSuccess);
                var media = readableMediaResult.Value;

                // assert - gzip uncompressed size is unknown, zip uncompressed size is known
                Assert.Equal(extension == ".img.gz", media.IsSizeUnknown);
                Assert.Equal(extension == ".img.gz" ? 0 : size, media.Size);
                var readData = await media.Stream.ReadBytes(size);
                Assert.Equal(data, readData);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
