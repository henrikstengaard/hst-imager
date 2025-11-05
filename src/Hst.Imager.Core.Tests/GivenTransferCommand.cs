namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Models;
    using Xunit;

    public class GivenTransferCommand : CommandTestBase
    {
        [Fact]
        public async Task When_ConvertSrcImgToDestImg_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - convert source img to destination img
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(), false, 0, 0);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            convertCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            Assert.NotNull(dataProcessedEventArgs);
            Assert.NotEqual(0, dataProcessedEventArgs.PercentComplete);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesTotal);

            // get src bytes from img
            var sourceBytes = await ReadMediaBytes(testCommandHelper, sourcePath);

            // get dest bytes from img
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath);

            // assert - src and dest bytes are identical
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task When_ConvertSrcImgToDestImgWithSize_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - convert source img to destination img
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false, 0, 0);
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // get src bytes from img
            var sourceBytes = await ReadMediaBytes(testCommandHelper, sourcePath);

            // get dest bytes from img
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath);

            // assert - dest bytes contains size of src bytes and remaining bytes are zero
            var expectedDestBytes = new byte[ImageSize];
            Array.Copy(sourceBytes, 0, expectedDestBytes, 0, size);
            Assert.Equal(expectedDestBytes, destinationBytes);
        }

        [Fact]
        public async Task When_ConvertSrcImgToDestVhd_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - read source img to destination vhd
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(), false, 0, 0);
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // get src bytes from img
            var sourceBytes = await ReadMediaBytes(testCommandHelper, sourcePath);

            // get dest bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath);

            // assert - src and dest bytes are identical
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task When_ConvertSrcImgToDestVhdWithSize_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - read source img to destination vhd
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false, 0, 0);
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // get source bytes from img
            var sourceBytes = await ReadMediaBytes(testCommandHelper, sourcePath);;

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath);

            // assert - dest bytes contains size of src bytes and remaining bytes are zero
            var expectedDestBytes = new byte[ImageSize];
            Array.Copy(sourceBytes, 0, expectedDestBytes, 0, size);
            Assert.Equal(expectedDestBytes, destinationBytes);
        }

        [Fact]
        public async Task When_TransferSrcImgToDestVhdWithSrcStartOffset_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var srcStartOffset = (16 * 512) + 200;
            var size = ImageSize - srcStartOffset;
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - transfer src img to dest vhd
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false, srcStartOffset, 0);
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // get source bytes from img
            var sourceBytes = await ReadMediaBytes(testCommandHelper, sourcePath);

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath);

            // assert - dest bytes contains size of src bytes from src start offset and remaining bytes are zero
            var expectedDestinationBytes = new byte[ImageSize];
            Array.Copy(sourceBytes, srcStartOffset, expectedDestinationBytes, 0, size);
            Assert.Equal(expectedDestinationBytes, destinationBytes);
        }

        [Fact]
        public async Task When_TransferSrcImgToDestVhdWithDestStartOffset_Then_ReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var destStartOffset = (16 * 512) + 200;
            var size = ImageSize - destStartOffset;
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.CreateTestMedia(sourcePath, ImageSize, createTestData: true);
            await testCommandHelper.CreateTestMedia(destinationPath, ImageSize);

            // act - transfer src img to dest vhd
            var convertCommand = new TransferCommand(testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false, 0, destStartOffset);
            var result = await convertCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // get source bytes
            var sourceBytes = (await testCommandHelper.GetTestMedia(sourcePath).ReadData()).ToArray();

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath, ImageSize);

            // assert - dest bytes contains size of src bytes at dest start offset and remaining bytes are zero
            var expectedDestBytes = new byte[ImageSize];
            Array.Copy(sourceBytes, 0, expectedDestBytes, destStartOffset, size);
            Assert.Equal(expectedDestBytes, destinationBytes);
        }
    }
}