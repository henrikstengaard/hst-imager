using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenTransferCommandWithCompressedDestination
{
    [Theory]
    [InlineData(".img.gz")]
    [InlineData(".zip")]
    public async Task When_TransferringImageWithZeroFilledDataToCompressedImageWithSparseFiles_Then_AllDataIsTransferred(
        string extension)
    {
        // arrange - source image with data in first 1mb and zero filled data after
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}{extension}");
        const int size = 10 * 1024 * 1024;
        var data = new byte[size];
        for (var i = 0; i < 1024 * 1024; i++)
        {
            data[i] = (byte)(i % 256);
        }
        await File.WriteAllBytesAsync(sourcePath, data);

        try
        {
            // act - transfer source image to compressed destination image with sparse files enabled
            using (var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false, true, false,
                       CacheType.Disk))
            {
                var transferCommand = new TransferCommand(commandHelper, sourcePath, destinationPath, new Size(),
                    false, 0, 0, true);
                var result = await transferCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);
            }

            // assert - compressed destination image is smaller than source image
            Assert.True(new FileInfo(destinationPath).Length < size);

            // assert - compressed destination image contains all data from source image
            using (var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false))
            {
                var mediaResult = await commandHelper.GetReadableFileMedia(destinationPath);
                Assert.True(mediaResult.IsSuccess);
                var media = mediaResult.Value;
                Assert.Equal(size, media.Size);
                var destinationData = await media.Stream.ReadBytes(size);
                Assert.Equal(data, destinationData);
            }
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }
}
