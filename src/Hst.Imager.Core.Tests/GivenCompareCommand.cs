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
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);
            testCommandHelper.AddTestMediaWithData(destinationPath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(new NullLogger<CompareCommand>(),
                    testCommandHelper,
                    new List<IPhysicalDrive>(),
                    sourcePath,
                    0,
                    destinationPath,
                    0,
                    new Size(),
                    0,
                    false,
                    false);
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
            var sourceBytes = await testCommandHelper.GetTestMedia(sourcePath).ReadData();
            var destinationBytes = await testCommandHelper.GetTestMedia(destinationPath).ReadData();
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenComparingSourceImgToDestinationVhdThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange destination vhd has copy of source img data
            var sourceBytes = await testCommandHelper.ReadMediaData(sourcePath);
            await testCommandHelper.WriteMediaData(destinationPath, sourceBytes);

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(
                new NullLogger<CompareCommand>(),
                testCommandHelper,
                new List<IPhysicalDrive>(),
                sourcePath, 
                0,
                destinationPath,
                0,
                new Size(), 
                0, 
                false,
                false);
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
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);
            testCommandHelper.AddTestMediaWithData(destinationPath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - compare source img to destination img
            var compareCommand = new CompareCommand(
                new NullLogger<CompareCommand>(),
                testCommandHelper,
                new List<IPhysicalDrive>(),
                sourcePath,
                0,
                destinationPath,
                0,
                new Size(size, Unit.Bytes),
                0,
                false,
                false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            compareCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data processed is 100 percent complete and has processed size
            Assert.NotNull(dataProcessedEventArgs);
            Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
            Assert.Equal(size, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.Equal(size, dataProcessedEventArgs.BytesTotal);
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
            await testCommandHelper.AddTestMedia(sourcePath, data: sourceBytes);

            // create destination
            var destinationBytesWithError = testCommandHelper.CreateTestData(ImageSize);
            destinationBytesWithError[offsetWithError] = destinationByte;
            await testCommandHelper.AddTestMedia(destinationPath, data: destinationBytesWithError);

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(
                    new NullLogger<CompareCommand>(),
                    testCommandHelper,
                    new List<IPhysicalDrive>(),
                    sourcePath,
                    0,
                    destinationPath,
                    0,
                    new Size(),
                    0,
                    false,
                    false);
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
            await testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes);

            // create destination
            await testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(
                    new NullLogger<CompareCommand>(),
                    testCommandHelper,
                    new List<IPhysicalDrive>(),
                    sourcePath,
                    0,
                    destinationPath,
                    0,
                    new Size(),
                    0,
                    false,
                    false);
            var result = await compareCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task When_ComparingSourceLargerThanDestination_Then_ErrorIsReturned()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = testCommandHelper.CreateTestData(ImageSize);

            // create source
            await testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // create destination
            await testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes);

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(
                    new NullLogger<CompareCommand>(),
                    testCommandHelper,
                    new List<IPhysicalDrive>(),
                    sourcePath,
                    0,
                    destinationPath,
                    0,
                    new Size(),
                    0,
                    false,
                    false);
            
            // act - execute compare command
            var result = await compareCommand.Execute(cancellationTokenSource.Token);

            // assert - compare command returned error
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<CompareSizeTooLargeError>(result.Error);
        }
        
        [Fact]
        public async Task When_ComparingSourceSmallerThanDestination_Then_DataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            var testDataBytes = testCommandHelper.CreateTestData(ImageSize);

            // create source
            await testCommandHelper.AddTestMedia(sourcePath, data: testDataBytes);

            // create destination
            await testCommandHelper.AddTestMedia(destinationPath, data: testDataBytes.Concat(testDataBytes).ToArray());

            // act - compare source img to destination img
            var compareCommand =
                new CompareCommand(
                    new NullLogger<CompareCommand>(),
                    testCommandHelper,
                    new List<IPhysicalDrive>(),
                    sourcePath,
                    0,
                    destinationPath,
                    0,
                    new Size(),
                    0,
                    false,
                    false);
            var bytesProcessed = 0L;
            compareCommand.DataProcessed += (_, args) =>
            {
                bytesProcessed = args.BytesProcessed;
            };
            
            // act - execute compare command
            var result = await compareCommand.Execute(cancellationTokenSource.Token);

            // assert - compare command succeeded
            Assert.True(result.IsSuccess);
            
            // assert - bytes processed comparing is equal to destination size
            Assert.Equal(testDataBytes.Length, bytesProcessed);
        }

        [Fact]
        public async Task WhenComparingZipAndGZipCompressedImgThenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.zip";
            var destinationPath = $"{Guid.NewGuid()}.img.gz";
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source zip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.zip"), sourcePath);

                // arrange - create destination gzip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), destinationPath);
                
                // act - compare zip compressed img media and gzip compressed img media
                var cancellationTokenSource = new CancellationTokenSource();
                var compareCommand =
                    new CompareCommand(
                        new NullLogger<CompareCommand>(),
                        testCommandHelper,
                        new List<IPhysicalDrive>(),
                        sourcePath,
                        0,
                        destinationPath,
                        0,
                        new Size(),
                        0,
                        false,
                        false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                compareCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await compareCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
                
                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesTotal);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }
    }
}