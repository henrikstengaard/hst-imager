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
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            testCommandHelper.AddTestMedia(destinationPath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            compareCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            Assert.NotNull(dataProcessedEventArgs);
            Assert.NotEqual(0, dataProcessedEventArgs.PercentComplete);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesTotal);

            // assert data is identical
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data;
            var destinationBytes = testCommandHelper.GetTestMedia(destinationPath).Data;
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenComparingSourceImgToDestinationVhdThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange destination vhd has copy of source img data
            var sourceBytes = await testCommandHelper.ReadMediaData(sourcePath);
            await testCommandHelper.WriteMediaData(destinationPath, sourceBytes);

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
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
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            testCommandHelper.AddTestMedia(destinationPath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath,
                destinationPath, new Size(size, Unit.Bytes), 0, false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is identical
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data;
            var destinationBytes = testCommandHelper.GetTestMedia(destinationPath).Data;
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
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // create source
            var sourceBytes = testCommandHelper.CreateTestData(ImageSize);
            sourceBytes[offsetWithError] = sourceByte;
            testCommandHelper.AddTestMedia(sourcePath, data: sourceBytes);

            // create destination
            var destinationBytesWithError = testCommandHelper.CreateTestData(ImageSize);
            destinationBytesWithError[offsetWithError] = destinationByte;
            testCommandHelper.AddTestMedia(destinationPath, data: destinationBytesWithError);

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
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
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = testCommandHelper.CreateTestData(ImageSize);

            // create source
            testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes);

            // create destination
            testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
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
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = testCommandHelper.CreateTestData(ImageSize);
            var destinationSize = testDataBytes.Length;

            // create source
            testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // create destination
            testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes);

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
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
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = testCommandHelper.CreateTestData(ImageSize);
            var destinationSize = testDataBytes.Length;

            // create source
            testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // create destination
            testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes);

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
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