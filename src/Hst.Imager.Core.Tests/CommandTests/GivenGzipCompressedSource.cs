using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

/// <summary>
/// Gzip compressed source has unknown uncompressed size, so data is processed until end of source.
/// </summary>
public class GivenGzipCompressedSource
{
    private const int Size = 10 * 1024 * 1024;

    [Fact]
    public async Task WhenTransferToImgThenAllDataIsTransferredAndProgressIsIndeterminate()
    {
        // arrange - gzip compressed source image
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img.gz");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        var data = CreateData();
        await CreateGzipFile(sourcePath, data);

        try
        {
            // act - transfer gzip compressed source image to destination image
            DataProcessedEventArgs dataProcessedEventArgs = null;
            using (var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false))
            {
                var transferCommand = new TransferCommand(commandHelper, sourcePath, destinationPath, new Size(),
                    false, 0, 0, false);
                transferCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await transferCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);
            }

            // assert - data processed is indeterminate and all data is processed
            Assert.NotNull(dataProcessedEventArgs);
            Assert.True(dataProcessedEventArgs.Indeterminate);
            Assert.Equal(Size, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesTotal);

            // assert - destination image contains all data from source image
            Assert.Equal(data, await File.ReadAllBytesAsync(destinationPath));
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task WhenTransferToVhdThenVhdIsCreatedWithUncompressedSize()
    {
        // arrange - gzip compressed source image
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img.gz");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}.vhd");
        var data = CreateData();
        await CreateGzipFile(sourcePath, data);

        try
        {
            // act - transfer gzip compressed source image to destination vhd
            using (var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false))
            {
                var transferCommand = new TransferCommand(commandHelper, sourcePath, destinationPath, new Size(),
                    false, 0, 0, false);
                var result = await transferCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);
            }

            // assert - destination vhd has size of uncompressed source image and contains all data
            using (var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false))
            {
                var mediaResult = await commandHelper.GetReadableFileMedia(destinationPath);
                Assert.True(mediaResult.IsSuccess);
                using var media = mediaResult.Value;
                Assert.True(media.Size >= Size);
                var destinationData = await media.Stream.ReadBytes(Size);
                Assert.Equal(data, destinationData);
            }
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task WhenCompareWithIdenticalImgThenDataIsIdenticalAndProgressIsIndeterminate()
    {
        // arrange - gzip compressed source image and identical destination image
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img.gz");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        var data = CreateData();
        await CreateGzipFile(sourcePath, data);
        await File.WriteAllBytesAsync(destinationPath, data);

        try
        {
            // act - compare gzip compressed source image with destination image
            using var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), commandHelper,
                new List<IPhysicalDrive>(), sourcePath, 0, destinationPath, 0, new Size(), 0, false, false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            compareCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await compareCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - data processed is indeterminate and all data is compared
            Assert.NotNull(dataProcessedEventArgs);
            Assert.True(dataProcessedEventArgs.Indeterminate);
            Assert.Equal(Size, dataProcessedEventArgs.BytesProcessed);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task WhenCompareWithSmallerImgThenErrorIsReturned()
    {
        // arrange - gzip compressed source image and destination image with first half of source data
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img.gz");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        var data = CreateData();
        await CreateGzipFile(sourcePath, data);
        await File.WriteAllBytesAsync(destinationPath, data.AsSpan(0, Size / 2).ToArray());

        try
        {
            // act - compare gzip compressed source image with smaller destination image
            using var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), commandHelper,
                new List<IPhysicalDrive>(), sourcePath, 0, destinationPath, 0, new Size(), 0, false, false);
            var result = await compareCommand.Execute(CancellationToken.None);

            // assert - compare failed as source is larger than destination
            Assert.True(result.IsFaulted);
            Assert.IsType<SourceSizeTooLargeError>(result.Error);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    [Fact]
    public async Task WhenTransferWithPercentSizeThenErrorIsReturned()
    {
        // arrange - gzip compressed source image
        var sourcePath = Path.GetFullPath($"{Guid.NewGuid()}.img.gz");
        var destinationPath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        await CreateGzipFile(sourcePath, CreateData());

        try
        {
            // act - transfer 50% of gzip compressed source image
            using var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);
            var transferCommand = new TransferCommand(commandHelper, sourcePath, destinationPath,
                new Size(50, Unit.Percent), false, 0, 0, false);
            var result = await transferCommand.Execute(CancellationToken.None);

            // assert - transfer failed as percent size can't be resolved for unknown size
            Assert.True(result.IsFaulted);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    private static byte[] CreateData()
    {
        var data = new byte[Size];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 7 % 251);
        }

        return data;
    }

    private static async Task CreateGzipFile(string path, byte[] data)
    {
        await using var stream = File.Create(path);
        await using var gZipStream = new GZipStream(stream, CompressionLevel.Fastest);
        await gZipStream.WriteAsync(data);
    }
}
