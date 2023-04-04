namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;
    using File = System.IO.File;

    public class GivenConvertCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenConvertSourceToImgDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            testCommandHelper.AddTestMedia(destinationPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - convert source img to destination img
            var convertCommand = new ConvertCommand(new NullLogger<ConvertCommand>(), testCommandHelper, sourcePath,
                destinationPath, new Size(), false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            convertCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await convertCommand.Execute(cancellationTokenSource.Token);
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
        public async Task WhenConvertSourceToImgDestinationWithSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            testCommandHelper.AddTestMedia(destinationPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - convert source img to destination img
            var convertCommand = new ConvertCommand(new NullLogger<ConvertCommand>(), testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false);
            var result = await convertCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is identical within defined size
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data.Take(size).ToArray();
            var destinationBytes = testCommandHelper.GetTestMedia(destinationPath).Data;
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenConvertSourceToVhdDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - read source img to destination vhd
            var convertCommand = new ConvertCommand(new NullLogger<ConvertCommand>(), testCommandHelper, sourcePath,
                destinationPath, new Size(), false);
            var result = await convertCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get source bytes
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data;

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath, sourceBytes.Length);
            var destinationPathSize = new FileInfo(destinationPath).Length;

            // assert length is not the same (vhd file format different than img) and bytes are the same
            Assert.NotEqual(sourceBytes.Length, destinationPathSize);
            Assert.Equal(sourceBytes, destinationBytes);

            // delete destination path vhd
            File.Delete(destinationPath);
        }

        [Fact]
        public async Task WhenConvertSourceToVhdDestinationWithSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - read source img to destination vhd
            var convertCommand = new ConvertCommand(new NullLogger<ConvertCommand>(), testCommandHelper, sourcePath,
                destinationPath, new Size(size, Unit.Bytes), false);
            var result = await convertCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get source bytes
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data.Take(size).ToArray();

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, destinationPath, sourceBytes.Length);
            var destinationPathSize = new FileInfo(destinationPath).Length;

            // assert length is not the same (vhd file format different than img) and bytes are the same
            Assert.NotEqual(sourceBytes.Length, destinationPathSize);
            Assert.Equal(sourceBytes, destinationBytes);

            // delete destination path vhd
            File.Delete(destinationPath);
        }
    }
}