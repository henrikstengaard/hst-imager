namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenCompareCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenComparingSourceImgAndDestinationImgThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper(new[] { sourcePath, destinationPath });
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            compareCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            await compareCommand.Execute(cancellationTokenSource.Token);

            Assert.NotNull(dataProcessedEventArgs);
            Assert.NotEqual(0, dataProcessedEventArgs.PercentComplete);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesTotal);

            // assert data is identical
            var sourceBytes = fakeCommandHelper.GetMedia(sourcePath).GetBytes();
            var destinationBytes = fakeCommandHelper.GetMedia(destinationPath).GetBytes();
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenComparingSourceImgToDestinationVhdThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var fakeCommandHelper = new FakeCommandHelper(new[] { sourcePath });
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange destination vhd has copy of source img data
            var sourceBytes = fakeCommandHelper.GetMedia(sourcePath).GetBytes();
            await fakeCommandHelper.AppendWriteableMediaDataVhd(destinationPath, sourceBytes.Length, sourceBytes);

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // delete destination path vhd
            File.Delete(destinationPath);
        }

        [Fact]
        public async Task WhenComparingSourceImgToDestinationImgWithSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var fakeCommandHelper = new FakeCommandHelper(new[] { sourcePath, destinationPath });
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(), sourcePath,
                destinationPath, new Size(size, Unit.Bytes), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is identical
            var sourceBytes = fakeCommandHelper.GetMedia(sourcePath).GetBytes(size);
            var destinationBytes = fakeCommandHelper.GetMedia(destinationPath).GetBytes(size);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenComparingSourceImgToImgDestinationWithDifferentBytesAtOffsetThenResultIsByteNotEqualError()
        {
            // arrange
            const int offsetWithError = 8390;
            const byte sourceByte = 178;
            const byte destinationByte = 250;
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // create source
            var sourceBytes = fakeCommandHelper.CreateTestData();
            sourceBytes[offsetWithError] = sourceByte;
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, sourceBytes.Length,
                Media.MediaType.Raw, false,
                new MemoryStream(sourceBytes)));

            // create destination
            var destinationBytesWithError = fakeCommandHelper.CreateTestData();
            destinationBytesWithError[offsetWithError] = destinationByte;
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath,
                destinationBytesWithError.Length, Media.MediaType.Raw, false,
                new MemoryStream(destinationBytesWithError)));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.False(result.IsSuccess);
            Assert.Equal(typeof(ByteNotEqualError), result.Error.GetType());
            var byteNotEqualError = (ByteNotEqualError)result.Error;
            Assert.Equal(offsetWithError, byteNotEqualError.Offset);
            Assert.Equal(sourceByte, byteNotEqualError.SourceByte);
            Assert.Equal(destinationByte, byteNotEqualError.DestinationByte);
        }

        [Fact]
        public async Task WhenComparingSourceSmallerThanDestinationThenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = fakeCommandHelper.CreateTestData();

            // create source
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, testDataBytes.Length,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes)));

            // create destination
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath, testDataBytes.Length * 2,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes.Concat(testDataBytes).ToArray())));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task WhenComparingSourceLargerThanDestinationAndCompareSizeIsDestinationSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = fakeCommandHelper.CreateTestData();
            var sourceSize = testDataBytes.Length * 2;
            var destinationSize = testDataBytes.Length;

            // create source
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, sourceSize,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes.Concat(testDataBytes).ToArray())));

            // create destination
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath, destinationSize,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes)));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(destinationSize, Unit.Bytes), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
        }
        
        [Fact]
        public async Task WhenComparingSourceLargerThanDestinationThenLargestComparableSizeOfDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = fakeCommandHelper.CreateTestData();
            var sourceSize = testDataBytes.Length * 2;
            var destinationSize = testDataBytes.Length;

            // create source
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, sourceSize,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes.Concat(testDataBytes).ToArray())));

            // create destination
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath, destinationSize,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes)));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false);
            var bytesProcessed = 0L;
            compareCommand.DataProcessed += (sender, args) =>
            {
                bytesProcessed = args.BytesProcessed;
            };
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // assert - bytes processed comparing is equal to destination size
            Assert.Equal(destinationSize, bytesProcessed);
        }
    }
}