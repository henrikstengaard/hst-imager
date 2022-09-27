namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models;
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
                    sourcePath, destinationPath, new Size());
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
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size());
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
                destinationPath, new Size(size, Unit.Bytes));
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
                    sourcePath, destinationPath, new Size());
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.False(result.IsSuccess);
            Assert.Equal(typeof(ByteNotEqualError), result.Error.GetType());
            var byteNotEqualError = (ByteNotEqualError)result.Error;
            Assert.Equal(offsetWithError, byteNotEqualError.Offset);
            Assert.Equal(sourceByte, byteNotEqualError.SourceByte);
            Assert.Equal(destinationByte, byteNotEqualError.DestinationByte);
        }

        [Fact]
        public async Task WhenComparingSourceImgToDestinationImgWithDifferentSizesThenResultIsSizeNotEqualError()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // create source
            var sourceBytes = fakeCommandHelper.CreateTestData();
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, sourceBytes.Length,
                Media.MediaType.Raw, false,
                new MemoryStream(sourceBytes)));

            // create destination
            var destinationSize = sourceBytes.Length / 2;
            var destinationBytesChunk = new byte[Convert.ToInt32(destinationSize)];
            Array.Copy(sourceBytes, 0, destinationBytesChunk, 0, destinationBytesChunk.Length);
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath, destinationSize,
                Media.MediaType.Raw, false,
                new MemoryStream(destinationBytesChunk)));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size());
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.False(result.IsSuccess);

            Assert.Equal(typeof(SizeNotEqualError), result.Error.GetType());
            var sizeNotEqualError = (SizeNotEqualError)result.Error;
            Assert.Equal(0, sizeNotEqualError.Offset);
            Assert.Equal(sourceBytes.Length, sizeNotEqualError.Size);
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
                    sourcePath, destinationPath, new Size());
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task WhenCompareSourceIsLargerThanDestinationThenResultIsSizeNotEqualError()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = fakeCommandHelper.CreateTestData();

            // create source
            fakeCommandHelper.ReadableMedias.Add(new Media(sourcePath, sourcePath, testDataBytes.Length * 2,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes.Concat(testDataBytes).ToArray())));

            // create destination
            fakeCommandHelper.ReadableMedias.Add(new Media(destinationPath, destinationPath, testDataBytes.Length,
                Media.MediaType.Raw, false,
                new MemoryStream(testDataBytes)));

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), fakeCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size());
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.False(result.IsSuccess);

            Assert.Equal(typeof(SizeNotEqualError), result.Error.GetType());
            var sizeNotEqualError = (SizeNotEqualError)result.Error;
            Assert.Equal(0, sizeNotEqualError.Offset);
            Assert.Equal(testDataBytes.Length * 2, sizeNotEqualError.Size);
        }
    }
}