using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Medias;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenMediaManager
{
    private static MediaManager CreateMediaManager() =>
        new(NullLogger.Instance, new MediaPathResolver(NullLogger.Instance), false, false, false, CacheType.Disk);

    [Fact]
    public async Task When_CreatingWritableFileMedia_Then_FileIsCreatedWithSize()
    {
        // arrange
        var path = Path.GetFullPath($"{Guid.NewGuid()}.img");
        const long size = 1024 * 1024;

        try
        {
            using var mediaManager = CreateMediaManager();

            // act
            var mediaResult = await mediaManager.GetWritableFileMedia(path, size: size, create: true);

            // assert
            Assert.True(mediaResult.IsSuccess);
            Assert.Equal(Media.MediaType.Raw, mediaResult.Value.Type);
            Assert.Equal(size, mediaResult.Value.Size);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task When_OpeningSameMediaTwice_Then_ActiveMediaIsReused()
    {
        // arrange
        var path = Path.GetFullPath($"{Guid.NewGuid()}.img");
        await File.WriteAllBytesAsync(path, new byte[64 * 1024]);

        try
        {
            using var mediaManager = CreateMediaManager();

            // act
            var firstMediaResult = await mediaManager.GetReadableFileMedia(path);
            var secondMediaResult = await mediaManager.GetReadableFileMedia(path);

            // assert
            Assert.True(firstMediaResult.IsSuccess);
            Assert.True(secondMediaResult.IsSuccess);
            Assert.Same(firstMediaResult.Value, secondMediaResult.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task When_ClearingActiveMediaAndOpeningMediaAgain_Then_NewMediaIsOpened()
    {
        // arrange
        var path = Path.GetFullPath($"{Guid.NewGuid()}.img");
        await File.WriteAllBytesAsync(path, new byte[64 * 1024]);

        try
        {
            using var mediaManager = CreateMediaManager();
            var firstMediaResult = await mediaManager.GetReadableFileMedia(path);

            // act
            mediaManager.ClearActiveMedia(path);
            var secondMediaResult = await mediaManager.GetReadableFileMedia(path);

            // assert
            Assert.True(firstMediaResult.IsSuccess);
            Assert.True(firstMediaResult.Value.IsDisposed);
            Assert.True(secondMediaResult.IsSuccess);
            Assert.NotSame(firstMediaResult.Value, secondMediaResult.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task When_OpeningReadableMediaForNonExistingPath_Then_ErrorIsReturned()
    {
        // arrange
        var path = Path.GetFullPath($"{Guid.NewGuid()}.img");
        using var mediaManager = CreateMediaManager();

        // act
        var mediaResult = await mediaManager.GetReadableMedia([], path);

        // assert
        Assert.True(mediaResult.IsFaulted);
    }
}
